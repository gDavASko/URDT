using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Registry;
using KBP.URDT.Tests.Stand;
using NUnit.Framework;
using UnityEngine;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M3 indexing and addressing (AI-IMPLEMENTATION-GUIDE §4, 04/05). Covers selector
    /// rules with deterministic collision suffixes, incremental O(1) registration of an
    /// object spawned mid-scene (addressable without a full re-scan), lifecycle events,
    /// runtime-only labels, stable-handle resolve, and the discouraged path fallback.
    /// </summary>
    public sealed class UrdtRegistryTests
    {
        private const string MARKER = nameof(UrdtRegistryMarker);

        private TestIdRegistry _registry;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            _registry = new TestIdRegistry();
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();

            if (_registry != null)
            {
                _registry.Dispose();
                _registry = null;
            }
        }

        [Test]
        public void Register_MidScene_ResolvableByTestIdAndHandle_WithoutFullRescan()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "click_target"));

            RegistrationInfo captured = default;
            int events = 0;
            _registry.ObjectRegistered += info => { captured = info; events++; };

            GameObject spawned = CreateMarked("SpawnedMidScene");
            Handle handle = _registry.Register(spawned, RegistrationSource.Incremental);

            Assert.IsTrue(handle.IsValid, "Register must return a valid stable handle.");
            Assert.AreEqual(1, events, "Exactly one object_registered event must fire.");
            Assert.AreEqual(RegistrationSource.Incremental, captured.Source);

            Handle byTestId;
            Assert.IsTrue(_registry.TryResolveTestId("click_target", out byTestId), "TestId must resolve.");
            Assert.AreEqual(handle, byTestId, "TestId must resolve to the same handle.");

            GameObject resolved;
            Assert.IsTrue(_registry.TryResolveHandle(handle, out resolved), "Handle must resolve to the object.");
            Assert.AreSame(spawned, resolved, "Handle must resolve to the exact spawned object (no re-scan).");
        }

        [Test]
        public void Register_Collision_AppendsDeterministicIndexSuffix()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "tooth"));

            CreateMarked("A");
            CreateMarked("B");
            CreateMarked("C");

            _registry.Register(_spawned[0], RegistrationSource.Incremental);
            _registry.Register(_spawned[1], RegistrationSource.Incremental);
            _registry.Register(_spawned[2], RegistrationSource.Incremental);

            Handle h0, h1, h2;
            Assert.IsTrue(_registry.TryResolveTestId("tooth", out h0));
            Assert.IsTrue(_registry.TryResolveTestId("tooth_1", out h1));
            Assert.IsTrue(_registry.TryResolveTestId("tooth_2", out h2));
            Assert.AreNotEqual(h0, h1);
            Assert.AreNotEqual(h1, h2);
        }

        [Test]
        public void Register_SameInstanceTwice_IsDeduped()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "dedup"));
            int events = 0;
            _registry.ObjectRegistered += _ => events++;

            GameObject go = CreateMarked("Dedup");
            Handle first = _registry.Register(go, RegistrationSource.Incremental);
            Handle second = _registry.Register(go, RegistrationSource.Incremental);

            Assert.AreEqual(first, second, "Re-registering the same instance must return the same handle.");
            Assert.AreEqual(1, events, "De-dup must not fire a second object_registered event.");
            Assert.AreEqual(1, _registry.Count, "The registry must hold exactly one entry.");
        }

        [Test]
        public void Disable_Unregisters_And_Reenable_ReregistersFresh()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "pooled"));
            int registered = 0;
            int unregistered = 0;
            _registry.ObjectRegistered += _ => registered++;
            _registry.ObjectUnregistered += _ => unregistered++;

            GameObject go = CreateMarked("Pooled");
            _registry.Register(go, RegistrationSource.Incremental);
            Assert.AreEqual(1, registered);

            go.SetActive(false);
            Assert.AreEqual(1, unregistered, "OnDisable must unregister (pool return).");
            Assert.AreEqual(0, _registry.Count, "Disabled object must leave the index.");

            go.SetActive(true);
            Assert.AreEqual(2, registered, "OnEnable must re-register (pool get).");
            Assert.AreEqual(1, _registry.Count, "Re-enabled object must be back in the index.");
        }

        [Test]
        public void RegisteredLabel_IsRuntimeOnly_NotSaved()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "labelled"));
            GameObject go = CreateMarked("Labelled");
            _registry.Register(go, RegistrationSource.Incremental);

            TestIdLabel label;
            Assert.IsTrue(go.TryGetComponent(out label), "A runtime-only TestIdLabel must be attached.");
            Assert.AreEqual(HideFlags.DontSave, label.hideFlags, "The label must never be saved into scenes/prefabs.");
        }

        [Test]
        public void UnmatchedObject_IsNotRegistered()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "x"));

            GameObject bare = new GameObject("NoMarker");
            _spawned.Add(bare);
            Handle handle = _registry.Register(bare, RegistrationSource.Incremental);

            Assert.IsFalse(handle.IsValid, "An object matching no rule must not be registered.");
            Assert.AreEqual(0, _registry.Count);
        }

        [Test]
        public void TryResolveByPath_ResolvesAndFlagsDiscouragedTier()
        {
            _registry.AddRule(new SelectorRule(new NameMatchesPredicate("Child"), "child_node"));

            GameObject root = new GameObject("PathRoot");
            _spawned.Add(root);
            GameObject child = new GameObject("Child");
            _spawned.Add(child);
            child.transform.SetParent(root.transform);

            Handle handle;
            string tier;
            bool ok = _registry.TryResolveByPath("PathRoot/Child", out handle, out tier);

            Assert.IsTrue(ok, "Path fallback must resolve an existing hierarchy path.");
            Assert.IsTrue(handle.IsValid);
            Assert.AreEqual(TestIdRegistry.PATH_FALLBACK_TIER, tier, "Path resolution must be flagged path_fallback.");
        }

        [Test]
        public void Driver_ResolvesTestId_And_InspectsThroughRegistry()
        {
            _registry.AddRule(new SelectorRule(new HasComponentPredicate(MARKER), "driver_target"));

            InputSimulator inputSimulator = new InputSimulator();
            try
            {
                UnityUrdtDriver driver = new UnityUrdtDriver(
                    inputSimulator, new StateInspector(), null, _registry);

                GameObject go = CreateMarked("DriverTarget");
                Handle registered = _registry.Register(go, RegistrationSource.Incremental);

                Handle resolved = driver.ResolveTarget(TargetRef.FromTestId("driver_target"));
                Assert.AreEqual(registered, resolved, "Driver must resolve a TestId via the registry.");

                NodeState state = driver.InspectNode(resolved);
                Assert.IsNotNull(state, "Driver must inspect a registry-managed object.");
                Assert.IsTrue(state.ActiveInHierarchy);

                driver.Dispose();
            }
            finally
            {
                inputSimulator.Dispose();
            }
        }

        private GameObject CreateMarked(string name)
        {
            GameObject go = new GameObject(name);
            go.AddComponent<UrdtRegistryMarker>();
            _spawned.Add(go);
            return go;
        }
    }
}
