using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M07_WobbleAndSnap
{
    /// <summary>
    /// Инструмент: Стоматологические хирургические щипцы для удаления зуба (M07).
    /// Этап 1: Инструмент лежит на столике/лотке. Игрок перетаскивает щипцы к зубу.
    /// При отпускании над коронкой зуба щечки щипцов смыкаются на шейке/коронке зуба (Snap-on).
    /// Этап 2: Игрок раскачивает щипцы влево-вправо, ослабляя связку зуба, затем тянет вверх.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class DentalForcepsTool : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Связанный зуб")]
        [SerializeField] private WobbleItem _targetTooth = null;
        [SerializeField] private Vector2 _homeAnchoredPosition = new Vector2(230f, 0f);
        [SerializeField] private Vector2 _gripOffsetOnTooth = new Vector2(0f, 65f);
        [SerializeField] private float _snapAttachDistance = 110f;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Transform _originalParent;
        private bool _isAttached = false;
        private bool _isDragging = false;

        public bool IsAttached => _isAttached;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action OnAttached;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalParent = transform.parent;
            EnsureCanvas();
        }

        private void EnsureCanvas()
        {
            if (_parentCanvas == null)
            {
                _parentCanvas = GetComponentInParent<Canvas>();
                if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
        }

        public void SetTargetTooth(WobbleItem tooth)
        {
            _targetTooth = tooth;
        }

        public void ResetTool()
        {
            _isAttached = false;
            _isDragging = false;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();

            if (_originalParent != null && transform.parent != _originalParent)
            {
                transform.SetParent(_originalParent, false);
            }

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _homeAnchoredPosition;
                _rectTransform.localRotation = Quaternion.identity;
                _rectTransform.localScale = Vector3.one;
            }
        }

        private void Update()
        {
            if (!_isAttached && !_isDragging)
            {
                // Плавный возврат на столик при неудачной попытке наложения
                if (_rectTransform != null && Vector2.Distance(_rectTransform.anchoredPosition, _homeAnchoredPosition) > 1f)
                {
                    _rectTransform.anchoredPosition = Vector2.Lerp(_rectTransform.anchoredPosition, _homeAnchoredPosition, Time.unscaledDeltaTime * 14f);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            EnsureCanvas();
            if (_isAttached)
            {
                if (_targetTooth != null) _targetTooth.StartExternalDrag(eventData);
                return;
            }

            _isDragging = true;
            transform.SetAsLastSibling(); // Поверх остальных элементов во время переноса
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            EnsureCanvas();
            if (_isAttached)
            {
                if (_targetTooth != null) _targetTooth.StartExternalDrag(eventData);
                return;
            }
            _isDragging = true;
        }

        public void OnDrag(PointerEventData eventData)
        {
            EnsureCanvas();
            if (_isAttached)
            {
                if (_targetTooth != null) _targetTooth.ProcessExternalDrag(eventData);
                return;
            }

            // Перетаскивание щипцов со столика к зубу
            RectTransform parentRt = transform.parent as RectTransform;
            if (parentRt != null)
            {
                Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parentCanvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRt, eventData.position, cam, out Vector2 localPoint))
                {
                    _rectTransform.anchoredPosition = localPoint;
                }
            }

            // Проверка сближения с зубом
            CheckToothProximity(eventData, isFinalRelease: false);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            EnsureCanvas();
            if (_isAttached)
            {
                if (_targetTooth != null) _targetTooth.EndExternalDrag(eventData);
                return;
            }

            _isDragging = false;
            CheckToothProximity(eventData, isFinalRelease: true);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            EnsureCanvas();
            if (_isAttached)
            {
                if (_targetTooth != null) _targetTooth.EndExternalDrag(eventData);
                return;
            }

            _isDragging = false;
            CheckToothProximity(eventData, isFinalRelease: true);
        }

        private void CheckToothProximity(PointerEventData eventData, bool isFinalRelease)
        {
            if (_isAttached || _targetTooth == null) return;

            // Нижняя часть щипцов (щечки/бранши)
            Vector3 beaksWorldPos = _rectTransform.TransformPoint(new Vector3(0f, -80f, 0f));
            Vector3 toothWorldPos = _targetTooth.transform.position;

            Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay ? _parentCanvas.worldCamera : null;

            Vector2 beaksScreen = RectTransformUtility.WorldToScreenPoint(cam, beaksWorldPos);
            Vector2 toothScreen = RectTransformUtility.WorldToScreenPoint(cam, toothWorldPos);

            float screenDist = Vector2.Distance(beaksScreen, toothScreen);

            // Попадание курсора или щечек в область коронки зуба
            bool cursorOverTooth = RectTransformUtility.RectangleContainsScreenPoint(_targetTooth.RectTransform, eventData.position, cam);

            if (screenDist <= _snapAttachDistance || (isFinalRelease && cursorOverTooth))
            {
                AttachToTooth();
            }
        }

        private void AttachToTooth()
        {
            if (_isAttached || _targetTooth == null) return;

            _isAttached = true;
            _isDragging = false;

            // Фиксация щипцов на коронке зуба в качестве дочернего объекта
            transform.SetParent(_targetTooth.transform, false);
            _rectTransform.anchoredPosition = _gripOffsetOnTooth;
            _rectTransform.localRotation = Quaternion.identity;
            _rectTransform.localScale = Vector3.one;

            _targetTooth.IsForcepsAttached = true;
            OnAttached?.Invoke();
        }
    }
}
