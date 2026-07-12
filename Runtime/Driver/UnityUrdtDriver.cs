using System;
using System.Collections.Generic;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Registry;
using KBP.URDT.Timing;
using UnityEngine;

namespace KBP.URDT.Driver
{
    /// <summary>
    /// Unity implementation of the URDT Engine Driver SPI (milestones M1–M2, in-proc).
    /// Input goes through the device-level <see cref="InputSimulator"/> (invariant I1);
    /// the game keeps ownership of all hit logic (invariant I2); objects are addressed
    /// by stable handles resolved O(1) from an internal map (invariant I8).
    /// Object registration is an explicit seam for now and is superseded by the
    /// TestIdRegistry at milestone M3. Time control is delegated to an optional
    /// <see cref="Timing.TimeController"/>; when none was supplied the time members
    /// throw <see cref="NotSupportedException"/> (protocol error E_UNSUPPORTED).
    /// Scene lifecycle and diagnostics capture arrive at later milestones.
    /// </summary>
    public sealed class UnityUrdtDriver : IUrdtDriver, IDisposable
    {
        private readonly InputSimulator _inputSimulator;
        private readonly StateInspector _stateInspector;
        private readonly TimeController _timeController;
        private readonly TestIdRegistry _registry;
        private readonly Dictionary<Handle, GameObject> _objectsByHandle =
            new Dictionary<Handle, GameObject>();
        private readonly List<NodeState> _queryBuffer = new List<NodeState>(16);

        public UnityUrdtDriver(InputSimulator inputSimulator, StateInspector stateInspector)
            : this(inputSimulator, stateInspector, null, null)
        {
        }

        public UnityUrdtDriver(
            InputSimulator inputSimulator,
            StateInspector stateInspector,
            TimeController timeController)
            : this(inputSimulator, stateInspector, timeController, null)
        {
        }

        public UnityUrdtDriver(
            InputSimulator inputSimulator,
            StateInspector stateInspector,
            TimeController timeController,
            TestIdRegistry registry)
        {
            _inputSimulator = inputSimulator ?? throw new ArgumentNullException(nameof(inputSimulator));
            _stateInspector = stateInspector ?? throw new ArgumentNullException(nameof(stateInspector));
            _timeController = timeController;
            _registry = registry;
        }

        /// <summary>
        /// Registers an object and returns its stable handle (M1 seam; the
        /// TestIdRegistry takes over registration at M3).
        /// </summary>
        public Handle RegisterObject(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return Handle.Invalid;
            }

            Handle handle = Handle.FromInstanceId(gameObject.GetInstanceID());
            _objectsByHandle[handle] = gameObject;
            return handle;
        }

        public void UnregisterObject(Handle handle)
        {
            _objectsByHandle.Remove(handle);
        }

        public void InjectPointer(Vector2 screenPos, PointerPhase phase, int pointerId = 0)
        {
            _inputSimulator.InjectPointer(screenPos, phase, pointerId);
        }

        public Handle ResolveTarget(TargetRef reference)
        {
            if (reference.Handle.IsValid && _objectsByHandle.ContainsKey(reference.Handle))
            {
                return reference.Handle;
            }

            if (!string.IsNullOrEmpty(reference.TestId)
                && _registry != null
                && _registry.TryResolveTestId(reference.TestId, out Handle byTestId))
            {
                return byTestId;
            }

            // Point-based resolution is deferred (world-interaction contract).
            return Handle.Invalid;
        }

        public IReadOnlyList<NodeState> QueryNodes(Selector selector)
        {
            _queryBuffer.Clear();

            if (selector == null)
            {
                return _queryBuffer;
            }

            foreach (KeyValuePair<Handle, GameObject> entry in _objectsByHandle)
            {
                GameObject gameObject = entry.Value;
                if (gameObject == null || !Matches(gameObject, selector))
                {
                    continue;
                }

                NodeState state = _stateInspector.BuildNodeState(gameObject, entry.Key);
                if (state != null)
                {
                    _queryBuffer.Add(state);
                }
            }

            return _queryBuffer;
        }

        public NodeState InspectNode(Handle target, IReadOnlyList<string> componentWhitelist = null)
        {
            if (!target.IsValid)
            {
                return null;
            }

            GameObject gameObject;
            if (_objectsByHandle.TryGetValue(target, out gameObject) && gameObject != null)
            {
                return _stateInspector.BuildNodeState(gameObject, target, componentWhitelist);
            }

            if (_registry != null && _registry.TryResolveHandle(target, out gameObject))
            {
                return _stateInspector.BuildNodeState(gameObject, target, componentWhitelist);
            }

            return null;
        }

        public void StepFrame(int frames, float deltaMs)
        {
            EnsureTimeController();
            _timeController.StepFrame(frames, deltaMs);
        }

        public void SetTimeScale(float scale)
        {
            EnsureTimeController();
            _timeController.SetTimeScale(scale);
        }

        public void PinFixedDelta(float fixedDeltaMs, float maxDeltaMs)
        {
            EnsureTimeController();
            _timeController.PinFixedDelta(fixedDeltaMs, maxDeltaMs);
        }

        public void ResetScene(SceneRef scene)
        {
            throw new NotSupportedException("E_UNSUPPORTED: scene lifecycle arrives with SceneLifecycleManager.");
        }

        public CaptureResult Capture(CaptureOptions options)
        {
            throw new NotSupportedException("E_UNSUPPORTED: diagnostics capture arrives at milestone M6.");
        }

        public void Dispose()
        {
            _objectsByHandle.Clear();
            _queryBuffer.Clear();
        }

        private void EnsureTimeController()
        {
            if (_timeController == null)
            {
                throw new NotSupportedException(
                    "E_UNSUPPORTED: this driver was created without a TimeController.");
            }
        }

        private static bool Matches(GameObject gameObject, Selector selector)
        {
            if (!string.IsNullOrEmpty(selector.ByComponent))
            {
                return gameObject.GetComponent(selector.ByComponent) != null;
            }

            if (!string.IsNullOrEmpty(selector.ByTag))
            {
                return gameObject.CompareTag(selector.ByTag);
            }

            if (!string.IsNullOrEmpty(selector.ByPath))
            {
                return string.Equals(gameObject.name, selector.ByPath, StringComparison.Ordinal);
            }

            // Rule-based selection requires the TestIdRegistry (M3).
            return false;
        }
    }
}
