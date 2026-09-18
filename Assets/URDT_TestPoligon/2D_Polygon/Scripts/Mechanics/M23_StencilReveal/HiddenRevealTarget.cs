using System;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M23_StencilReveal
{
    /// <summary>
    /// Скрытый объект, проявляющийся под линзой/маской и собираемый после 2 секунд непрерывного удержания лупы.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class HiddenRevealTarget : MonoBehaviour
    {
        [SerializeField] private bool _isJunkDust = false;
        [SerializeField] private Image _image = null;

        public event Action<HiddenRevealTarget> OnTargetCollected;

        private RectTransform _rectTransform;
        private bool _isCollected = false;
        private CanvasGroup _canvasGroup;
        private Vector3 _originalScale = Vector3.one;

        public bool IsJunkDust => _isJunkDust;
        public bool IsCollected => _isCollected;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (_image == null) _image = GetComponent<Image>();
            _originalScale = transform.localScale;
        }

        public void ResetTarget()
        {
            _isCollected = false;
            if (_canvasGroup == null) _canvasGroup = GetComponent<CanvasGroup>();
            if (_canvasGroup == null) _canvasGroup = gameObject.AddComponent<CanvasGroup>();
            if (_image == null) _image = GetComponent<Image>();
            transform.localScale = _originalScale;
            gameObject.SetActive(true);
            SetRevealed(false);
            SetHoldProgress(0f);
        }

        public void SetRevealed(bool revealed)
        {
            if (_canvasGroup != null)
            {
                _canvasGroup.alpha = revealed ? 1f : 0f;
                _canvasGroup.blocksRaycasts = false; // Пропускаем клики к лупе и рабочей области
            }
            if (_image != null)
            {
                _image.raycastTarget = false;
            }
            if (!revealed)
            {
                SetHoldProgress(0f);
            }
        }

        /// <summary>
        /// Визуализирует прогресс удержания лупы над объектом (0.0 .. 1.0)
        /// </summary>
        public void SetHoldProgress(float progress01)
        {
            progress01 = Mathf.Clamp01(progress01);
            float pulse = 1f + progress01 * 0.35f + Mathf.Sin(Time.unscaledTime * 12f) * (0.04f * progress01);
            transform.localScale = _originalScale * pulse;

            if (_image != null)
            {
                if (_isJunkDust)
                {
                    _image.color = Color.Lerp(Color.white, new Color(1f, 0.35f, 0.35f, 1f), progress01);
                }
                else
                {
                    _image.color = Color.Lerp(Color.white, new Color(0.25f, 1f, 0.65f, 1f), progress01);
                }
            }
        }

        public void Collect()
        {
            if (_isCollected) return;

            if (_isJunkDust)
            {
                OnTargetCollected?.Invoke(this);
                return;
            }

            _isCollected = true;
            gameObject.SetActive(false);
            OnTargetCollected?.Invoke(this);
        }
    }
}
