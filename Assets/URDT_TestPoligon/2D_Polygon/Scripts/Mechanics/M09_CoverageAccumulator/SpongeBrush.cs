using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M09_CoverageAccumulator
{
    /// <summary>
    /// Инструмент-губка для стирания пятен грязи непрерывным круговым движением.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SpongeBrush : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private Vector2 _homePosition = new Vector2(230f, 0f);
        [SerializeField] private float _brushRadius = 40f;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private bool _isDragging;

        public float BrushRadius => _brushRadius;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<Vector2> OnBrushMoved;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            ResetBrush();
        }

        public void ResetBrush()
        {
            _isDragging = false;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _homePosition;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            _isDragging = true;
            transform.SetAsLastSibling();
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
                OnBrushMoved?.Invoke(_rectTransform.position);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }
    }
}
