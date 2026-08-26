using System;
using System.Collections.Generic;
using System.Reflection;
using KBP.URDT.Driver;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Builds whitelisted state slices of game objects for `inspect`/`query`.
    /// Reflection accessors are resolved once per component type and cached
    /// (NFR-PERF-2): no per-call reflection lookups on repeated inspects.
    /// Verdicts are made from this state, never from screenshots (invariant I3).
    /// </summary>
    public sealed class StateInspector
    {
        private static readonly Dictionary<Type, CachedMember[]> ACCESSOR_CACHE =
            new Dictionary<Type, CachedMember[]>();

        private static readonly List<Component> COMPONENT_BUFFER = new List<Component>(16);

        public NodeState BuildNodeState(
            GameObject gameObject,
            Handle handle,
            IReadOnlyList<string> componentWhitelist = null)
        {
            if (gameObject == null)
            {
                return null;
            }

            NodeState state = new NodeState
            {
                Handle = handle,
                Name = gameObject.name,
                ActiveInHierarchy = gameObject.activeInHierarchy,
                ScreenPosition = ComputeScreenPosition(gameObject),
                Components = new Dictionary<string, Dictionary<string, object>>()
            };

            gameObject.GetComponents(COMPONENT_BUFFER);

            for (int i = 0; i < COMPONENT_BUFFER.Count; i++)
            {
                Component component = COMPONENT_BUFFER[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                string typeName = type.Name;

                if (componentWhitelist != null && !ContainsName(componentWhitelist, typeName))
                {
                    continue;
                }

                Dictionary<string, object> slice = BuildComponentSlice(component, type);
                if (slice != null)
                {
                    state.Components[typeName] = slice;
                }
            }

            COMPONENT_BUFFER.Clear();
            return state;
        }

        private static Dictionary<string, object> BuildComponentSlice(Component component, Type type)
        {
            if (component is Transform transform)
            {
                return new Dictionary<string, object>
                {
                    { "position", transform.position },
                    { "rotation", transform.rotation.eulerAngles },
                    { "localScale", transform.localScale }
                };
            }

            CachedMember[] members = GetOrBuildAccessors(type);
            if (members.Length == 0)
            {
                return null;
            }

            Dictionary<string, object> slice = new Dictionary<string, object>(members.Length);
            for (int i = 0; i < members.Length; i++)
            {
                // A misbehaving inspectable getter must not abort the whole inspect/query
                // (fault isolation, TZ §7.5): report the failure per-member and continue.
                try
                {
                    slice[members[i].Name] = members[i].Read(component);
                }
                catch (Exception exception)
                {
                    slice[members[i].Name] = "E_READ: " + exception.GetType().Name;
                }
            }

            return slice;
        }

        private static CachedMember[] GetOrBuildAccessors(Type type)
        {
            CachedMember[] cached;
            if (ACCESSOR_CACHE.TryGetValue(type, out cached))
            {
                return cached;
            }

            List<CachedMember> members = new List<CachedMember>();
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            FieldInfo[] fields = type.GetFields(FLAGS);
            for (int i = 0; i < fields.Length; i++)
            {
                if (fields[i].IsDefined(typeof(TestInspectableAttribute), true))
                {
                    members.Add(new CachedMember(fields[i]));
                }
            }

            PropertyInfo[] properties = type.GetProperties(FLAGS);
            for (int i = 0; i < properties.Length; i++)
            {
                if (properties[i].CanRead
                    && properties[i].GetIndexParameters().Length == 0
                    && properties[i].IsDefined(typeof(TestInspectableAttribute), true))
                {
                    members.Add(new CachedMember(properties[i]));
                }
            }

            cached = members.ToArray();
            ACCESSOR_CACHE[type] = cached;
            return cached;
        }

        private static Vector2? ComputeScreenPosition(GameObject gameObject)
        {
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (rectTransform != null)
            {
                Canvas.ForceUpdateCanvases();
                Canvas canvas = gameObject.GetComponentInParent<Canvas>();
                Camera canvasCamera = null;
                if (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                {
                    canvasCamera = canvas.worldCamera;
                }

                Vector3 worldCenter = rectTransform.TransformPoint(rectTransform.rect.center);
                return RectTransformUtility.WorldToScreenPoint(canvasCamera, worldCenter);
            }

            Camera camera = Camera.main;
            if (camera == null)
            {
                return null;
            }

            Vector3 screenPoint = camera.WorldToScreenPoint(gameObject.transform.position);
            return new Vector2(screenPoint.x, screenPoint.y);
        }

        private static bool ContainsName(IReadOnlyList<string> names, string candidate)
        {
            for (int i = 0; i < names.Count; i++)
            {
                if (string.Equals(names[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct CachedMember
        {
            private readonly FieldInfo _field;
            private readonly PropertyInfo _property;

            public CachedMember(FieldInfo field)
            {
                _field = field;
                _property = null;
            }

            public CachedMember(PropertyInfo property)
            {
                _field = null;
                _property = property;
            }

            public string Name
            {
                get { return _field != null ? _field.Name : _property.Name; }
            }

            public object Read(object target)
            {
                return _field != null ? _field.GetValue(target) : _property.GetValue(target);
            }
        }
    }
}
