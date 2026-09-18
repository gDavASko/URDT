using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace KBP.URDT.TestPoligon.Mechanics2D.M24_TargetElimination
{
    /// <summary>
    /// Всплывающая мишень (пузырь/шар/бомба).
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class PoppableTarget : MonoBehaviour, IPointerClickHandler
    {
        [SerializeField] private bool _isHazardBomb = false;
        [SerializeField] private Image _image = null;

        public event Action<PoppableTarget> OnPopped;

        private RectTransform _rectTransform;
        private float _speed = 120f;
        private bool _isPopped = false;

        public bool IsHazardBomb => _isHazardBomb;
        public bool IsPopped => _isPopped;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            if (_image == null) _image = GetComponent<Image>();
        }

        public void Spawn(bool isHazard, Vector2 startPos, float speed, Sprite sprite)
        {
            _isHazardBomb = isHazard;
            _speed = speed;
            _isPopped = false;
            gameObject.SetActive(true);

            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            _rectTransform.anchoredPosition = startPos;

            if (_image != null)
            {
                _image.sprite = sprite;
                _image.color = Color.white;
            }
        }

        private void Update()
        {
            if (_isPopped) return;

            Vector2 pos = _rectTransform.anchoredPosition;
            pos.y += _speed * Time.unscaledDeltaTime;
            _rectTransform.anchoredPosition = pos;

            if (pos.y > 210f)
            {
                gameObject.SetActive(false);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_isPopped) return;

            _isPopped = true;
            gameObject.SetActive(false);
            OnPopped?.Invoke(this);
        }
    }
}
