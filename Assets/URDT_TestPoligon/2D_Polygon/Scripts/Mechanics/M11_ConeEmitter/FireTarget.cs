using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M11_ConeEmitter
{
    /// <summary>
    /// Очаг возгорания (мишень) с запасом прочности (HP).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class FireTarget : MonoBehaviour
    {
        [SerializeField] private float _maxHp = 100f;
        [SerializeField] private Image _hpBarFill = null;

        private RectTransform _rectTransform;
        private float _currentHp;
        private bool _isExtinguished;

        public bool IsExtinguished => _isExtinguished;
        public float CurrentHp => _currentHp;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            ResetTarget();
        }

        public void ResetTarget()
        {
            _currentHp = _maxHp;
            _isExtinguished = false;
            transform.localScale = Vector3.one;
            gameObject.SetActive(true);
            UpdateBar();
        }

        public void ApplyDamage(float dps)
        {
            if (_isExtinguished) return;

            _currentHp -= dps * Time.unscaledDeltaTime;
            UpdateBar();

            float ratio = Mathf.Clamp01(_currentHp / _maxHp);
            transform.localScale = Vector3.one * Mathf.Lerp(0.45f, 1f, ratio);

            if (_currentHp <= 0f)
            {
                _currentHp = 0f;
                _isExtinguished = true;
                gameObject.SetActive(false);
            }
        }

        private void UpdateBar()
        {
            if (_hpBarFill != null)
            {
                _hpBarFill.fillAmount = Mathf.Clamp01(_currentHp / _maxHp);
            }
        }
    }
}
