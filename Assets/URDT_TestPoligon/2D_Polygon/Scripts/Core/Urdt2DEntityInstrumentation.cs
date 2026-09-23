using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using KBP.URDT.Inspect;

namespace KBP.URDT.TestPoligon.Mechanics2D.Core
{
    /// <summary>
    /// Second instrumentation pass for 2D mechanics: gives a beacon to every gameplay entity the
    /// name-based rules of <see cref="Urdt2DBeaconUtility"/> miss, so an external agent can perceive it:
    /// 1. objects the mechanic references through its own fields (avatar, ball, nozzle, hazards...),
    /// 2. objects carrying mechanic gameplay components (FireTarget, Well, Star...),
    /// 3. objects spawned at runtime (coins, barriers, bubbles) — picked up by a throttled rescan.
    /// Beacons are observation-only: they expose position and live read-only GameState.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Urdt2DEntityInstrumentation : MonoBehaviour
    {
        private const float RESCAN_INTERVAL = 0.2f;
        private const BindingFlags FIELD_FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        private BaseMechanic2DModule _module;
        private float _nextScan;
        private int _spawnCounter;
        private readonly HashSet<int> _initialObjects = new HashSet<int>();
        private readonly Dictionary<int, string> _roleByObject = new Dictionary<int, string>();

        public static void Attach(BaseMechanic2DModule module)
        {
            if (module == null) return;
            if (!module.TryGetComponent(out Urdt2DEntityInstrumentation watcher))
            {
                watcher = module.gameObject.AddComponent<Urdt2DEntityInstrumentation>();
            }

            watcher._module = module;
            watcher.CaptureInitial();
            watcher.Scan();
        }

        private void CaptureInitial()
        {
            _initialObjects.Clear();
            _roleByObject.Clear();
            Transform[] all = GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                _initialObjects.Add(all[i].gameObject.GetInstanceID());
            }

            CollectReferencedRoles();
        }

        private void LateUpdate()
        {
            if (Time.unscaledTime < _nextScan) return;
            _nextScan = Time.unscaledTime + RESCAN_INTERVAL;
            Scan();
        }

        private void Scan()
        {
            if (_module == null) return;

            Transform[] all = GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i].gameObject;
                if (go == gameObject || go.TryGetComponent(out UrdtDebugTarget _))
                {
                    continue;
                }

                int id = go.GetInstanceID();
                string role;
                if (_roleByObject.TryGetValue(id, out role))
                {
                    AddEntityBeacon(go, go.name, role);
                }
                else if (TryGetGameplayComponent(go, out string componentName))
                {
                    AddEntityBeacon(go, go.name, componentName);
                }
                else if (!_initialObjects.Contains(id) && IsSpawnedEntityRoot(go))
                {
                    string baseName = go.name.Replace("(Clone)", string.Empty).Trim();
                    AddEntityBeacon(go, baseName + "#" + (++_spawnCounter), "spawned:" + baseName);
                }
            }
        }

        /// <summary>Only the top of a spawned subtree becomes an entity (not its decorative children).</summary>
        private bool IsSpawnedEntityRoot(GameObject go)
        {
            // TextMeshPro builds sub-mesh children at runtime; they are rendering internals, not entities.
            if (go.GetComponent<TMPro.TMP_SubMeshUI>() != null || go.GetComponent<TMPro.TMP_Text>() != null)
            {
                return false;
            }

            Transform parent = go.transform.parent;
            return parent != null && _initialObjects.Contains(parent.gameObject.GetInstanceID());
        }

        private static void AddEntityBeacon(GameObject go, string targetId, string role)
        {
            if (go.GetComponent<RectTransform>() == null)
            {
                return;
            }

            Urdt2DInteractiveAreaTarget target = go.AddComponent<Urdt2DInteractiveAreaTarget>();
            target.ConfigureArea(targetId, go.name, role);
        }

        private bool TryGetGameplayComponent(GameObject go, out string componentName)
        {
            componentName = null;
            MonoBehaviour[] behaviours = go.GetComponents<MonoBehaviour>();
            for (int i = 0; i < behaviours.Length; i++)
            {
                MonoBehaviour behaviour = behaviours[i];
                if (behaviour == null || behaviour is BaseMechanic2DModule || behaviour is Urdt2DEntityInstrumentation)
                {
                    continue;
                }

                string ns = behaviour.GetType().Namespace ?? string.Empty;
                if (ns.StartsWith("KBP.URDT.TestPoligon.Mechanics2D", StringComparison.Ordinal))
                {
                    componentName = behaviour.GetType().Name;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Objects a mechanic keeps in its own fields are, by construction, meaningful entities.</summary>
        private void CollectReferencedRoles()
        {
            for (Type type = _module.GetType(); type != null && type != typeof(BaseMechanic2DModule); type = type.BaseType)
            {
                FieldInfo[] fields = type.GetFields(FIELD_FLAGS | BindingFlags.DeclaredOnly);
                for (int f = 0; f < fields.Length; f++)
                {
                    object value;
                    try { value = fields[f].GetValue(_module); }
                    catch (Exception) { continue; }

                    string role = fields[f].Name.TrimStart('_');
                    if (value is IEnumerable sequence && !(value is string) && !(value is Transform))
                    {
                        foreach (object element in sequence)
                        {
                            RegisterRole(element, role);
                        }
                    }
                    else
                    {
                        RegisterRole(value, role);
                    }
                }
            }
        }

        private void RegisterRole(object value, string role)
        {
            GameObject go = null;
            if (value is GameObject g) go = g;
            else if (value is Component c && c != null) go = c.gameObject;
            if (go == null || go == gameObject || !go.transform.IsChildOf(transform)) return;

            // Text labels and plain layout containers are already covered or carry no gameplay meaning.
            if (go.GetComponent<TMPro.TMP_Text>() != null) return;
            int id = go.GetInstanceID();
            if (!_roleByObject.ContainsKey(id))
            {
                _roleByObject[id] = role;
            }
        }
    }
}
