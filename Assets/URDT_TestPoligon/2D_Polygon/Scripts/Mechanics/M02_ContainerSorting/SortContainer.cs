using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M02_ContainerSorting
{
    /// <summary>
    /// Контейнер / корзина для сортировки предметов по категориям/типам.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SortContainer : MonoBehaviour
    {
        [Header("Конфигурация корзины")]
        [SerializeField] private string _acceptedTypeId = "red";
        [SerializeField] private int _requiredCount = 2;
        [SerializeField] private Image _containerImage = null;
        [SerializeField] private Image _highlightBorder = null;
        [SerializeField] private TMP_Text _countLabel = null;

        [Header("Цвета индикации")]
        [SerializeField] private Color _normalBorderColor = new Color(0.3f, 0.4f, 0.5f, 0.4f);
        [SerializeField] private Color _matchHoverColor = new Color(0.2f, 0.95f, 0.4f, 0.95f);
        [SerializeField] private Color _rejectHoverColor = new Color(0.95f, 0.25f, 0.25f, 0.95f);

        private RectTransform _rectTransform;
        private int _currentCount;

        public string AcceptedTypeId => _acceptedTypeId;
        public int RequiredCount => _requiredCount;
        public int CurrentCount => _currentCount;
        public bool IsFull => _currentCount >= _requiredCount;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<SortContainer, int> OnCountChanged;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ResetContainer();
        }

        public void ResetContainer()
        {
            _currentCount = 0;
            UpdateUi();
            SetHoverHighlight(0);
        }

        public bool CanAccept(string itemTypeId)
        {
            return !IsFull && string.Equals(_acceptedTypeId, itemTypeId, StringComparison.OrdinalIgnoreCase);
        }

        public bool TryDeposit(string itemTypeId)
        {
            if (!CanAccept(itemTypeId)) return false;

            _currentCount++;
            UpdateUi();
            OnCountChanged?.Invoke(this, _currentCount);
            SetHoverHighlight(0);
            return true;
        }

        /// <summary>
        /// 0 = normal, 1 = match hover (green), -1 = reject hover (red)
        /// </summary>
        public void SetHoverHighlight(int state)
        {
            if (_highlightBorder == null) return;

            switch (state)
            {
                case 1:
                    _highlightBorder.color = _matchHoverColor;
                    break;
                case -1:
                    _highlightBorder.color = _rejectHoverColor;
                    break;
                default:
                    _highlightBorder.color = _normalBorderColor;
                    break;
            }
        }

        private void UpdateUi()
        {
            if (_countLabel != null)
            {
                _countLabel.text = $"{_currentCount}/{_requiredCount}";
                _countLabel.color = IsFull ? new Color(0.3f, 1f, 0.4f) : Color.white;
            }
        }
    }
}
