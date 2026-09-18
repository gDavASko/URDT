using System;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M14_SlingshotImpulser
{
    /// <summary>
    /// Мишень-банка для сбивания из рогатки.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SlingshotTargetCan : MonoBehaviour
    {
        [SerializeField] private bool _isObstacle = false;

        public event Action<SlingshotTargetCan> OnHit;

        private RectTransform _rectTransform;
        private Image _image;
        private bool _isHit = false;
        private Vector2 _originalPos;

        public bool IsObstacle => _isObstacle;
        public bool IsHit => _isHit;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _image = GetComponent<Image>();
            _originalPos = _rectTransform.anchoredPosition;
        }

        public void ResetCan()
        {
            _isHit = false;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
            if (_originalPos == Vector2.zero && _rectTransform != null) _originalPos = _rectTransform.anchoredPosition;

            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = _originalPos;
                _rectTransform.localRotation = Quaternion.identity;
            }
            gameObject.SetActive(true);
            if (_image != null) _image.color = Color.white;
        }

        public void Hit()
        {
            if (_isHit || _isObstacle) return;
            _isHit = true;

            // Визуальный наклон и падение
            _rectTransform.localRotation = Quaternion.Euler(0f, 0f, 75f);
            if (_image != null) _image.color = new Color(0.6f, 0.6f, 0.6f, 0.5f);

            OnHit?.Invoke(this);
        }
    }
}
