using System.Collections;
using KBP.URDT.Tests.Stand;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

namespace KBP.URDT.Tests
{
    /// <summary>
    /// M0 honest-input spike (AI-IMPLEMENTATION-GUIDE §4).
    /// Proves on a self-contained stand that a virtual Input System mouse drives
    /// the REAL new-input path (positive test) while the legacy Input.* polling
    /// pipeline stays genuinely unreachable (negative control). Verdicts are
    /// state-based only — no screenshots (invariant I3).
    /// </summary>
    public sealed class UrdtHonestInputSpikeTests : InputTestFixture
    {
        private const float START_X = 100f;
        private const float START_Y = 100f;
        private const float TARGET_X = 400f;
        private const float TARGET_Y = 300f;
        private const int DRAG_STEPS = 10;

        private GameObject _draggableGo;
        private GameObject _twinGo;
        private GameObject _receiverGo;

        public override void TearDown()
        {
            if (_draggableGo != null)
            {
                Object.DestroyImmediate(_draggableGo);
                _draggableGo = null;
            }

            if (_twinGo != null)
            {
                Object.DestroyImmediate(_twinGo);
                _twinGo = null;
            }

            if (_receiverGo != null)
            {
                Object.DestroyImmediate(_receiverGo);
                _receiverGo = null;
            }

            base.TearDown();
        }

        [UnityTest]
        public IEnumerator VirtualMouseDrag_MovesInputSystemDraggable_ToReceiver()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>("URDT_TestMouse");
            UrdtTestReceiver receiver = CreateReceiver();
            UrdtTestDraggable draggable = CreateDraggable(receiver);

            yield return DriveDrag(mouse);

            Assert.IsTrue(
                draggable.ReachedReceiver,
                "Device-level virtual mouse input must drive the Input-System draggable to the receiver.");
            Assert.IsFalse(draggable.IsDragging, "Drag must be finished after button release.");
        }

        [UnityTest]
        public IEnumerator VirtualMouseDrag_DoesNotMoveLegacyPollingTwin()
        {
            Mouse mouse = InputSystem.AddDevice<Mouse>("URDT_TestMouse");
            UrdtTestReceiver receiver = CreateReceiver();
            UrdtLegacyDraggableTwin twin = CreateLegacyTwin(receiver);
            Vector2 initialPosition = twin.ScreenPosition;

            yield return DriveDrag(mouse);

            Assert.AreEqual(
                initialPosition,
                twin.ScreenPosition,
                "Legacy Input.* polling must NOT see virtual-device input: the twin must stay put.");
            Assert.IsFalse(
                twin.ReachedReceiver,
                "The legacy twin must never reach the receiver under device-level injection.");
        }

        private IEnumerator DriveDrag(Mouse mouse)
        {
            Vector2 start = new Vector2(START_X, START_Y);
            Vector2 target = new Vector2(TARGET_X, TARGET_Y);

            Set(mouse.position, start);
            yield return null;

            Press(mouse.leftButton);
            yield return null;

            for (int step = 1; step <= DRAG_STEPS; step++)
            {
                Vector2 point = Vector2.Lerp(start, target, (float)step / DRAG_STEPS);
                Set(mouse.position, point);
                yield return null;
            }

            Release(mouse.leftButton);
            yield return null;
            yield return null;
        }

        private UrdtTestReceiver CreateReceiver()
        {
            _receiverGo = new GameObject("UrdtTestReceiver");
            UrdtTestReceiver receiver = _receiverGo.AddComponent<UrdtTestReceiver>();
            receiver.ScreenPosition = new Vector2(TARGET_X, TARGET_Y);
            receiver.AcceptRadius = 40f;
            return receiver;
        }

        private UrdtTestDraggable CreateDraggable(UrdtTestReceiver receiver)
        {
            _draggableGo = new GameObject("UrdtTestDraggable");
            UrdtTestDraggable draggable = _draggableGo.AddComponent<UrdtTestDraggable>();
            draggable.ScreenPosition = new Vector2(START_X, START_Y);
            draggable.GrabRadius = 60f;
            draggable.Receiver = receiver;
            return draggable;
        }

        private UrdtLegacyDraggableTwin CreateLegacyTwin(UrdtTestReceiver receiver)
        {
            _twinGo = new GameObject("UrdtLegacyDraggableTwin");
            UrdtLegacyDraggableTwin twin = _twinGo.AddComponent<UrdtLegacyDraggableTwin>();
            twin.ScreenPosition = new Vector2(START_X, START_Y);
            twin.GrabRadius = 60f;
            twin.Receiver = receiver;
            return twin;
        }
    }
}
