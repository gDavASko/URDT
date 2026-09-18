using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M19_FloodFillColoring
{
    /// <summary>
    /// Раскрашиваемый сегмент чертежа/маски.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class ColoringSegment : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private int _expectedColorId = 1;
        [SerializeField] private Image _fillImage = null;

        public event Action<ColoringSegment> OnSegmentClicked;

        private int _currentColorId = 0;
        public int ExpectedColorId => _expectedColorId;
        public int CurrentColorId => _currentColorId;
        public bool IsCorrect => _currentColorId == _expectedColorId;

        public void SetExpectedColorId(int id)
        {
            _expectedColorId = id;
        }

        private void Awake()
        {
            if (_fillImage == null) _fillImage = GetComponent<Image>();
            ResetSegment();
        }

        public void ResetSegment()
        {
            _currentColorId = 0;
            if (_fillImage == null) _fillImage = GetComponent<Image>();
            if (_fillImage != null) _fillImage.color = new Color(0.2f, 0.25f, 0.35f, 0.8f);
        }

        public void ApplyColor(int colorId, Color colorVisual)
        {
            _currentColorId = colorId;
            if (_fillImage != null)
            {
                _fillImage.color = colorVisual;
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnSegmentClicked?.Invoke(this);
        }
    }
}
