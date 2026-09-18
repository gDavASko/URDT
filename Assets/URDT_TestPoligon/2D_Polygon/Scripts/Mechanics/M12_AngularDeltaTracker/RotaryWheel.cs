using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M12_AngularDeltaTracker
{
    /// <summary>
    /// Интерактивное колесо/вентиль, отслеживающее угловую дельту вращения (Angular Delta).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class RotaryWheel : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private bool _isJammed = false;
        [SerializeField] private RectTransform _wheelVisual = null;

        public event Action<float> OnDeltaRotated;
        public event Action OnJammedAttempt;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private float _previousPointerAngle;
        private bool _isDragging;
        private float _accumulatedAngle;

        public bool IsJammed => _isJammed;
        public float AccumulatedAngle => _accumulatedAngle;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            if (_wheelVisual == null) _wheelVisual = _rectTransform;
        }

        public void ResetWheel()
        {
            _accumulatedAngle = 0f;
            _isDragging = false;
            if (_wheelVisual == null) _wheelVisual = GetComponent<RectTransform>();
            if (_wheelVisual != null)
            {
                _wheelVisual.localRotation = Quaternion.identity;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isJammed)
            {
                OnJammedAttempt?.Invoke();
                Jiggle();
                return;
            }

            _isDragging = true;
            _previousPointerAngle = GetPointerAngle(eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging || _isJammed) return;

            float currentPointerAngle = GetPointerAngle(eventData.position);
            float delta = Mathf.DeltaAngle(_previousPointerAngle, currentPointerAngle);

            // Фильтрация резких скачков
            if (Mathf.Abs(delta) < 90f)
            {
                _accumulatedAngle += delta;
                if (_wheelVisual != null)
                {
                    _wheelVisual.localRotation = Quaternion.Euler(0f, 0f, _accumulatedAngle);
                }
                OnDeltaRotated?.Invoke(delta);
            }

            _previousPointerAngle = currentPointerAngle;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _isDragging = false;
        }

        private float GetPointerAngle(Vector2 screenPos)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            RectTransform parentRect = RectTransform.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, screenPos, cam, out Vector2 pointerInParent))
            {
                Vector2 centerInParent = (Vector2)RectTransform.localPosition;
                Vector2 dir = pointerInParent - centerInParent;
                return Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
            }

            Vector2 screenCenter = RectTransformUtility.WorldToScreenPoint(cam, RectTransform.position);
            Vector2 screenDir = screenPos - screenCenter;
            return Mathf.Atan2(screenDir.y, screenDir.x) * Mathf.Rad2Deg;
        }

        private void Jiggle()
        {
            // Легкое дрожание заклинившего колеса
            if (_wheelVisual != null)
            {
                _wheelVisual.localRotation = Quaternion.Euler(0f, 0f, UnityEngine.Random.Range(-5f, 5f));
            }
        }
    }
}
