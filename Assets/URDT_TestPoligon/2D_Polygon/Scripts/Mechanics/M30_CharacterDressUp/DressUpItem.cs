using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M30_CharacterDressUp
{
    public enum DressSlotType
    {
        Head,
        Body,
        Feet,
        Accessory,
        Junk
    }

    /// <summary>
    /// Перетаскиваемый предмет одежды/экипировки.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class DressUpItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private DressSlotType _slotType = DressSlotType.Body;
        [SerializeField] private bool _isJunk = false;

        public DressSlotType SlotType => _slotType;
        public bool IsJunk => _isJunk;

        public event Action<DressUpItem, PointerEventData> OnItemBeginDrag;
        public event Action<DressUpItem, PointerEventData> OnItemDrag;
        public event Action<DressUpItem, PointerEventData> OnItemEndDrag;

        private RectTransform _rectTransform;
        private Vector2 _initialPosition;
        private Transform _initialParent;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;
        private bool _isEquipped = false;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public bool IsEquipped => _isEquipped;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            _initialPosition = _rectTransform.anchoredPosition;
            _initialParent = transform.parent;
        }

        public void ResetItem()
        {
            _isEquipped = false;
            if (_initialParent != null && transform.parent != _initialParent)
            {
                transform.SetParent(_initialParent, false);
            }
            RectTransform.anchoredPosition = _initialPosition;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        }

        public void SetEquipped(Transform slotTransform)
        {
            _isEquipped = true;
            transform.SetParent(slotTransform, false);
            RectTransform.anchoredPosition = Vector2.zero;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false; // зафиксирован
        }

        public void SetInitialWardrobePosition(Vector2 pos, Transform parent)
        {
            _initialPosition = pos;
            if (parent != null) _initialParent = parent;
            if (!_isEquipped)
            {
                RectTransform.anchoredPosition = pos;
            }
        }

        public void ReturnToWardrobe()
        {
            _isEquipped = false;
            transform.SetParent(_initialParent, false);
            RectTransform.anchoredPosition = _initialPosition;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isEquipped) return;
            transform.SetAsLastSibling();
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
            OnItemBeginDrag?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isEquipped) return;
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, cam, out Vector2 localPoint))
            {
                RectTransform.localPosition = localPoint;
            }

            OnItemDrag?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isEquipped) return;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            OnItemEndDrag?.Invoke(this, eventData);
        }
    }
}
