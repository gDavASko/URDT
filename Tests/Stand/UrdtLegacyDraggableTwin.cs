using UnityEngine;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Negative-control twin of <see cref="UrdtTestDraggable"/> that polls the
    /// LEGACY Input Manager (<see cref="Input.mousePosition"/> /
    /// <see cref="Input.GetMouseButton(int)"/>). Virtual Input System devices
    /// must NOT be able to move it — there is no bridge from the Input System
    /// to the legacy pipeline. If this twin ever moves under device-level
    /// injection, the honest-input premise (invariant I1) is broken.
    /// </summary>
    public sealed class UrdtLegacyDraggableTwin : MonoBehaviour
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
            Vector2 pointerPosition = UnityEngine.Input.mousePosition;
            bool isPressed = UnityEngine.Input.GetMouseButton(0);

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
