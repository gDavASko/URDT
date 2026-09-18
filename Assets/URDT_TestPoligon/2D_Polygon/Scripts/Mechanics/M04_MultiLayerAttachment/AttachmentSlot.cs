using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M04_MultiLayerAttachment
{
    /// <summary>
    /// Сокет для установки многослойной детали с валидацией зависимости (prerequisite layer)
    /// и интерактивной неоновой подсветкой при взятии детали.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class AttachmentSlot : MonoBehaviour
    {
        [Header("Конфигурация сокета")]
        [SerializeField] private int _stage = 1;
        [SerializeField] private int _layerIndex = 1;
        [SerializeField] private int _requiredPrerequisiteLayer = 0;
        [SerializeField] private string _slotName = "Слот";
        [SerializeField] private Image _slotOutline = null;
        [SerializeField] private Image _slotBackground = null;
        [SerializeField] private TMP_Text _slotHintLabel = null;

        private RectTransform _rectTransform;
        private AttachmentPart _installedPart;
        private Coroutine _pulseRoutine;
        private readonly Color _normalOutlineColor = new Color(0.3f, 0.5f, 0.7f, 0.45f);
        private readonly Color _highlightOutlineColor = new Color(0f, 1f, 0.95f, 1f);

        public int Stage => _stage;
        public int LayerIndex => _layerIndex;
        public int RequiredPrerequisiteLayer => _requiredPrerequisiteLayer;
        public string SlotName => _slotName;
        public bool IsInstalled => _installedPart != null;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<AttachmentSlot, AttachmentPart> OnPartInstalled;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureHintLabel();
            ResetSlot();
        }

        public void SetStageAndLayer(int stage, int layer, int prerequisite, string name)
        {
            _stage = stage;
            _layerIndex = layer;
            _requiredPrerequisiteLayer = prerequisite;
            _slotName = name;
        }

        private void EnsureHintLabel()
        {
            if (_slotHintLabel != null) return;
            GameObject lblObj = new GameObject("SlotHintLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(transform, false);
            RectTransform rt = lblObj.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(100f, 26f);

            TextMeshProUGUI tmp = lblObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 13;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.text = "▼ СЮДА ▼";
            tmp.color = _highlightOutlineColor;
            lblObj.SetActive(false);
            _slotHintLabel = tmp;
        }

        public void ResetSlot()
        {
            _installedPart = null;
            SetHighlighted(false);
            if (_slotOutline != null)
            {
                _slotOutline.color = _normalOutlineColor;
            }
        }

        public void SetHighlighted(bool active)
        {
            if (_pulseRoutine != null)
            {
                StopCoroutine(_pulseRoutine);
                _pulseRoutine = null;
            }

            if (IsInstalled)
            {
                if (_slotOutline != null) _slotOutline.color = new Color(0.2f, 0.95f, 0.4f, 0.9f);
                if (_slotHintLabel != null) _slotHintLabel.gameObject.SetActive(false);
                return;
            }

            if (active)
            {
                EnsureHintLabel();
                if (_slotHintLabel != null)
                {
                    _slotHintLabel.gameObject.SetActive(true);
                    _slotHintLabel.text = "▼ СЮДА ▼";
                    _slotHintLabel.color = _highlightOutlineColor;
                }
                _pulseRoutine = StartCoroutine(PulseRoutine());
            }
            else
            {
                if (_slotOutline != null)
                {
                    _slotOutline.color = _normalOutlineColor;
                }
                if (_slotHintLabel != null)
                {
                    _slotHintLabel.gameObject.SetActive(false);
                }
            }
        }

        private IEnumerator PulseRoutine()
        {
            float timer = 0f;
            while (true)
            {
                timer += Time.unscaledDeltaTime * 6f;
                float alpha = 0.5f + Mathf.PingPong(timer, 0.5f);
                if (_slotOutline != null)
                {
                    _slotOutline.color = new Color(_highlightOutlineColor.r, _highlightOutlineColor.g, _highlightOutlineColor.b, alpha);
                }
                yield return null;
            }
        }

        public bool CanInstall(AttachmentPart part, int currentMaxInstalledLayer)
        {
            if (IsInstalled) return false;
            if (part == null || part.IsJunk) return false;
            if (part.Stage != _stage) return false;
            if (part.LayerIndex != _layerIndex) return false;

            return currentMaxInstalledLayer >= _requiredPrerequisiteLayer;
        }

        public void InstallPart(AttachmentPart part)
        {
            _installedPart = part;
            SetHighlighted(false);
            if (_slotOutline != null)
            {
                _slotOutline.color = new Color(0.2f, 0.95f, 0.4f, 0.9f);
            }
            if (part != null)
            {
                part.transform.SetParent(transform, true);
                part.RectTransform.anchoredPosition = Vector2.zero;
            }
            OnPartInstalled?.Invoke(this, part);
        }
    }
}
