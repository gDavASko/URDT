using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Read-only EventSystem hit diagnostic for the Observe phase. It never injects
    /// input and never invokes UI callbacks.
    /// </summary>
    public sealed class HitTestHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<RaycastResult> _hits = new List<RaycastResult>(16);

        public HitTestHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Vector2 point;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out point))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "No target or point to hit-test.");
            }

            if (EventSystem.current == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_RUNTIME_UNAVAILABLE, "No active EventSystem.");
            }

            Canvas.ForceUpdateCanvases();
            _hits.Clear();
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = point
            };
            EventSystem.current.UpdateModules();
            EventSystem.current.RaycastAll(eventData, _hits);

            JArray hits = new JArray();
            for (int i = 0; i < _hits.Count; i++)
            {
                RaycastResult hit = _hits[i];
                GameObject gameObject = hit.gameObject;
                hits.Add(new JObject
                {
                    ["index"] = i,
                    ["name"] = gameObject != null ? gameObject.name : string.Empty,
                    ["path"] = gameObject != null ? BuildPath(gameObject.transform) : string.Empty,
                    ["module"] = hit.module != null ? hit.module.GetType().Name : string.Empty,
                    ["distance"] = hit.distance,
                    ["depth"] = hit.depth,
                    ["sortingLayer"] = hit.sortingLayer,
                    ["sortingOrder"] = hit.sortingOrder
                });
            }

            JObject data = new JObject
            {
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y },
                ["hitCount"] = _hits.Count,
                ["hits"] = hits,
                ["hit_check"] = "ugui_raycast_readonly",
                ["eventSystem"] = BuildEventSystemDiagnostics()
            };

            GameObject target;
            if (PayloadReader.TryResolveGameObject(_runtime, payload, out target))
            {
                data["targetDiagnostics"] = BuildTargetDiagnostics(target);
            }

            if (_hits.Count > 0 && _hits[0].gameObject != null)
            {
                data["topHit"] = new JObject
                {
                    ["name"] = _hits[0].gameObject.name,
                    ["path"] = BuildPath(_hits[0].gameObject.transform)
                };
            }

            return Response.Success(command.Id, data);
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            Stack<string> names = new Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }

        private static JObject BuildTargetDiagnostics(GameObject target)
        {
            JObject diagnostics = new JObject
            {
                ["name"] = target != null ? target.name : string.Empty,
                ["path"] = target != null ? BuildPath(target.transform) : string.Empty,
                ["activeSelf"] = target != null && target.activeSelf,
                ["activeInHierarchy"] = target != null && target.activeInHierarchy
            };

            if (target == null)
            {
                return diagnostics;
            }

            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                diagnostics["canvas"] = new JObject
                {
                    ["name"] = canvas.name,
                    ["path"] = BuildPath(canvas.transform),
                    ["enabled"] = canvas.enabled,
                    ["renderMode"] = canvas.renderMode.ToString(),
                    ["sortingOrder"] = canvas.sortingOrder,
                    ["scaleFactor"] = canvas.scaleFactor,
                    ["receivesEvents"] = canvas.gameObject.activeInHierarchy
                };
            }

            GraphicRaycaster raycaster = target.GetComponentInParent<GraphicRaycaster>();
            if (raycaster != null)
            {
                diagnostics["raycaster"] = new JObject
                {
                    ["name"] = raycaster.name,
                    ["path"] = BuildPath(raycaster.transform),
                    ["enabled"] = raycaster.enabled,
                    ["activeInHierarchy"] = raycaster.gameObject.activeInHierarchy,
                    ["ignoreReversedGraphics"] = raycaster.ignoreReversedGraphics,
                    ["blockingObjects"] = raycaster.blockingObjects.ToString()
                };
            }

            RectTransform rectTransform = target.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Vector3[] corners = new Vector3[4];
                rectTransform.GetWorldCorners(corners);
                JArray worldCorners = new JArray();
                for (int i = 0; i < corners.Length; i++)
                {
                    worldCorners.Add(new JObject
                    {
                        ["x"] = corners[i].x,
                        ["y"] = corners[i].y,
                        ["z"] = corners[i].z
                    });
                }

                diagnostics["rectTransform"] = new JObject
                {
                    ["rect"] = rectTransform.rect.ToString(),
                    ["lossyScale"] = new JObject
                    {
                        ["x"] = rectTransform.lossyScale.x,
                        ["y"] = rectTransform.lossyScale.y,
                        ["z"] = rectTransform.lossyScale.z
                    },
                    ["worldCorners"] = worldCorners
                };
            }

            JArray graphics = new JArray();
            Graphic[] targetGraphics = target.GetComponentsInChildren<Graphic>(false);
            for (int i = 0; i < targetGraphics.Length; i++)
            {
                Graphic graphic = targetGraphics[i];
                graphics.Add(new JObject
                {
                    ["name"] = graphic.name,
                    ["path"] = BuildPath(graphic.transform),
                    ["type"] = graphic.GetType().Name,
                    ["enabled"] = graphic.enabled,
                    ["raycastTarget"] = graphic.raycastTarget,
                    ["raycastPadding"] = graphic.raycastPadding.ToString(),
                    ["depth"] = graphic.depth,
                    ["canvasRendererCull"] = graphic.canvasRenderer != null && graphic.canvasRenderer.cull
                });
            }

            diagnostics["graphics"] = graphics;

            Selectable selectable = target.GetComponent<Selectable>();
            if (selectable != null)
            {
                Navigation navigation = selectable.navigation;
                diagnostics["selectable"] = new JObject
                {
                    ["type"] = selectable.GetType().Name,
                    ["interactable"] = selectable.interactable,
                    ["transition"] = selectable.transition.ToString(),
                    ["navigationMode"] = navigation.mode.ToString(),
                    ["selectOnUp"] = BuildSelectablePath(navigation.selectOnUp),
                    ["selectOnDown"] = BuildSelectablePath(navigation.selectOnDown),
                    ["selectOnLeft"] = BuildSelectablePath(navigation.selectOnLeft),
                    ["selectOnRight"] = BuildSelectablePath(navigation.selectOnRight)
                };
            }

            JArray canvasGroups = new JArray();
            CanvasGroup[] groups = target.GetComponentsInParent<CanvasGroup>(true);
            for (int i = 0; i < groups.Length; i++)
            {
                CanvasGroup group = groups[i];
                canvasGroups.Add(new JObject
                {
                    ["name"] = group.name,
                    ["path"] = BuildPath(group.transform),
                    ["alpha"] = group.alpha,
                    ["interactable"] = group.interactable,
                    ["blocksRaycasts"] = group.blocksRaycasts,
                    ["ignoreParentGroups"] = group.ignoreParentGroups,
                    ["activeInHierarchy"] = group.gameObject.activeInHierarchy
                });
            }

            diagnostics["canvasGroups"] = canvasGroups;
            return diagnostics;
        }

        private static string BuildSelectablePath(Selectable selectable)
        {
            return selectable != null ? BuildPath(selectable.transform) : string.Empty;
        }

        private static JObject BuildEventSystemDiagnostics()
        {
            EventSystem eventSystem = EventSystem.current;
            GameObject selected = eventSystem != null ? eventSystem.currentSelectedGameObject : null;
            JObject diagnostics = new JObject
            {
                ["current"] = eventSystem != null ? eventSystem.name : string.Empty,
                ["currentPath"] = eventSystem != null ? BuildPath(eventSystem.transform) : string.Empty,
                ["currentActiveInHierarchy"] = eventSystem != null && eventSystem.gameObject.activeInHierarchy,
                ["currentInputModule"] = eventSystem != null && eventSystem.currentInputModule != null
                    ? eventSystem.currentInputModule.GetType().Name
                    : string.Empty,
                ["selected"] = selected != null ? selected.name : string.Empty,
                ["selectedPath"] = selected != null ? BuildPath(selected.transform) : string.Empty
            };

            JArray systems = new JArray();
            EventSystem[] eventSystems = Object.FindObjectsByType<EventSystem>(FindObjectsSortMode.None);
            for (int i = 0; i < eventSystems.Length; i++)
            {
                EventSystem system = eventSystems[i];
                systems.Add(new JObject
                {
                    ["name"] = system.name,
                    ["path"] = BuildPath(system.transform),
                    ["enabled"] = system.enabled,
                    ["activeInHierarchy"] = system.gameObject.activeInHierarchy,
                    ["currentInputModule"] = system.currentInputModule != null
                        ? system.currentInputModule.GetType().Name
                        : string.Empty
                });
            }

            JArray raycasters = new JArray();
            BaseRaycaster[] activeRaycasters = Object.FindObjectsByType<BaseRaycaster>(FindObjectsSortMode.None);
            for (int i = 0; i < activeRaycasters.Length; i++)
            {
                BaseRaycaster raycaster = activeRaycasters[i];
                raycasters.Add(new JObject
                {
                    ["name"] = raycaster.name,
                    ["path"] = BuildPath(raycaster.transform),
                    ["type"] = raycaster.GetType().Name,
                    ["enabled"] = raycaster.enabled,
                    ["activeInHierarchy"] = raycaster.gameObject.activeInHierarchy,
                    ["rootRaycaster"] = raycaster.rootRaycaster != null ? raycaster.rootRaycaster.name : string.Empty,
                    ["eventCamera"] = raycaster.eventCamera != null ? raycaster.eventCamera.name : string.Empty
                });
            }

            diagnostics["eventSystems"] = systems;
            diagnostics["activeBaseRaycasters"] = raycasters;
            return diagnostics;
        }
    }
}
