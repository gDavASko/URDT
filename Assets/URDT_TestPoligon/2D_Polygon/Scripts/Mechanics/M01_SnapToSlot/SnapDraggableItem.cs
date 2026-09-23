using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M01_SnapToSlot
{
    /// <summary>
    /// Интерактивный перетаскиваемый элемент (деталь) с поддержкой примагничивания к слотам
    /// и возврата на исходную позицию при отпускании в невалидной зоне.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SnapDraggableItem : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Идентификация детали")]
        [SerializeField] private string _itemId = "default_item";
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Image _itemImage = null;

        [Header("Параметры драга")]
        [SerializeField] private float _snapThreshold = 120f;
        [SerializeField] private float _dragScale = 1.12f;
        [SerializeField] private float _returnDuration = 0.22f;
        [SerializeField] private Vector2 _homeAnchoredPosition;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Transform _originalParent;
        private Vector3 _originalScale = Vector3.one;
        private int _originalSiblingIndex;
        private bool _isLocked;
        private Coroutine _activeMoveRoutine;
        private SnapSlot _currentHoveredSlot;

        public string ItemId => _itemId;
        public bool IsJunk => _isJunk;
        /// <summary>Где предмет лежит (или куда едет) в лотке.</summary>
        public Vector2 HomePosition => _homeAnchoredPosition;
        public bool IsLocked => _isLocked;
        public float SnapThreshold => _snapThreshold;

        public void SetItemId(string id) { _itemId = id; }
        public void SetJunk(bool junk) { _isJunk = junk; }
        public void SetSnapThreshold(float t) { _snapThreshold = Mathf.Max(20f, t); }
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<SnapDraggableItem, SnapSlot> OnItemSnapped;
        public event Action<SnapDraggableItem, SnapSlot> OnItemRejected;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalParent = transform.parent;
            if (_rectTransform != null)
            {
                if (_rectTransform.localScale != Vector3.zero)
                {
                    _originalScale = _rectTransform.localScale;
                }
                else
                {
                    _originalScale = Vector3.one;
                    _rectTransform.localScale = Vector3.one;
                }
            }
            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
        }

        public void SetHomePosition(Vector2 homePos)
        {
            _homeAnchoredPosition = homePos;
            if (!_isLocked)
            {
                RectTransform.anchoredPosition = homePos;
                if (RectTransform.localScale == Vector3.zero)
                {
                    RectTransform.localScale = _originalScale != Vector3.zero ? _originalScale : Vector3.one;
                }
            }
        }

        public void ResetItem()
        {
            _isLocked = false;
            if (_activeMoveRoutine != null)
            {
                StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = null;
            }
            if (_originalParent != null && transform.parent != _originalParent)
            {
                transform.SetParent(_originalParent, false);
            }
            if (_originalScale == Vector3.zero)
            {
                _originalScale = Vector3.one;
            }
            RectTransform.localScale = _originalScale;
            RectTransform.anchoredPosition = _homeAnchoredPosition;
            if (_currentHoveredSlot != null)
            {
                _currentHoveredSlot.SetHover(false, false);
                _currentHoveredSlot = null;
            }
        }

        public void LockInSlot(SnapSlot slot)
        {
            _isLocked = true;
            if (_activeMoveRoutine != null) StopCoroutine(_activeMoveRoutine);

            transform.SetParent(slot.transform, true);
            _activeMoveRoutine = StartCoroutine(SmoothMoveRoutine(Vector2.zero, 0.15f, null));
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isLocked) return;
            if (_parentCanvas == null) _parentCanvas = GetComponentInParent<Canvas>();
            if (_canvasRectTransform == null && _parentCanvas != null)
            {
                _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isLocked) return;

            if (_activeMoveRoutine != null)
            {
                StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = null;
            }

            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            RectTransform.localScale = _originalScale * _dragScale;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isLocked) return;

            if (_canvasRectTransform != null && RectTransform.parent is RectTransform parentRect)
            {
                Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _parentCanvas.worldCamera : null;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    parentRect,
                    eventData.position,
                    cam,
                    out Vector2 localPoint))
                {
                    RectTransform.anchoredPosition = localPoint;
                }
            }

            UpdateHoverState();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isLocked) return;

            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            RectTransform.localScale = _originalScale;

            if (_currentHoveredSlot != null)
            {
                _currentHoveredSlot.SetHover(false, false);
                _currentHoveredSlot = null;
            }

            // Поиск ближайшего слота
            SnapSlot bestSlot = FindBestSlot(out float bestDist);

            if (bestSlot != null && bestDist <= _snapThreshold && bestSlot.CanAccept(this))
            {
                bestSlot.AcceptItem(this);
                LockInSlot(bestSlot);
                OnItemSnapped?.Invoke(this, bestSlot);
            }
            else
            {
                // Не совпало или мусор — возврат на исходную
                OnItemRejected?.Invoke(this, bestSlot);
                if (_activeMoveRoutine != null) StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = StartCoroutine(SmoothMoveRoutine(_homeAnchoredPosition, _returnDuration, () =>
                {
                    transform.SetSiblingIndex(_originalSiblingIndex);
                }));
            }
        }

        private void UpdateHoverState()
        {
            SnapSlot slot = FindBestSlot(out float dist);

            if (slot != _currentHoveredSlot)
            {
                if (_currentHoveredSlot != null)
                {
                    _currentHoveredSlot.SetHover(false, false);
                }
                _currentHoveredSlot = slot;
            }

            if (_currentHoveredSlot != null)
            {
                bool isMatch = _currentHoveredSlot.CanAccept(this);
                _currentHoveredSlot.SetHover(true, isMatch);
            }
        }

        private SnapSlot FindBestSlot(out float bestDistance)
        {
            bestDistance = float.MaxValue;
            SnapSlot bestSlot = null;

            SnapSlot[] allSlots = null;
            M01_SnapToSlotMechanic mechanic = GetComponentInParent<M01_SnapToSlotMechanic>();
            if (mechanic != null)
            {
                allSlots = mechanic.Slots;
            }
            if (allSlots == null || allSlots.Length == 0)
            {
                Canvas canvas = GetComponentInParent<Canvas>();
                if (canvas != null)
                {
                    allSlots = canvas.GetComponentsInChildren<SnapSlot>(true);
                }
            }

            if (allSlots == null || allSlots.Length == 0)
            {
                return null;
            }

            Camera cam = (_parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay) ? _parentCanvas.worldCamera : null;
            Vector2 itemScreenPos = RectTransformUtility.WorldToScreenPoint(cam, RectTransform.position);

            foreach (var slot in allSlots)
            {
                if (slot == null || slot.IsOccupied) continue;
                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(cam, slot.RectTransform.position);
                float dist = Vector2.Distance(itemScreenPos, slotScreenPos);
                if (dist < bestDistance)
                {
                    bestDistance = dist;
                    bestSlot = slot;
                }
            }

            if (bestDistance > _snapThreshold * 1.5f)
            {
                return null;
            }

            return bestSlot;
        }

        private IEnumerator SmoothMoveRoutine(Vector2 targetPos, float duration, Action onComplete)
        {
            Vector2 startPos = RectTransform.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                // EaseOutQuad
                t = t * (2f - t);
                RectTransform.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            RectTransform.anchoredPosition = targetPos;
            _activeMoveRoutine = null;
            onComplete?.Invoke();
        }
    }
}
