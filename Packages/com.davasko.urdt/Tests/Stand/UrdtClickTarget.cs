using KBP.URDT.Inspect;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KBP.URDT.Tests.Stand
{
    /// <summary>
    /// Clickable target of the M1 test stand. It owns its own hit logic
    /// (invariant I2): on a fresh press of the new Input System pointer within
    /// <see cref="HitRadius"/> of its screen position it increments
    /// <see cref="ClickCount"/>. The counter is exposed to URDT `inspect` via
    /// <see cref="TestInspectableAttribute"/>, proving the whitelist reflection path.
    /// </summary>
    public sealed class UrdtClickTarget : MonoBehaviour
    {
        [TestInspectable]
        private int _clickCount;

        private Vector2 _screenPosition;
        private float _hitRadius = 40f;
        private bool _wasPressed;

        public Vector2 ScreenPosition
        {
            get { return _screenPosition; }
            set { _screenPosition = value; }
        }

        public float HitRadius
        {
            get { return _hitRadius; }
            set { _hitRadius = value; }
        }

        [TestInspectable]
        public int ClickCount
        {
            get { return _clickCount; }
        }

        private void Update()
        {
            Pointer pointer = Pointer.current;
            if (pointer == null)
            {
                return;
            }

            bool isPressed = pointer.press.isPressed;

            if (isPressed && !_wasPressed)
            {
                Vector2 pointerPosition = pointer.position.ReadValue();
                if (IsInsideHitRadius(pointerPosition))
                {
                    _clickCount++;
                }
            }

            _wasPressed = isPressed;
        }

        private bool IsInsideHitRadius(Vector2 screenPoint)
        {
            return (screenPoint - _screenPosition).sqrMagnitude <= _hitRadius * _hitRadius;
        }
    }
}
