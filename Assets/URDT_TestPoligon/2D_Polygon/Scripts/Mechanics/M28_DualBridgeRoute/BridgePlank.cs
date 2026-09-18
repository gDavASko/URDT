using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M28_DualBridgeRoute
{
    /// <summary>
    /// Перетаскиваемый мостик для установки в провал дороги.
    /// Автоматически восстанавливает сочный визуальный стиль, контур, тень и информативную плашку.
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class BridgePlank : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [SerializeField] private int _bridgeId = 1;
        [SerializeField] private bool _isBroken = false;

        [SerializeField] private Transform _initialParent = null;
        [SerializeField] private Vector2 _initialPosition = Vector2.zero;

        public int BridgeId => _bridgeId;
        public bool IsBroken => _isBroken;

        public event Action<BridgePlank, PointerEventData> OnPlankBeginDrag;
        public event Action<BridgePlank, PointerEventData> OnPlankDrag;
        public event Action<BridgePlank, PointerEventData> OnPlankEndDrag;

        private RectTransform _rectTransform;
        private Canvas _canvas;
        private CanvasGroup _canvasGroup;

        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());
        public Vector2 InitialPosition => _initialPosition;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _canvas = GetComponentInParent<Canvas>();
            _canvasGroup = GetComponent<CanvasGroup>() ?? gameObject.AddComponent<CanvasGroup>();
            if (_initialParent == null) _initialParent = transform.parent;
            if (_initialPosition == Vector2.zero && _rectTransform != null && _rectTransform.anchoredPosition != Vector2.zero)
            {
                _initialPosition = _rectTransform.anchoredPosition;
            }

            ApplyVisualTheme();
        }

        public void SetInitialOrigin(Transform initialParent, Vector2 initialPos)
        {
            _initialParent = initialParent;
            _initialPosition = initialPos;
            if (transform.parent != initialParent)
            {
                transform.SetParent(initialParent, false);
            }
            RectTransform.anchoredPosition = initialPos;
        }

        private void Start()
        {
            ApplyVisualTheme();
        }

        private void OnEnable()
        {
            ApplyVisualTheme();
        }

        public void SetBroken(bool broken)
        {
            _isBroken = broken;
            ApplyVisualTheme();
        }

        public void SetBridgeId(int id)
        {
            _bridgeId = id;
            ApplyVisualTheme();
        }

        public void ApplyVisualTheme()
        {
            Image img = GetComponent<Image>();
            if (img == null) img = gameObject.AddComponent<Image>();
            img.raycastTarget = true;

            if (_isBroken)
            {
                // Сломанный мостик [X]
                img.color = new Color(0.65f, 0.18f, 0.18f, 1f);

                Outline outline = GetComponent<Outline>();
                if (outline == null) outline = gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(1f, 0.35f, 0.35f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                Shadow shadow = GetComponent<Shadow>();
                if (shadow == null) shadow = gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shadow.effectDistance = new Vector2(2f, -2f);

                EnsureLabel("[X] СЛОМАН!", new Color(1f, 0.85f, 0.2f));
            }
            else
            {
                // Исправный прочный деревянный мостик
                img.color = new Color(0.72f, 0.44f, 0.2f, 1f);

                Outline outline = GetComponent<Outline>();
                if (outline == null) outline = gameObject.AddComponent<Outline>();
                outline.effectColor = new Color(0.95f, 0.75f, 0.35f, 1f);
                outline.effectDistance = new Vector2(2f, -2f);

                Shadow shadow = GetComponent<Shadow>();
                if (shadow == null) shadow = gameObject.AddComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.75f);
                shadow.effectDistance = new Vector2(2f, -2f);

                EnsureLabel($"МОСТИК {_bridgeId}", Color.white);
            }
        }

        private void EnsureLabel(string text, Color textColor)
        {
            Transform lblTr = transform.Find("PlankLabel");
            TextMeshProUGUI tmp;
            if (lblTr == null)
            {
                GameObject lblObj = new GameObject("PlankLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
                lblObj.transform.SetParent(transform, false);
                RectTransform lRt = lblObj.GetComponent<RectTransform>();
                lRt.anchorMin = Vector2.zero;
                lRt.anchorMax = Vector2.one;
                lRt.offsetMin = Vector2.zero;
                lRt.offsetMax = Vector2.zero;
                tmp = lblObj.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                tmp = lblTr.GetComponent<TextMeshProUGUI>();
            }

            if (tmp != null)
            {
                tmp.text = text;
                tmp.fontSize = 13;
                tmp.fontStyle = FontStyles.Bold;
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.color = textColor;
                tmp.raycastTarget = false; // Текст не блокирует события мыши!
            }
        }

        public void ResetPlank()
        {
            if (_initialParent != null && transform.parent != _initialParent)
            {
                transform.SetParent(_initialParent, false);
            }
            RectTransform.anchoredPosition = _initialPosition;
            transform.localScale = Vector3.one;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            ApplyVisualTheme();
        }

        public void ReturnToOrigin()
        {
            if (_initialParent != null && transform.parent != _initialParent)
            {
                transform.SetParent(_initialParent, false);
            }
            RectTransform.anchoredPosition = _initialPosition;
            transform.localScale = Vector3.one;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            if (_canvas != null)
            {
                transform.SetParent(_canvas.transform, true);
            }
            transform.SetAsLastSibling();
            transform.localScale = new Vector3(1.08f, 1.08f, 1f);
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = false;
            OnPlankBeginDrag?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            Camera cam = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay ? _canvas.worldCamera : null;

            RectTransform parentRect = transform.parent as RectTransform;
            if (parentRect != null && RectTransformUtility.ScreenPointToLocalPointInRectangle(parentRect, eventData.position, cam, out Vector2 localPoint))
            {
                RectTransform.localPosition = localPoint;
            }

            OnPlankDrag?.Invoke(this, eventData);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            transform.localScale = Vector3.one;
            if (_canvasGroup != null) _canvasGroup.blocksRaycasts = true;
            OnPlankEndDrag?.Invoke(this, eventData);
        }
    }
}
