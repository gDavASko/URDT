using System;
using System.Collections.Generic;
using KBP.URDT.Driver;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.InputSystem.UI;

namespace KBP.URDT.Input
{
    /// <summary>
    /// Honest device-level input simulation (invariant I1): feeds state events to
    /// virtual Input System devices via <see cref="InputSystem.QueueStateEvent{T}"/>.
    /// Never invokes UI callbacks directly and never injects through the EventSystem -
    /// uGUI receives the input naturally through InputSystemUIInputModule reading
    /// the virtual devices. Legacy Input.* polling is NOT fed (no bridge exists).
    /// </summary>
    public sealed class InputSimulator : IDisposable
    {
        private readonly Queue<PointerFrame> _pendingPointerFrames = new Queue<PointerFrame>(32);
        private readonly List<InputDevice> _deviceBuffer = new List<InputDevice>(8);

        private Mouse _mouse;
        private Touchscreen _touchscreen;
        private Keyboard _keyboard;
        private bool _leftPressed;
        private Vector2 _mousePosition;

        public InputSimulator()
        {
            _mouse = InputSystem.AddDevice<Mouse>("URDT_VirtualMouse");
            _touchscreen = InputSystem.AddDevice<Touchscreen>("URDT_VirtualTouch");
            _keyboard = InputSystem.AddDevice<Keyboard>("URDT_VirtualKeyboard");
        }

        public Mouse VirtualMouse
        {
            get { return _mouse; }
        }

        public Touchscreen VirtualTouchscreen
        {
            get { return _touchscreen; }
        }

        public Keyboard VirtualKeyboard
        {
            get { return _keyboard; }
        }

        public event Action<PointerInputEvent> PointerInjected;

        public int PendingPointerFrameCount
        {
            get { return _pendingPointerFrames.Count; }
        }

        public void BindVirtualDevicesToUiModules()
        {
            InputSystemUIInputModule[] modules =
                UnityEngine.Object.FindObjectsByType<InputSystemUIInputModule>(FindObjectsSortMode.None);
            for (int i = 0; i < modules.Length; i++)
            {
                InputSystemUIInputModule module = modules[i];
                if (module != null && module.isActiveAndEnabled && module.actionsAsset != null)
                {
                    BindVirtualDevices(module.actionsAsset);
                }
            }
        }

        /// <summary>
        /// Injects a pointer state change at the given screen position (pixels,
        /// bottom-left origin). Pointer id 0 drives the virtual mouse (left button);
        /// positive ids drive individual touches of the virtual touchscreen.
        /// </summary>
        public void InjectPointer(Vector2 screenPos, PointerPhase phase, int pointerId = 0)
        {
            if (pointerId == 0)
            {
                QueueMouseState(screenPos, phase);
            }
            else
            {
                QueueTouchState(screenPos, phase, pointerId);
            }

            Action<PointerInputEvent> handler = PointerInjected;
            if (handler != null)
            {
                handler(new PointerInputEvent(screenPos, phase, pointerId, Time.frameCount));
            }
        }

        /// <summary>
        /// Schedules a click as frame-separated pointer states. The host pumps one step
        /// per Unity frame so game code observes Move/Down/Up through the player loop.
        /// </summary>
        public int ScheduleClick(Vector2 screenPos, int holdFrames = 0, int pointerId = 0)
        {
            int clampedHoldFrames = Mathf.Max(0, holdFrames);
            if (pointerId > 0)
            {
                EnqueuePointerStep(screenPos, PointerPhase.Down, pointerId);
                for (int i = 0; i < clampedHoldFrames; i++)
                {
                    EnqueuePointerStep(screenPos, PointerPhase.Move, pointerId);
                }

                EnqueuePointerStep(screenPos, PointerPhase.Up, pointerId);
                return 2 + clampedHoldFrames;
            }

            EnqueuePointerStep(screenPos, PointerPhase.Move, pointerId);
            // Let the EventSystem establish hover/enter state before PointerDown.
            // Some Selectable implementations, notably TMP_Dropdown, depend on this
            // transition occurring on a separate input frame.
            EnqueuePointerStep(screenPos, PointerPhase.Move, pointerId);
            EnqueuePointerStep(screenPos, PointerPhase.Down, pointerId);

            for (int i = 0; i < clampedHoldFrames; i++)
            {
                EnqueuePointerStep(screenPos, PointerPhase.Move, pointerId);
            }

            EnqueuePointerStep(screenPos, PointerPhase.Up, pointerId);
            return 4 + clampedHoldFrames;
        }

        /// <summary>
        /// Schedules a drag path as frame-separated pointer states.
        /// </summary>
        public int ScheduleDrag(IReadOnlyList<Vector2> points, int stepsPerSegment, int pointerId = 0)
        {
            if (points == null || points.Count < 2)
            {
                return 0;
            }

            int clampedSteps = Mathf.Max(1, stepsPerSegment);
            int queued = 0;
            Vector2 first = points[0];
            EnqueuePointerStep(first, PointerPhase.Move, pointerId);
            EnqueuePointerStep(first, PointerPhase.Down, pointerId);
            queued += 2;

            for (int segment = 1; segment < points.Count; segment++)
            {
                Vector2 from = points[segment - 1];
                Vector2 to = points[segment];
                for (int i = 1; i <= clampedSteps; i++)
                {
                    Vector2 point = Vector2.Lerp(from, to, (float)i / clampedSteps);
                    EnqueuePointerStep(point, PointerPhase.Move, pointerId);
                    queued++;
                }
            }

            EnqueuePointerStep(points[points.Count - 1], PointerPhase.Up, pointerId);
            return queued + 1;
        }

        /// <summary>
        /// Schedules simultaneous clicks with pointer ids 1..N for multitouch input.
        /// </summary>
        public int ScheduleMultiClick(IReadOnlyList<Vector2> points, int holdFrames = 0)
        {
            if (points == null || points.Count == 0)
            {
                return 0;
            }

            EnqueueMultiPointerFrame(points, PointerPhase.Move);
            EnqueueMultiPointerFrame(points, PointerPhase.Down);

            int clampedHoldFrames = Mathf.Max(0, holdFrames);
            for (int i = 0; i < clampedHoldFrames; i++)
            {
                EnqueueMultiPointerFrame(points, PointerPhase.Move);
            }

            EnqueueMultiPointerFrame(points, PointerPhase.Up);
            return 3 + clampedHoldFrames;
        }

        /// <summary>
        /// Schedules several pointer paths in the same frame windows.
        /// </summary>
        public int ScheduleMultiDrag(IReadOnlyList<IReadOnlyList<Vector2>> paths, int stepsPerSegment)
        {
            if (paths == null || paths.Count == 0)
            {
                return 0;
            }

            int clampedSteps = Mathf.Max(1, stepsPerSegment);
            int longestSegments = 0;
            for (int i = 0; i < paths.Count; i++)
            {
                if (paths[i] == null || paths[i].Count < 2)
                {
                    return 0;
                }

                int segments = paths[i].Count - 1;
                if (segments > longestSegments)
                {
                    longestSegments = segments;
                }
            }

            EnqueueMultiPathFrame(paths, 0, 0f, PointerPhase.Move);
            EnqueueMultiPathFrame(paths, 0, 0f, PointerPhase.Down);
            int queued = 2;

            for (int segment = 0; segment < longestSegments; segment++)
            {
                for (int step = 1; step <= clampedSteps; step++)
                {
                    EnqueueMultiPathFrame(paths, segment, (float)step / clampedSteps, PointerPhase.Move);
                    queued++;
                }
            }

            EnqueueMultiPathEndFrame(paths);
            return queued + 1;
        }

        /// <summary>
        /// Schedules mouse-wheel style scroll at a screen position.
        /// </summary>
        public int ScheduleScroll(Vector2 screenPos, Vector2 scrollDelta)
        {
            // Force a real pointer exit before re-entering the target. A small jitter can
            // remain within the same nested control and leave PointerEventData.pointerEnter stale.
            EnqueuePointerStep(Vector2.zero, PointerPhase.Move, 0);
            EnqueuePointerStep(screenPos, PointerPhase.Move, 0);
            PointerFrame frame = new PointerFrame();
            frame.ScrollDelta = scrollDelta;
            frame.HasScroll = true;
            _pendingPointerFrames.Enqueue(frame);

            // Wheel deltas are transient. Resetting them on the following frame ensures
            // two identical scroll commands produce two distinct Input System events.
            PointerFrame resetFrame = new PointerFrame();
            resetFrame.ScrollDelta = Vector2.zero;
            resetFrame.HasScroll = true;
            _pendingPointerFrames.Enqueue(resetFrame);
            return 4;
        }

        /// <summary>
        /// Schedules a frame-separated keyboard key press through the virtual keyboard.
        /// </summary>
        public int ScheduleKeyPress(Key key, int holdFrames = 0)
        {
            EnqueueKeyStep(key, true);

            int clampedHoldFrames = Mathf.Max(0, holdFrames);
            for (int i = 0; i < clampedHoldFrames; i++)
            {
                EnqueueKeyStep(key, true);
            }

            EnqueueKeyStep(key, false);
            return 2 + clampedHoldFrames;
        }

        /// <summary>Queues Unicode text events through the virtual keyboard.</summary>
        public int ScheduleText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return 0;
            }

            for (int i = 0; i < text.Length; i++)
            {
                PointerFrame frame = new PointerFrame();
                frame.TextCharacter = text[i];
                frame.HasText = true;
                _pendingPointerFrames.Enqueue(frame);
            }

            return text.Length;
        }

        public bool PumpQueuedPointerStep()
        {
            if (_pendingPointerFrames.Count == 0)
            {
                return false;
            }

            PointerFrame frame = _pendingPointerFrames.Dequeue();
            for (int i = 0; i < frame.Steps.Count; i++)
            {
                PointerStep step = frame.Steps[i];
                InjectPointer(step.ScreenPos, step.Phase, step.PointerId);
            }

            if (frame.HasScroll)
            {
                QueueMouseScroll(frame.ScrollDelta);
            }

            if (frame.HasKey)
            {
                QueueKeyboardState(frame.Key, frame.KeyPressed);
            }

            if (frame.HasText)
            {
                QueueText(frame.TextCharacter);
            }

            return true;
        }

        public void ClearPendingPointerSteps()
        {
            _pendingPointerFrames.Clear();
        }

        public void Dispose()
        {
            ClearPendingPointerSteps();

            if (_mouse != null)
            {
                InputSystem.RemoveDevice(_mouse);
                _mouse = null;
            }

            if (_touchscreen != null)
            {
                InputSystem.RemoveDevice(_touchscreen);
                _touchscreen = null;
            }

            if (_keyboard != null)
            {
                InputSystem.RemoveDevice(_keyboard);
                _keyboard = null;
            }
        }

        private void QueueMouseState(Vector2 screenPos, PointerPhase phase)
        {
            BindVirtualDevicesToUiModules();
            _mousePosition = screenPos;

            if (phase == PointerPhase.Down)
            {
                _leftPressed = true;
            }
            else if (phase == PointerPhase.Up)
            {
                _leftPressed = false;
            }

            MouseState state = new MouseState { position = screenPos };
            state = state.WithButton(MouseButton.Left, _leftPressed);
            _mouse.MakeCurrent();
            InputSystem.QueueStateEvent(_mouse, state);
        }

        private void QueueMouseScroll(Vector2 scrollDelta)
        {
            BindVirtualDevicesToUiModules();
            _mouse.MakeCurrent();
            InputSystem.QueueDeltaStateEvent(_mouse.scroll, scrollDelta);
        }

        private void QueueTouchState(Vector2 screenPos, PointerPhase phase, int touchId)
        {
            BindVirtualDevicesToUiModules();
            TouchState state = new TouchState
            {
                touchId = touchId,
                position = screenPos,
                phase = ToTouchPhase(phase)
            };
            _touchscreen.MakeCurrent();
            InputSystem.QueueStateEvent(_touchscreen, state);
        }

        private void QueueKeyboardState(Key key, bool pressed)
        {
            BindVirtualDevicesToUiModules();
            KeyboardState state = pressed ? new KeyboardState(key) : new KeyboardState();
            _keyboard.MakeCurrent();
            InputSystem.QueueStateEvent(_keyboard, state);
        }

        private void QueueText(char character)
        {
            BindVirtualDevicesToUiModules();
            _keyboard.MakeCurrent();
            InputSystem.QueueTextEvent(_keyboard, character);
        }

        private void BindVirtualDevices(InputActionAsset actionsAsset)
        {
            _deviceBuffer.Clear();
            if (actionsAsset.devices.HasValue)
            {
                var devices = actionsAsset.devices.Value;
                for (int i = 0; i < devices.Count; i++)
                {
                    InputDevice device = devices[i];
                    if (device != null && !_deviceBuffer.Contains(device))
                    {
                        _deviceBuffer.Add(device);
                    }
                }
            }
            else
            {
                for (int i = 0; i < InputSystem.devices.Count; i++)
                {
                    InputDevice device = InputSystem.devices[i];
                    if (device != null && !_deviceBuffer.Contains(device))
                    {
                        _deviceBuffer.Add(device);
                    }
                }
            }

            if (_mouse != null && !_deviceBuffer.Contains(_mouse))
            {
                _deviceBuffer.Add(_mouse);
            }

            if (_touchscreen != null && !_deviceBuffer.Contains(_touchscreen))
            {
                _deviceBuffer.Add(_touchscreen);
            }

            if (_keyboard != null && !_deviceBuffer.Contains(_keyboard))
            {
                _deviceBuffer.Add(_keyboard);
            }

            actionsAsset.devices = _deviceBuffer.ToArray();
            _deviceBuffer.Clear();
        }

        private static UnityEngine.InputSystem.TouchPhase ToTouchPhase(PointerPhase phase)
        {
            switch (phase)
            {
                case PointerPhase.Down:
                    return UnityEngine.InputSystem.TouchPhase.Began;
                case PointerPhase.Up:
                    return UnityEngine.InputSystem.TouchPhase.Ended;
                default:
                    return UnityEngine.InputSystem.TouchPhase.Moved;
            }
        }

        private void EnqueuePointerStep(Vector2 screenPos, PointerPhase phase, int pointerId)
        {
            PointerFrame frame = new PointerFrame();
            frame.Steps.Add(new PointerStep(screenPos, phase, pointerId));
            _pendingPointerFrames.Enqueue(frame);
        }

        private void EnqueueKeyStep(Key key, bool pressed)
        {
            PointerFrame frame = new PointerFrame();
            frame.Key = key;
            frame.KeyPressed = pressed;
            frame.HasKey = true;
            _pendingPointerFrames.Enqueue(frame);
        }

        private void EnqueueMultiPointerFrame(IReadOnlyList<Vector2> points, PointerPhase phase)
        {
            PointerFrame frame = new PointerFrame();
            for (int i = 0; i < points.Count; i++)
            {
                frame.Steps.Add(new PointerStep(points[i], phase, i + 1));
            }

            _pendingPointerFrames.Enqueue(frame);
        }

        private void EnqueueMultiPathFrame(
            IReadOnlyList<IReadOnlyList<Vector2>> paths,
            int segment,
            float t,
            PointerPhase phase)
        {
            PointerFrame frame = new PointerFrame();
            for (int i = 0; i < paths.Count; i++)
            {
                IReadOnlyList<Vector2> path = paths[i];
                int clampedSegment = Mathf.Min(segment, path.Count - 2);
                Vector2 point = Vector2.Lerp(path[clampedSegment], path[clampedSegment + 1], t);
                frame.Steps.Add(new PointerStep(point, phase, i + 1));
            }

            _pendingPointerFrames.Enqueue(frame);
        }

        private void EnqueueMultiPathEndFrame(IReadOnlyList<IReadOnlyList<Vector2>> paths)
        {
            PointerFrame frame = new PointerFrame();
            for (int i = 0; i < paths.Count; i++)
            {
                IReadOnlyList<Vector2> path = paths[i];
                frame.Steps.Add(new PointerStep(path[path.Count - 1], PointerPhase.Up, i + 1));
            }

            _pendingPointerFrames.Enqueue(frame);
        }

        private sealed class PointerFrame
        {
            public readonly List<PointerStep> Steps = new List<PointerStep>(4);
            public Vector2 ScrollDelta;
            public bool HasScroll;
            public Key Key;
            public bool KeyPressed;
            public bool HasKey;
            public char TextCharacter;
            public bool HasText;
        }

        private readonly struct PointerStep
        {
            public readonly Vector2 ScreenPos;
            public readonly PointerPhase Phase;
            public readonly int PointerId;

            public PointerStep(Vector2 screenPos, PointerPhase phase, int pointerId)
            {
                ScreenPos = screenPos;
                Phase = phase;
                PointerId = pointerId;
            }
        }
    }

    public readonly struct PointerInputEvent
    {
        public readonly Vector2 ScreenPos;
        public readonly PointerPhase Phase;
        public readonly int PointerId;
        public readonly int Frame;

        public PointerInputEvent(Vector2 screenPos, PointerPhase phase, int pointerId, int frame)
        {
            ScreenPos = screenPos;
            Phase = phase;
            PointerId = pointerId;
            Frame = frame;
        }
    }
}
