using UnityEngine;
using UnityEngine.InputSystem;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Draggable of the M0 test stand driven by the NEW Input System
    /// (<see cref="Pointer.current"/>). Owns its hit logic (press within
    /// <see cref="GrabRadius"/> of the current screen position starts a drag),
    /// which is exactly the invariant I2 setup: URDT injects device input,
    /// the "game" decides what a hit means.
    /// </summary>
    public sealed class UrdtTestDraggable : MonoBehaviour
    {
        private Vector2 _screenPosition;
        private float _grabRadius = 60f;
        private UrdtTestReceiver _receiver;
        private bool _isDragging;
        private bool _wasPressed;
        private bool _reachedReceiver;

        public Vector2 ScreenPosition
        {
            get { return _screenPosition; }
            set { _screenPosition = value; }
        }

        public float GrabRadius
        {
            get { return _grabRadius; }
            set { _grabRadius = value; }
        }

        public UrdtTestReceiver Receiver
        {
            get { return _receiver; }
            set { _receiver = value; }
        }

        public bool IsDragging
        {
            get { return _isDragging; }
        }

        public bool ReachedReceiver
        {
            get { return _reachedReceiver; }
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            Vector2 pointerPosition = pointer.position.ReadValue();
            bool isPressed = pointer.press.isPressed;

            if (!_isDragging && isPressed && !_wasPressed && IsInsideGrabRadius(pointerPosition))
            {
                _isDragging = true;
            }

            if (_isDragging)
            {
                if (isPressed)
                {
                    _screenPosition = pointerPosition;
                }
                else
                {
                    _isDragging = false;

                    if (_receiver != null && _receiver.Accepts(_screenPosition))
                    {
                        _reachedReceiver = true;
                    }
                }
            }

            _wasPressed = isPressed;
        }

        private bool IsInsideGrabRadius(Vector2 screenPoint)
        {
            return (screenPoint - _screenPosition).sqrMagnitude <= _grabRadius * _grabRadius;
        }
    }
}
