using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Virtual On-Screen Joystick / Stick component for uGUI.
    /// Drives normalized Vector2 input from pointer drag.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtVirtualStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [Header("Stick Geometry")]
        [SerializeField] private RectTransform _background;
        [SerializeField] private RectTransform _handle;
        [SerializeField] private float _handleRange = 45f;
        [SerializeField] private float _deadZone = 0.05f;

        private Vector2 _inputVector = Vector2.zero;
        private bool _isPressed = false;
        private Canvas _canvas;

        public event Action<Vector2> OnValueChanged;

        public Vector2 InputVector
        {
            get { return _inputVector; }
        }

        public float Magnitude
        {
            get { return _inputVector.magnitude; }
        }

        public bool IsPressed
        {
            get { return _isPressed; }
        }

        public RectTransform Handle
        {
            get { return _handle; }
        }

        public RectTransform Background
        {
            get { return _background; }
        }

        public float HandleRange
        {
            get { return _handleRange; }
        }

        private void Awake()
        {
            if (_background == null)
            {
                _background = GetComponent<RectTransform>();
            }
            _canvas = GetComponentInParent<Canvas>();
        }

        public void Configure(RectTransform background, RectTransform handle, float handleRange = 45f)
        {
            _background = background;
            _handle = handle;
            _handleRange = handleRange;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isPressed = true;
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_background == null || _handle == null)
            {
                return;
            }

            Camera cam = null;
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                cam = _canvas.worldCamera;
            }

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_background, eventData.position, cam, out localPoint))
            {
                Vector2 clamped = Vector2.ClampMagnitude(localPoint, _handleRange);
                _handle.anchoredPosition = clamped;

                Vector2 raw = clamped / _handleRange;
                if (raw.magnitude < _deadZone)
                {
                    _inputVector = Vector2.zero;
                }
                else
                {
                    _inputVector = raw;
                }

                Action<Vector2> handler = OnValueChanged;
                if (handler != null)
                {
                    handler(_inputVector);
                }
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isPressed = false;
            _inputVector = Vector2.zero;
            if (_handle != null)
            {
                _handle.anchoredPosition = Vector2.zero;
            }

            Action<Vector2> handler = OnValueChanged;
            if (handler != null)
            {
                handler(Vector2.zero);
            }
        }
    }
}
