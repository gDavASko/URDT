using System.Collections.Generic;
using KBP.URDT.Driver;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Reads protocol payload fields and resolves addressing (testId/handle -> stable handle,
    /// and screen points for input) for the command handlers.
    /// </summary>
    public static class PayloadReader
    {
        private static readonly List<RaycastResult> RAYCAST_RESULTS = new List<RaycastResult>(16);
        private static readonly List<Vector2> POINT_CANDIDATES = new List<Vector2>(9);
        private static readonly Vector3[] WORLD_CORNERS = new Vector3[4];

        public static JObject AsObject(object payload)
        {
            return payload as JObject ?? new JObject();
        }

        public static string GetString(JObject payload, string key, string fallback = null)
        {
            JToken token = payload?[key];
            return token != null && token.Type != JTokenType.Null ? token.ToString() : fallback;
        }

        public static int GetInt(JObject payload, string key, int fallback = 0)
        {
            JToken token = payload?[key];
            return token != null && token.Type != JTokenType.Null ? token.Value<int>() : fallback;
        }

        public static float GetFloat(JObject payload, string key, float fallback = 0f)
        {
            JToken token = payload?[key];
            return token != null && token.Type != JTokenType.Null ? token.Value<float>() : fallback;
        }

        public static bool GetBool(JObject payload, string key, bool fallback = false)
        {
            JToken token = payload?[key];
            return token != null && token.Type != JTokenType.Null ? token.Value<bool>() : fallback;
        }

        public static bool TryResolveHandle(UrdtRuntime runtime, JObject payload, out Handle handle)
        {
            handle = Handle.Invalid;
            if (payload == null)
            {
                return false;
            }

            string handleId = GetString(payload, "handle");
            if (!string.IsNullOrEmpty(handleId))
            {
                Handle candidate = new Handle(handleId);
                if (runtime.Driver.ResolveTarget(TargetRef.FromHandle(candidate)).IsValid)
                {
                    handle = candidate;
                    return true;
                }
            }

            string testId = GetString(payload, "testId");
            if (!string.IsNullOrEmpty(testId) && runtime.Registry.TryResolveTestId(testId, out handle))
            {
                return true;
            }

            string path = GetString(payload, "path") ?? GetString(payload, "byPath");
            string tier;
            if (!string.IsNullOrEmpty(path) && runtime.Registry.TryResolveByPath(path, out handle, out tier))
            {
                return true;
            }

            handle = Handle.Invalid;
            return false;
        }

        public static bool TryResolveHandle(UrdtRuntime runtime, JToken token, out Handle handle)
        {
            handle = Handle.Invalid;
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            JObject payload = token as JObject;
            if (payload != null)
            {
                return TryResolveHandle(runtime, payload, out handle);
            }

            if (token.Type == JTokenType.String)
            {
                return TryResolveHandle(runtime, new JObject { ["testId"] = token.ToString() }, out handle);
            }

            return false;
        }

        /// <summary>
        /// Resolves a screen point: explicit <c>x</c>/<c>y</c> if present, otherwise a
        /// UGUI-raycastable point of the addressed object, falling back to its center.
        /// </summary>
        public static bool TryGetScreenPoint(UrdtRuntime runtime, JObject payload, out Vector2 point)
        {
            point = Vector2.zero;
            if (payload == null)
            {
                return false;
            }

            if (payload["x"] != null && payload["y"] != null)
            {
                point = new Vector2(GetFloat(payload, "x"), GetFloat(payload, "y"));
                return true;
            }

            Handle handle;
            if (TryResolveHandle(runtime, payload, out handle))
            {
                GameObject target;
                if (TryResolveGameObject(runtime, handle, out target)
                    && TryGetUguiRaycastablePoint(target, out point))
                {
                    return true;
                }

                NodeState state = runtime.Driver.InspectNode(handle);
                if (state != null && state.ScreenPosition.HasValue)
                {
                    point = state.ScreenPosition.Value;
                    return true;
                }
            }

            return false;
        }

        public static bool TryGetScreenPoint(UrdtRuntime runtime, JToken token, out Vector2 point)
        {
            point = Vector2.zero;
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            JObject payload = token as JObject;
            if (payload != null)
            {
                return TryGetScreenPoint(runtime, payload, out point);
            }

            if (token.Type == JTokenType.String)
            {
                return TryGetScreenPoint(runtime, new JObject { ["testId"] = token.ToString() }, out point);
            }

            return false;
        }

        public static bool TryResolveGameObject(UrdtRuntime runtime, JObject payload, out GameObject target)
        {
            target = null;
            Handle handle;
            return TryResolveHandle(runtime, payload, out handle)
                && TryResolveGameObject(runtime, handle, out target);
        }

        public static bool HasAddressedTarget(JObject payload)
        {
            return payload != null
                && (!string.IsNullOrEmpty(GetString(payload, "handle"))
                    || !string.IsNullOrEmpty(GetString(payload, "testId"))
                    || !string.IsNullOrEmpty(GetString(payload, "path"))
                    || !string.IsNullOrEmpty(GetString(payload, "byPath")));
        }

        public static bool IsTopHitRelated(GameObject target, Vector2 point, out GameObject topHit)
        {
            topHit = null;
            if (target == null || EventSystem.current == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            EventSystem.current.UpdateModules();
            RAYCAST_RESULTS.Clear();
            PointerEventData eventData = new PointerEventData(EventSystem.current)
            {
                position = point
            };
            EventSystem.current.RaycastAll(eventData, RAYCAST_RESULTS);

            if (RAYCAST_RESULTS.Count > 0)
            {
                topHit = RAYCAST_RESULTS[0].gameObject;
            }

            bool related = topHit != null && IsRelatedToTarget(topHit.transform, target.transform);
            RAYCAST_RESULTS.Clear();
            return related;
        }

        private static bool TryResolveGameObject(UrdtRuntime runtime, Handle handle, out GameObject target)
        {
            target = null;
            return runtime != null
                && runtime.Registry != null
                && runtime.Registry.TryResolveHandle(handle, out target)
                && target != null;
        }

        private static bool TryGetUguiRaycastablePoint(GameObject target, out Vector2 point)
        {
            point = Vector2.zero;
            if (target == null || !target.activeInHierarchy)
            {
                return false;
            }

            RectTransform rectTransform = target.GetComponent<RectTransform>();
            EventSystem eventSystem = EventSystem.current;
            if (rectTransform == null || eventSystem == null)
            {
                return false;
            }

            Canvas.ForceUpdateCanvases();
            eventSystem.UpdateModules();
            Camera camera = GetCanvasCamera(target);
            BuildPointCandidates(rectTransform, camera, POINT_CANDIDATES);

            for (int i = 0; i < POINT_CANDIDATES.Count; i++)
            {
                Vector2 candidate = POINT_CANDIDATES[i];
                if (IsRaycastableHit(eventSystem, target.transform, candidate))
                {
                    point = candidate;
                    POINT_CANDIDATES.Clear();
                    return true;
                }
            }

            POINT_CANDIDATES.Clear();
            return false;
        }

        private static void BuildPointCandidates(
            RectTransform rectTransform,
            Camera camera,
            List<Vector2> candidates)
        {
            candidates.Clear();
            rectTransform.GetWorldCorners(WORLD_CORNERS);
            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, WORLD_CORNERS[0]);
            Vector2 max = min;

            for (int i = 1; i < WORLD_CORNERS.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, WORLD_CORNERS[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            float insetX = Mathf.Min(12f, Mathf.Max(1f, (max.x - min.x) * 0.15f));
            float insetY = Mathf.Min(12f, Mathf.Max(1f, (max.y - min.y) * 0.15f));
            float left = min.x + insetX;
            float right = max.x - insetX;
            float bottom = min.y + insetY;
            float top = max.y - insetY;
            float centerX = (min.x + max.x) * 0.5f;
            float centerY = (min.y + max.y) * 0.5f;

            candidates.Add(new Vector2(centerX, centerY));
            candidates.Add(new Vector2(left, centerY));
            candidates.Add(new Vector2(right, centerY));
            candidates.Add(new Vector2(centerX, bottom));
            candidates.Add(new Vector2(centerX, top));
            candidates.Add(new Vector2(left, bottom));
            candidates.Add(new Vector2(left, top));
            candidates.Add(new Vector2(right, bottom));
            candidates.Add(new Vector2(right, top));
        }

        private static bool IsRaycastableHit(
            EventSystem eventSystem,
            Transform target,
            Vector2 point)
        {
            RAYCAST_RESULTS.Clear();
            PointerEventData eventData = new PointerEventData(eventSystem)
            {
                position = point
            };
            eventSystem.RaycastAll(eventData, RAYCAST_RESULTS);

            bool matched = RAYCAST_RESULTS.Count > 0
                && RAYCAST_RESULTS[0].gameObject != null
                && IsRelatedToTarget(RAYCAST_RESULTS[0].gameObject.transform, target);

            RAYCAST_RESULTS.Clear();
            return matched;
        }

        private static bool IsRelatedToTarget(Transform hit, Transform target)
        {
            if (hit == null || target == null)
            {
                return false;
            }

            return hit == target || hit.IsChildOf(target) || target.IsChildOf(hit);
        }

        private static Camera GetCanvasCamera(GameObject target)
        {
            Canvas canvas = target.GetComponentInParent<Canvas>();
            if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return canvas.worldCamera;
            }

            return null;
        }
    }
}
