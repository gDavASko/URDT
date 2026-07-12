using System.Collections;
using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Tests.Stand;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M1 driver-primitives spike (AI-IMPLEMENTATION-GUIDE §4). Drives a click and
    /// inspects the resulting state ENTIRELY through <see cref="IUrdtDriver"/>:
    /// InjectPointer feeds the device-level InputSimulator, ResolveTarget/InspectNode
    /// read state via stable handles and the whitelist StateInspector. Verdicts are
    /// state-based only (invariant I3).
    /// </summary>
    public sealed class UrdtDriverPrimitivesTests : InputTestFixture
    {
        private const float TARGET_X = 250f;
        private const float TARGET_Y = 200f;

        private InputSimulator _inputSimulator;
        private UnityUrdtDriver _driver;
        private GameObject _targetGo;

        public override void TearDown()
        {
            if (_targetGo != null)
            {
                Object.DestroyImmediate(_targetGo);
                _targetGo = null;
            }

            if (_driver != null)
            {
                _driver.Dispose();
                _driver = null;
            }

            if (_inputSimulator != null)
            {
                _inputSimulator.Dispose();
                _inputSimulator = null;
            }

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator Driver_ClickThenInspect_ReportsStateThroughSpi()
        {
            _inputSimulator = new InputSimulator();
            _driver = new UnityUrdtDriver(_inputSimulator, new StateInspector());

            UrdtClickTarget target = CreateTarget();
            Handle handle = _driver.RegisterObject(_targetGo);
            Assert.IsTrue(handle.IsValid, "RegisterObject must return a valid stable handle.");

            Handle resolved = _driver.ResolveTarget(TargetRef.FromHandle(handle));
            Assert.AreEqual(handle, resolved, "ResolveTarget must resolve a registered handle O(1).");

            Vector2 point = new Vector2(TARGET_X, TARGET_Y);
            _driver.InjectPointer(point, PointerPhase.Move);
            yield return null;
            _driver.InjectPointer(point, PointerPhase.Down);
            yield return null;
            _driver.InjectPointer(point, PointerPhase.Up);
            yield return null;
            yield return null;

            NodeState state = _driver.InspectNode(handle);
            Assert.IsNotNull(state, "InspectNode must return a state for a registered handle.");
            Assert.IsTrue(state.ActiveInHierarchy, "Target must report activeInHierarchy true.");

            Dictionary<string, object> slice;
            Assert.IsTrue(
                state.Components.TryGetValue(nameof(UrdtClickTarget), out slice),
                "Inspect must include the custom component slice.");
            Assert.AreEqual(
                1,
                (int)slice["ClickCount"],
                "The click driven through IUrdtDriver must be observed by the game's own hit logic.");

            // Direct field-of-truth cross-check (independent of the inspected slice).
            Assert.AreEqual(1, target.ClickCount, "Target click count must equal 1 after a single click.");
        }

        [UnityTest]
        public IEnumerator Driver_QueryByComponent_ReturnsRegisteredTarget()
        {
            _inputSimulator = new InputSimulator();
            _driver = new UnityUrdtDriver(_inputSimulator, new StateInspector());

            CreateTarget();
            Handle handle = _driver.RegisterObject(_targetGo);

            Selector selector = new Selector { ByComponent = nameof(UrdtClickTarget) };
            IReadOnlyList<NodeState> nodes = _driver.QueryNodes(selector);

            Assert.AreEqual(1, nodes.Count, "Query by component must return the single registered target.");
            Assert.AreEqual(handle, nodes[0].Handle, "Queried node must carry the target's stable handle.");
            yield break;
        }

        private UrdtClickTarget CreateTarget()
        {
            _targetGo = new GameObject("UrdtClickTarget");
            UrdtClickTarget target = _targetGo.AddComponent<UrdtClickTarget>();
            target.ScreenPosition = new Vector2(TARGET_X, TARGET_Y);
            target.HitRadius = 40f;
            return target;
        }
    }
}
