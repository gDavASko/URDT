using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M09_CoverageAccumulator
{
    /// <summary>
    /// Ячейка загрязнения для стирания губкой.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class DirtCell : MonoBehaviour
    {
        [SerializeField] private bool _isPermanentHazard = false;
        private Image _image;
        private bool _isCleaned;

        public bool IsCleaned => _isCleaned;
        public bool IsPermanentHazard => _isPermanentHazard;

        private void Awake()
        {
            _image = GetComponent<Image>();
            ResetCell();
        }

        public void SetHazard(bool hazard)
        {
            _isPermanentHazard = hazard;
            if (_image != null)
            {
                _image.color = hazard ? new Color(0.9f, 0.2f, 0.2f, 0.9f) : new Color(0.35f, 0.25f, 0.15f, 0.85f);
            }
        }

        public void ResetCell()
        {
            _isCleaned = false;
            if (_image != null)
            {
                _image.color = _isPermanentHazard ? new Color(0.9f, 0.2f, 0.2f, 0.9f) : new Color(0.35f, 0.25f, 0.15f, 0.85f);
            }
        }

        public bool TryClean()
        {
            if (_isPermanentHazard || _isCleaned) return false;
            _isCleaned = true;
            if (_image != null)
            {
                _image.color = new Color(0.35f, 0.25f, 0.15f, 0.05f); // Почти прозрачный
            }
            return true;
        }
    }
}
