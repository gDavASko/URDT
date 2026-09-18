using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M07_WobbleAndSnap
{
    /// <summary>
    /// Объект на виртуальной пружине с накоплением усталости при расшатывании (Wobble & Snap).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class WobbleItem : MonoBehaviour, IPointerDownHandler, IDragHandler, IEndDragHandler
    {
        [Header("Параметры пружины")]
        [SerializeField] private float _springStiffness = 0.25f;
        [SerializeField] private float _maxTensionDistance = 120f;
        [SerializeField] private float _fatigueRate = 1.4f;
        [SerializeField] private float _snapReleaseThreshold = 80f;
        [SerializeField] private bool _requiresForceps = true;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector2 _anchorPosition;
        private float _fatigue = 0f;
        private float _previousAngle = 0f;
        private bool _isDragging = false;
        private bool _isSnapped = false;
        private bool _isForcepsAttached = false;

        public float Fatigue => _fatigue;
        public bool IsSnapped => _isSnapped;
        public bool RequiresForceps { get => _requiresForceps; set => _requiresForceps = value; }
        public bool IsForcepsAttached { get => _isForcepsAttached; set => _isForcepsAttached = value; }
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<float> OnFatigueChanged;
        public event Action OnSnapped;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            _anchorPosition = _rectTransform.anchoredPosition;
        }

        public void ResetItem()
        {
            _fatigue = 0f;
            _isDragging = false;
            _isSnapped = false;
            _isForcepsAttached = false;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null)
            {
                if (_anchorPosition == Vector2.zero) _anchorPosition = _rectTransform.anchoredPosition;
                _rectTransform.anchoredPosition = _anchorPosition;
                _rectTransform.localRotation = Quaternion.identity;
            }
            OnFatigueChanged?.Invoke(0f);
        }

        private void Update()
        {
            if (_isSnapped) return;

            if (!_isDragging)
            {
                // Возврат пружиной в анкерную точку
                _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, _anchorPosition, Time.unscaledDeltaTime * 12f);
                _rectTransform.localRotation = Quaternion.Lerp(_rectTransform.localRotation, Quaternion.identity, Time.unscaledDeltaTime * 12f);
            }
        }

        public void StartExternalDrag(PointerEventData eventData)
        {
            if (_isSnapped) return;
            _isDragging = true;
            Vector2 dir = GetLocalPoint(eventData.position) - _anchorPosition;
            _previousAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        public void ProcessExternalDrag(PointerEventData eventData)
        {
            if (_isSnapped) return;
            OnDrag(eventData);
        }

        public void EndExternalDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isSnapped) return;
            if (_requiresForceps && !_isForcepsAttached) return; // Нельзя расшатывать зуб пальцами без щипцов!
            _isDragging = true;
            Vector2 dir = GetLocalPoint(eventData.position) - _anchorPosition;
            _previousAngle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isSnapped) return;
            if (_requiresForceps && !_isForcepsAttached) return;

            Vector2 pointerPos = GetLocalPoint(eventData.position);
            Vector2 offset = pointerPos - _anchorPosition;

            // Ограничение смещения пружины
            float currentDist = offset.magnitude;
            Vector2 clampedOffset = offset.normalized * Mathf.Clamp(currentDist * _springStiffness, 0f, _maxTensionDistance);
            _rectTransform.anchoredPosition = _anchorPosition + clampedOffset;

            // Расчет угла отклонения и накопления усталости
            float currentAngle = Mathf.Atan2(offset.y, offset.x) * Mathf.Rad2Deg;
            float deltaAngle = Mathf.Abs(Mathf.DeltaAngle(_previousAngle, currentAngle));
            _previousAngle = currentAngle;

            // Накопление усталости связки при расшатывании
            if (currentDist > 20f)
            {
                _fatigue += (deltaAngle / 180f) * _fatigueRate;
                _fatigue = Mathf.Clamp01(_fatigue);
                OnFatigueChanged?.Invoke(_fatigue);

                // Наклон спрайта в сторону оттягивания
                float tiltZ = Mathf.Clamp(-offset.x * 0.35f, -25f, 25f);
                _rectTransform.localRotation = Quaternion.Euler(0f, 0f, tiltZ);

                // Если усталость 100% и игрок сильно потянул вверх — отрыв!
                if (_fatigue >= 1f && offset.y > _snapReleaseThreshold)
                {
                    SnapOff();
                }
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _isDragging = false;
        }

        private void SnapOff()
        {
            _isSnapped = true;
            _isDragging = false;
            _rectTransform.anchoredPosition = _anchorPosition + new Vector2(0f, 150f);
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, 35f);
            OnSnapped?.Invoke();
        }

        private Vector2 GetLocalPoint(Vector2 screenPoint)
        {
            if (_canvasRectTransform != null && _parentCanvas != null)
            {
                Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
                Vector2 localPoint;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRectTransform, screenPoint, cam, out localPoint))
                {
                    return localPoint;
                }
            }
            return screenPoint;
        }
    }
}
