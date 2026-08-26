using System;
using System.Collections.Generic;
using System.Reflection;
using KBP.URDT.Driver;
using KBP.URDT.Inspect;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace KBP.URDT.Registry
{
    /// <summary>
    /// Hybrid TestId index (04, 05). Populated from three coordinated sources:
    /// (A) a one-off rule scan on <c>sceneLoaded</c> (amortised, not a per-frame hot
    /// path), (B) incremental O(1) registration on instantiate/pool-get/OnEnable, and
    /// (C) lazy resolve at command time. Addressing is by stable handle
    /// (<c>InstanceID</c>-backed, O(1) dictionary resolve — invariant I8); the
    /// hierarchy-path resolver is a discouraged escape hatch flagged
    /// <c>path_fallback</c>. No <c>GameObject.Find</c>/<c>Transform.Find</c> is used.
    /// Rule predicates are evaluated ONLY at registration points and only over stable
    /// traits (04 §2.4).
    /// </summary>
    public sealed class TestIdRegistry : IDisposable
    {
        public const string PATH_FALLBACK_TIER = "path_fallback";

        private readonly List<SelectorRule> _rules = new List<SelectorRule>();
        private readonly Dictionary<Handle, GameObject> _byHandle = new Dictionary<Handle, GameObject>();
        private readonly Dictionary<string, Handle> _byTestId = new Dictionary<string, Handle>();
        private readonly Dictionary<int, Handle> _instanceToHandle = new Dictionary<int, Handle>();
        private readonly Dictionary<Handle, string> _handleToTestId = new Dictionary<Handle, string>();
        private readonly Dictionary<Handle, GameObject> _inactiveByHandle = new Dictionary<Handle, GameObject>();
        private readonly Dictionary<string, Handle> _inactiveByTestId = new Dictionary<string, Handle>();
        private readonly Dictionary<Handle, string> _inactiveHandleToTestId = new Dictionary<Handle, string>();

        private bool _sceneScanEnabled;

        public event Action<RegistrationInfo> ObjectRegistered;

        public event Action<RegistrationInfo> ObjectUnregistered;

        public int Count
        {
            get { return _byHandle.Count; }
        }

        public void AddRule(SelectorRule rule)
        {
            if (rule != null)
            {
                _rules.Add(rule);
            }
        }

        /// <summary>
        /// Subscribes to <c>SceneManager.sceneLoaded</c> so loaded scenes are scanned
        /// (source A). Idempotent.
        /// </summary>
        public void EnableSceneScan()
        {
            if (_sceneScanEnabled)
            {
                return;
            }

            SceneManager.sceneLoaded += OnSceneLoaded;
            _sceneScanEnabled = true;
        }

        public void DisableSceneScan()
        {
            if (!_sceneScanEnabled)
            {
                return;
            }

            SceneManager.sceneLoaded -= OnSceneLoaded;
            _sceneScanEnabled = false;
        }

        /// <summary>
        /// Source A: walks the scene hierarchy once and registers every object that
        /// matches a rule. Amortised O(N); not a per-frame hot path.
        /// </summary>
        public void ScanScene(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
            {
                return;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                RegisterHierarchy(roots[i].transform, RegistrationSource.SceneScan);
            }
        }

        /// <summary>
        /// Source B: registers a single object in O(1) (rule eval on this object only,
        /// no scene walk). De-dups by InstanceID: re-registering the same instance
        /// returns the existing handle without a duplicate event.
        /// </summary>
        public Handle Register(GameObject gameObject, RegistrationSource source)
        {
            if (gameObject == null)
            {
                return Handle.Invalid;
            }

            int instanceId = gameObject.GetInstanceID();
            Handle existing;
            if (_instanceToHandle.TryGetValue(instanceId, out existing))
            {
                return existing;
            }

            string testId;
            if (!TryBuildTestId(gameObject, out testId))
            {
                return Handle.Invalid;
            }

            Handle handle = Handle.FromInstanceId(instanceId);

            RemoveInactive(handle, testId);

            _instanceToHandle[instanceId] = handle;
            _byHandle[handle] = gameObject;
            _byTestId[testId] = handle;
            _handleToTestId[handle] = testId;

            TestIdLabel label;
            if (!gameObject.TryGetComponent(out label))
            {
                label = gameObject.AddComponent<TestIdLabel>();
            }

            label.Bind(this, testId, handle);

            RaiseRegistered(new RegistrationInfo(handle, testId, gameObject, source, null));
            return handle;
        }

        public void Unregister(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return;
            }

            Handle handle;
            if (_instanceToHandle.TryGetValue(gameObject.GetInstanceID(), out handle))
            {
                UnregisterHandle(handle, gameObject, "unregister");
            }
        }

        public bool TryResolveTestId(string testId, out Handle handle)
        {
            if (!string.IsNullOrEmpty(testId) && _byTestId.TryGetValue(testId, out handle))
            {
                return true;
            }

            if (!string.IsNullOrEmpty(testId) && _inactiveByTestId.TryGetValue(testId, out handle))
            {
                return true;
            }

            handle = Handle.Invalid;
            return false;
        }

        /// <summary>
        /// Collects the handles of registered objects whose GameObject satisfies
        /// <paramref name="predicate"/> into <paramref name="results"/> (query path,
        /// not a per-frame hot path).
        /// </summary>
        public void CollectMatching(Func<GameObject, bool> predicate, List<Handle> results)
        {
            results.Clear();
            foreach (KeyValuePair<Handle, GameObject> entry in _byHandle)
            {
                if (entry.Value != null && (predicate == null || predicate(entry.Value)))
                {
                    results.Add(entry.Key);
                }
            }
        }

        public bool TryResolveHandle(Handle handle, out GameObject gameObject)
        {
            if (handle.IsValid && _byHandle.TryGetValue(handle, out gameObject) && gameObject != null)
            {
                return true;
            }

            if (handle.IsValid && _inactiveByHandle.TryGetValue(handle, out gameObject) && gameObject != null)
            {
                return true;
            }

            gameObject = null;
            return false;
        }

        public bool TryGetTestId(Handle handle, out string testId)
        {
            return _handleToTestId.TryGetValue(handle, out testId)
                || _inactiveHandleToTestId.TryGetValue(handle, out testId);
        }

        /// <summary>
        /// Priority-3 escape hatch (05 §2.2): resolves an object by hierarchy path over
        /// the active scene roots and lazily registers it (source C). DISCOURAGED — the
        /// path is brittle; obtain a stable handle via a rule/query instead. The result
        /// is flagged <see cref="PATH_FALLBACK_TIER"/> and a warning is logged.
        /// Walks children via <c>Transform.GetChild</c> — never <c>Transform.Find</c>.
        /// </summary>
        public bool TryResolveByPath(string path, out Handle handle, out string tier)
        {
            tier = PATH_FALLBACK_TIER;
            handle = Handle.Invalid;

            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            string[] segments = path.Split('/');
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            Transform current = FindRootByName(roots, segments[0]);
            for (int i = 1; i < segments.Length && current != null; i++)
            {
                current = FindChildByName(current, segments[i]);
            }

            if (current == null)
            {
                return false;
            }

            Debug.LogWarning(
                "URDT addressing_tier=path_fallback is discouraged (brittle path). "
                + "Obtain a stable handle via query. path=" + path);

            handle = Register(current.gameObject, RegistrationSource.LazyResolve);
            return handle.IsValid;
        }

        public void Dispose()
        {
            DisableSceneScan();
            _byHandle.Clear();
            _byTestId.Clear();
            _instanceToHandle.Clear();
            _handleToTestId.Clear();
            _inactiveByHandle.Clear();
            _inactiveByTestId.Clear();
            _inactiveHandleToTestId.Clear();
            _rules.Clear();
            ObjectRegistered = null;
            ObjectUnregistered = null;
        }

        internal void NotifyLabelEnabled(TestIdLabel label)
        {
            if (label == null)
            {
                return;
            }

            GameObject go = label.gameObject;
            if (!_instanceToHandle.ContainsKey(go.GetInstanceID()))
            {
                // Pool reuse: the object was unregistered on OnDisable and is now active
                // again — re-register with a freshly evaluated TestId (04 §4.1).
                Register(go, RegistrationSource.Incremental);
            }
        }

        internal void NotifyLabelDisabled(TestIdLabel label)
        {
            if (label != null)
            {
                UnregisterHandle(label.Handle, label.gameObject, "disabled");
            }
        }

        internal void NotifyLabelDestroyed(TestIdLabel label)
        {
            if (label != null)
            {
                UnregisterHandle(label.Handle, label.gameObject, "destroyed");
            }
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ScanScene(scene);
        }

        private void RegisterHierarchy(Transform root, RegistrationSource source)
        {
            Register(root.gameObject, source);

            int childCount = root.childCount;
            for (int i = 0; i < childCount; i++)
            {
                RegisterHierarchy(root.GetChild(i), source);
            }
        }

        private void UnregisterHandle(Handle handle, GameObject gameObject, string reason)
        {
            if (!handle.IsValid || !_byHandle.ContainsKey(handle))
            {
                return;
            }

            string testId;
            _handleToTestId.TryGetValue(handle, out testId);

            if (string.Equals(reason, "disabled", StringComparison.Ordinal)
                && gameObject != null
                && !gameObject.activeInHierarchy)
            {
                _inactiveByHandle[handle] = gameObject;
                _inactiveHandleToTestId[handle] = testId;
                if (!string.IsNullOrEmpty(testId))
                {
                    _inactiveByTestId[testId] = handle;
                }
            }

            _byHandle.Remove(handle);
            _handleToTestId.Remove(handle);
            if (!string.IsNullOrEmpty(testId))
            {
                _byTestId.Remove(testId);
            }

            if (gameObject != null)
            {
                _instanceToHandle.Remove(gameObject.GetInstanceID());
            }

            RaiseUnregistered(new RegistrationInfo(handle, testId, gameObject, RegistrationSource.Incremental, reason));
        }

        private void RemoveInactive(Handle handle, string testId)
        {
            _inactiveByHandle.Remove(handle);
            string inactiveTestId;
            _inactiveHandleToTestId.TryGetValue(handle, out inactiveTestId);
            _inactiveHandleToTestId.Remove(handle);

            if (!string.IsNullOrEmpty(inactiveTestId))
            {
                _inactiveByTestId.Remove(inactiveTestId);
            }

            if (!string.IsNullOrEmpty(testId))
            {
                _inactiveByTestId.Remove(testId);
            }
        }

        private bool TryBuildTestId(GameObject gameObject, out string testId)
        {
            if (TryGetExplicitInspectableId(gameObject, out testId))
            {
                testId = ResolveCollision(testId);
                return true;
            }

            for (int i = 0; i < _rules.Count; i++)
            {
                SelectorRule rule = _rules[i];
                if (rule.Predicate != null && rule.Predicate.Matches(gameObject))
                {
                    testId = ResolveCollision(rule.TestIdTemplate);
                    return true;
                }
            }

            testId = null;
            return false;
        }

        private static bool TryGetExplicitInspectableId(GameObject gameObject, out string testId)
        {
            testId = null;
            Component[] components = gameObject.GetComponents<Component>();
            const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            for (int i = 0; i < components.Length; i++)
            {
                Component component = components[i];
                if (component == null)
                {
                    continue;
                }

                Type type = component.GetType();
                PropertyInfo property = type.GetProperty("TargetId", FLAGS)
                    ?? type.GetProperty("TestId", FLAGS);
                if (property != null
                    && property.CanRead
                    && property.GetIndexParameters().Length == 0
                    && property.IsDefined(typeof(TestInspectableAttribute), true))
                {
                    testId = property.GetValue(component) as string;
                    if (!string.IsNullOrEmpty(testId))
                    {
                        return true;
                    }
                }

                FieldInfo field = type.GetField("TargetId", FLAGS)
                    ?? type.GetField("TestId", FLAGS);
                if (field != null && field.IsDefined(typeof(TestInspectableAttribute), true))
                {
                    testId = field.GetValue(component) as string;
                    if (!string.IsNullOrEmpty(testId))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private string ResolveCollision(string baseId)
        {
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = "urdt";
            }

            if (!_byTestId.ContainsKey(baseId))
            {
                return baseId;
            }

            int index = 1;
            string candidate = baseId + "_" + index;
            while (_byTestId.ContainsKey(candidate))
            {
                index++;
                candidate = baseId + "_" + index;
            }

            return candidate;
        }

        private void RaiseRegistered(RegistrationInfo info)
        {
            Action<RegistrationInfo> handler = ObjectRegistered;
            if (handler != null)
            {
                handler(info);
            }
        }

        private void RaiseUnregistered(RegistrationInfo info)
        {
            Action<RegistrationInfo> handler = ObjectUnregistered;
            if (handler != null)
            {
                handler(info);
            }
        }

        private static Transform FindRootByName(GameObject[] roots, string name)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name == name)
                {
                    return roots[i].transform;
                }
            }

            return null;
        }

        private static Transform FindChildByName(Transform parent, string name)
        {
            int childCount = parent.childCount;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }
            }

            return null;
        }
    }
}
