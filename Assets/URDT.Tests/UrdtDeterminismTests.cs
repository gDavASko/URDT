using System.Collections;
using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Tests.Stand;
using KBP.URDT.Timing;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M2 determinism spike (AI-IMPLEMENTATION-GUIDE §4, 03-determinism-time-control.md).
    /// Proves that <see cref="TimeController"/> gates time-mode commands, that a
    /// frame-stepped physics scenario reproduces run-to-run independently of host FPS,
    /// and that the M0 drag scenario advances purely by explicit frame steps with no
    /// wall-clock waits. Verdicts are state-based only (invariant I3).
    /// </summary>
    public sealed class UrdtDeterminismTests : InputTestFixture
    {
        private const float FRAME_DELTA_MS = 1000f / 60f;
        private const float MAX_DELTA_MS = 100f;

        private TimeController _timeController;
        private InputSimulator _inputSimulator;
        private UnityUrdtDriver _driver;
        private readonly List<GameObject> _spawned = new List<GameObject>();

        public override void TearDown()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null)
                {
                    Object.DestroyImmediate(_spawned[i]);
                }
            }

            _spawned.Clear();

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

            if (_timeController != null)
            {
                _timeController.Dispose();
                _timeController = null;
            }

            base.TearDown();
        }

        [Test]
        public void TimeCommands_InRealtimeMode_ThrowTimeModeError()
        {
            _timeController = new TimeController();

            Assert.AreEqual(TimeMode.Realtime, _timeController.Mode);
            Assert.Throws<System.NotSupportedException>(() => _timeController.StepFrame(1, FRAME_DELTA_MS));
            Assert.Throws<System.NotSupportedException>(() => _timeController.PinFixedDelta(FRAME_DELTA_MS, MAX_DELTA_MS));
        }

        [UnityTest]
        public IEnumerator FallingBody_SteppedByFrames_IsReproducibleAcrossRuns()
        {
            float[] first = RunFallingBody(seed: 12345);
            yield return null;
            float[] second = RunFallingBody(seed: 12345);

            Assert.AreEqual(first.Length, second.Length, "Both runs must produce the same number of samples.");
            for (int i = 0; i < first.Length; i++)
            {
                Assert.AreEqual(
                    first[i],
                    second[i],
                    1e-6f,
                    "Trajectory diverged at step " + i + " — deterministic stepping is not reproducible.");
            }

            Assert.Less(
                first[first.Length - 1],
                first[0],
                "The body must actually fall under gravity (physics really simulated).");
        }

        [UnityTest]
        public IEnumerator DragScenario_FrameStepped_ReachesReceiverWithoutWallClockWaits()
        {
            _inputSimulator = new InputSimulator();
            _timeController = new TimeController();
            _driver = new UnityUrdtDriver(_inputSimulator, new StateInspector(), _timeController);

            Vector2 start = new Vector2(100f, 100f);
            Vector2 target = new Vector2(400f, 300f);

            UrdtTestReceiver receiver = CreateReceiver(target);
            UrdtTestDraggable draggable = CreateDraggable(start, receiver);

            // PlayMode-harness path (§2.3): the yield loop owns frame advancement and the
            // engine keeps driving input per frame, so we do not force ProcessEventsManually.
            _timeController.EnterDeterministic(seed: 777, manualInputPump: false);
            _timeController.PinFixedDelta(FRAME_DELTA_MS, MAX_DELTA_MS);

            yield return StepPointer(start, PointerPhase.Move);
            yield return StepPointer(start, PointerPhase.Down);

            const int MOVE_STEPS = 10;
            for (int step = 1; step <= MOVE_STEPS; step++)
            {
                Vector2 point = Vector2.Lerp(start, target, (float)step / MOVE_STEPS);
                yield return StepPointer(point, PointerPhase.Move);
            }

            yield return StepPointer(target, PointerPhase.Up);

            _timeController.ExitDeterministic();

            Assert.IsTrue(
                draggable.ReachedReceiver,
                "The frame-stepped drag must drive the draggable to the receiver.");
        }

        private IEnumerator StepPointer(Vector2 screenPos, PointerPhase phase)
        {
            _driver.InjectPointer(screenPos, phase);
            _timeController.StepFrame(1, FRAME_DELTA_MS);
            yield return null;
        }

        private float[] RunFallingBody(int seed)
        {
            using (TimeController controller = new TimeController())
            {
                controller.EnterDeterministic(seed);
                controller.PinFixedDelta(FRAME_DELTA_MS, MAX_DELTA_MS);

                GameObject body = new GameObject("UrdtFallingBody");
                body.transform.position = new Vector3(0f, 100f, 0f);
                Rigidbody rigidbody = body.AddComponent<Rigidbody>();
                rigidbody.useGravity = true;
                rigidbody.position = body.transform.position;

                const int STEPS = 30;
                float[] samples = new float[STEPS];
                for (int i = 0; i < STEPS; i++)
                {
                    controller.StepFrame(1, FRAME_DELTA_MS);
                    samples[i] = body.transform.position.y;
                }

                Object.DestroyImmediate(body);
                controller.ExitDeterministic();
                return samples;
            }
        }

        private UrdtTestReceiver CreateReceiver(Vector2 screenPosition)
        {
            GameObject receiverGo = new GameObject("UrdtTestReceiver");
            _spawned.Add(receiverGo);
            UrdtTestReceiver receiver = receiverGo.AddComponent<UrdtTestReceiver>();
            receiver.ScreenPosition = screenPosition;
            receiver.AcceptRadius = 40f;
            return receiver;
        }

        private UrdtTestDraggable CreateDraggable(Vector2 screenPosition, UrdtTestReceiver receiver)
        {
            GameObject dragGo = new GameObject("UrdtTestDraggable");
            _spawned.Add(dragGo);
            UrdtTestDraggable draggable = dragGo.AddComponent<UrdtTestDraggable>();
            draggable.ScreenPosition = screenPosition;
            draggable.GrabRadius = 60f;
            draggable.Receiver = receiver;
            return draggable;
        }
    }
}
