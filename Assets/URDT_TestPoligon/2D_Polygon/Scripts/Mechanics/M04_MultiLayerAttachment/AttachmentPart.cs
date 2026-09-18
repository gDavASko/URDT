using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M04_MultiLayerAttachment
{
    /// <summary>
    /// Деталь для многослойной сборки робота с поддержкой этапов, подсветкой целевого слота при взятии
    /// и валидацией технологической последовательности.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class AttachmentPart : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Идентификация детали")]
        [SerializeField] private int _stage = 1;
        [SerializeField] private int _layerIndex = 1;
        [SerializeField] private string _partName = "Деталь";
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Vector2 _homeAnchoredPosition;
        [SerializeField] private float _snapDistance = 150f;
        [SerializeField] private TMP_Text _partLabel = null;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector3 _originalScale = Vector3.one;
        private bool _isLocked;
        private Coroutine _moveRoutine;

        public int Stage => _stage;
        public int LayerIndex => _layerIndex;
        public string PartName => _partName;
        public bool IsJunk => _isJunk;
        public bool IsLocked => _isLocked;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<AttachmentPart, AttachmentSlot> OnPartSnapped;
        public event Action<AttachmentPart, string> OnPartFailed;

        private Transform _originalParent;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _originalParent = transform.parent;
            if (transform.localScale != Vector3.zero)
            {
                _originalScale = transform.localScale;
            }
            else
            {
                _originalScale = Vector3.one;
                transform.localScale = Vector3.one;
            }

            _parentCanvas = GetComponentInParent<Canvas>();
            if (_parentCanvas != null)
            {
                _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
            if (_homeAnchoredPosition == Vector2.zero && _rectTransform != null)
            {
                _homeAnchoredPosition = _rectTransform.anchoredPosition;
            }

            if (_partLabel == null)
            {
                _partLabel = GetComponentInChildren<TMP_Text>();
            }
            if (_partLabel == null)
            {
                EnsureRuntimeLabel();
            }
        }

        public void InitializePart(int stage, int layer, string partName, bool isJunk, Vector2 homePos)
        {
            _stage = stage;
            _layerIndex = layer;
            _partName = partName;
            _isJunk = isJunk;
            _homeAnchoredPosition = homePos;
            if (_originalParent == null) _originalParent = transform.parent;
            if (_rectTransform != null) _rectTransform.anchoredPosition = homePos;

            if (_partLabel != null)
            {
                _partLabel.text = _isJunk ? "<color=#FF6666>[X] ДЕФЕКТ</color>" : _partName;
            }
        }

        private void EnsureRuntimeLabel()
        {
            GameObject lblObj = new GameObject("PartLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = new Vector2(0f, 0f);
            lblRt.anchorMax = new Vector2(1f, 0f);
            lblRt.pivot = new Vector2(0.5f, 1f);
            lblRt.anchoredPosition = new Vector2(0f, -4f);
            lblRt.sizeDelta = new Vector2(120f, 20f);
            TextMeshProUGUI tmp = lblObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 12;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            tmp.text = _isJunk ? "<color=#FF6666>[X] ДЕФЕКТ</color>" : _partName;
            _partLabel = tmp;
        }

        public void SetHomePosition(Vector2 pos)
        {
            _homeAnchoredPosition = pos;
            if (_originalParent == null) _originalParent = transform.parent;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = pos;
                if (transform.localScale == Vector3.zero)
                {
                    transform.localScale = _originalScale != Vector3.zero ? _originalScale : Vector3.one;
                }
            }
        }

        public void ResetPart()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            _isLocked = false;
            if (_originalParent != null && transform.parent != _originalParent)
            {
                transform.SetParent(_originalParent, false);
            }
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null) _rectTransform.anchoredPosition = _homeAnchoredPosition;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isLocked) return;
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isLocked) return;
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            transform.SetAsLastSibling();
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale * 1.15f;

            // Подсвечиваем слот, куда нужно ставить эту деталь!
            var mechanic = GetComponentInParent<M04_MultiLayerAttachmentMechanic>();
            if (mechanic != null)
            {
                mechanic.HighlightSlotForPart(this);
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isLocked) return;

            if (_canvasRectTransform != null && _parentCanvas != null)
            {
                Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
                Vector3 worldPos;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        _canvasRectTransform,
                        eventData.position,
                        cam,
                        out worldPos))
                {
                    transform.position = worldPos;
                }
            }
            else
            {
                transform.position = eventData.position;
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isLocked) return;
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;

            // Выключаем подсветку слотов
            var mechanic = GetComponentInParent<M04_MultiLayerAttachmentMechanic>();
            if (mechanic != null)
            {
                mechanic.ClearSlotHighlights();
            }

            AttachmentSlot targetSlot = FindTargetSlot(eventData);
            int currentLayer = mechanic != null ? mechanic.CurrentInstalledLayer : 0;

            if (targetSlot != null)
            {
                if (_isJunk)
                {
                    OnPartFailed?.Invoke(this, "Бракованный дефектный блок (X) не подходит!");
                    _moveRoutine = StartCoroutine(ReturnHomeRoutine());
                    return;
                }

                if (targetSlot.CanInstall(this, currentLayer))
                {
                    _isLocked = true;
                    targetSlot.InstallPart(this);
                    OnPartSnapped?.Invoke(this, targetSlot);
                    _moveRoutine = StartCoroutine(SnapToSlotRoutine(targetSlot.transform.position));
                    return;
                }
                else
                {
                    if (currentLayer < targetSlot.RequiredPrerequisiteLayer)
                    {
                        OnPartFailed?.Invoke(this, $"Нарушен порядок монтажа! Сначала установите слой {targetSlot.RequiredPrerequisiteLayer}.");
                    }
                    else
                    {
                        OnPartFailed?.Invoke(this, $"Деталь «{_partName}» не подходит к данному сокету!");
                    }
                }
            }

            _moveRoutine = StartCoroutine(ReturnHomeRoutine());
        }

        private AttachmentSlot FindTargetSlot(PointerEventData eventData)
        {
            Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _parentCanvas.worldCamera
                : null;

            AttachmentSlot[] slots = FindObjectsByType<AttachmentSlot>(FindObjectsSortMode.None);
            AttachmentSlot bestSlot = null;
            float bestDistance = float.MaxValue;

            // Сперва ищем точный сокет для этой детали (по этапу и слою)
            foreach (var slot in slots)
            {
                if (slot == null || slot.IsInstalled) continue;
                if (slot.Stage != _stage || slot.LayerIndex != _layerIndex) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(slot.RectTransform, eventData.position, cam))
                {
                    return slot;
                }

                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(cam, slot.RectTransform.position);
                float dist = Vector2.Distance(eventData.position, slotScreenPos);
                if (dist <= _snapDistance && dist < bestDistance)
                {
                    bestDistance = dist;
                    bestSlot = slot;
                }
            }

            if (bestSlot != null) return bestSlot;

            // Если не нашли точный, проверяем остальные сокеты (для вывода ошибки нарушения порядка)
            foreach (var slot in slots)
            {
                if (slot == null || slot.IsInstalled) continue;
                if (slot.Stage != _stage) continue;

                Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(cam, slot.RectTransform.position);
                float dist = Vector2.Distance(eventData.position, slotScreenPos);
                if (dist <= _snapDistance && dist < bestDistance)
                {
                    bestDistance = dist;
                    bestSlot = slot;
                }
            }

            return bestSlot;
        }

        private IEnumerator SnapToSlotRoutine(Vector3 targetWorldPos)
        {
            float elapsed = 0f;
            float duration = 0.18f;
            Vector3 startPos = transform.position;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                transform.position = Vector3.Lerp(startPos, targetWorldPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            transform.position = targetWorldPos;
            _moveRoutine = null;
        }

        private IEnumerator ReturnHomeRoutine()
        {
            float elapsed = 0f;
            float duration = 0.22f;
            Vector2 startPos = _rectTransform.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _homeAnchoredPosition, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _rectTransform.anchoredPosition = _homeAnchoredPosition;
            _moveRoutine = null;
        }
    }
}
