using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M08_WaypointTracking
{
    /// <summary>
    /// Инструмент для непрерывного ведения по контрольным точкам траектории (Waypoint Tracking).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class WaypointTrackerTool : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        [Header("Параметры инструмента")]
        [SerializeField] private float _reachRadius = 45f;
        [SerializeField] private Vector2 _startPosition = new Vector2(-220f, 0f);

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private bool _isDragging;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<Vector2> OnPositionMoved;
        public event Action OnDragCancelled;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            ResetTool();
        }

        public void ResetTool()
        {
            _isDragging = false;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _startPosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_isDragging) return;

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_parentCanvas == null)
            {
                _parentCanvas = GetComponentInParent<Canvas>();
                if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }

            Vector2 localPoint;
            Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parentCanvas.worldCamera : null;
            RectTransform parentRt = _rectTransform != null ? _rectTransform.parent as RectTransform : null;
            if (parentRt != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRt,
                    eventData.position,
                    cam,
                    out localPoint))
            {
                _rectTransform.anchoredPosition = localPoint;
                OnPositionMoved?.Invoke(_rectTransform.anchoredPosition);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
            OnDragCancelled?.Invoke();
        }
    }
}
