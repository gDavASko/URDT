using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M01_SnapToSlot
{
    /// <summary>
    /// Целевой посадочный паз / слот (Socket) для механики Snap-to-Slot.
    /// Отвечает за валидацию совпадения идентификатора детали и визуальный отклик при наведении.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SnapSlot : MonoBehaviour
    {
        [Header("Конфигурация слота")]
        [SerializeField] private string _slotId = "default_slot";
        [SerializeField] private Image _slotImage = null;
        [SerializeField] private Image _highlightBorder = null;
        [SerializeField] private TMP_Text _label = null;

        [Header("Цветовая индикация")]
        [SerializeField] private Color _normalBorderColor = new Color(0.3f, 0.4f, 0.5f, 0.5f);
        [SerializeField] private Color _matchHoverColor = new Color(0.1f, 0.95f, 0.4f, 0.95f);
        [SerializeField] private Color _rejectHoverColor = new Color(0.95f, 0.2f, 0.2f, 0.95f);
        [SerializeField] private Color _occupiedColor = new Color(0.2f, 0.9f, 0.5f, 0.8f);

        private RectTransform _rectTransform;
        private SnapDraggableItem _occupiedItem;

        public string SlotId => _slotId;
        public bool IsOccupied => _occupiedItem != null;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ResetSlot();
        }

        public void Configure(string slotId, string labelText)
        {
            _slotId = slotId;
            if (_label != null) _label.text = labelText;
            ResetSlot();
        }

        public void SetHover(bool isHovering, bool isMatch)
        {
            if (IsOccupied) return;

            if (_highlightBorder != null)
            {
                if (!isHovering)
                {
                    _highlightBorder.color = _normalBorderColor;
                }
                else
                {
                    _highlightBorder.color = isMatch ? _matchHoverColor : _rejectHoverColor;
                }
            }
        }

        public bool CanAccept(SnapDraggableItem item)
        {
            if (IsOccupied) return false;
            if (item == null) return false;
            if (item.IsJunk) return false;
            return string.Equals(_slotId, item.ItemId, System.StringComparison.OrdinalIgnoreCase);
        }

        public void AcceptItem(SnapDraggableItem item)
        {
            _occupiedItem = item;
            if (_highlightBorder != null)
            {
                _highlightBorder.color = _occupiedColor;
            }
        }

        public void ResetSlot()
        {
            _occupiedItem = null;
            if (_highlightBorder != null)
            {
                _highlightBorder.color = _normalBorderColor;
            }
        }
    }
}
