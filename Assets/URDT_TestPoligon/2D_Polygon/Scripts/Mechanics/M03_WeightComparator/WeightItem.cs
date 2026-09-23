using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon.Mechanics2D.M03_WeightComparator
{
    /// <summary>
    /// Перетаскиваемая гиря с определенной массой.
    /// Поддерживает drag-and-drop, привязку к чашам весов и визуальное отображение массы.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class WeightItem : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Параметры гири")]
        [SerializeField] private float _mass = 5f;
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Vector2 _homeAnchoredPosition;
        [SerializeField] private TMP_Text _massLabel = null;

        private RectTransform _rectTransform;
        private Transform _homeParent;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector3 _originalScale = Vector3.one;
        private ScalePan _currentPan;
        private Coroutine _moveRoutine;

        public float Mass => _mass;
        public bool IsJunk => _isJunk;

        public void SetMass(float m) { _mass = m; UpdateVisuals(); }
        public void SetJunk(bool j) { _isJunk = j; UpdateVisuals(); }
        public ScalePan CurrentPan => _currentPan;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<WeightItem, ScalePan> OnPlacedOnPan;
        public event Action<WeightItem> OnRemovedFromPan;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            _homeParent = transform.parent;

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

            if (_massLabel == null)
            {
                _massLabel = GetComponentInChildren<TMP_Text>();
            }
            if (_massLabel == null)
            {
                EnsureRuntimeLabel();
            }

            UpdateVisuals();
        }

        private void EnsureRuntimeLabel()
        {
            GameObject lblObj = new GameObject("MassLabel", typeof(RectTransform), typeof(TextMeshProUGUI));
            lblObj.transform.SetParent(transform, false);
            RectTransform lblRt = lblObj.GetComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(4f, 4f);
            lblRt.offsetMax = new Vector2(-4f, -12f);
            TextMeshProUGUI tmp = lblObj.GetComponent<TextMeshProUGUI>();
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.raycastTarget = false;
            _massLabel = tmp;
        }

        public void InitializeWeight(float mass, bool isJunk)
        {
            _mass = mass;
            _isJunk = isJunk;
            UpdateVisuals();
        }

        public void SetHomePosition(Vector2 pos)
        {
            _homeAnchoredPosition = pos;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = pos;
                if (transform.localScale == Vector3.zero)
                {
                    transform.localScale = _originalScale != Vector3.zero ? _originalScale : Vector3.one;
                }
            }
        }

        public void UpdateVisuals()
        {
            if (_massLabel != null)
            {
                if (_isJunk)
                {
                    _massLabel.text = "<color=#FFAA44>0 кг</color>\n<size=11><color=#FF6666>(X)</color></size>";
                }
                else
                {
                    _massLabel.text = $"<b>{_mass:F0} кг</b>";
                }
                _massLabel.raycastTarget = false;
            }
        }

        public void ResetWeight()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_currentPan != null)
            {
                _currentPan.RemoveWeight(this);
                _currentPan = null;
            }

            if (_homeParent != null && transform.parent != _homeParent)
            {
                transform.SetParent(_homeParent, true);
            }

            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null) _rectTransform.anchoredPosition = _homeAnchoredPosition;
            UpdateVisuals();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_currentPan != null)
            {
                _currentPan.RemoveWeight(this);
                OnRemovedFromPan?.Invoke(this);
                _currentPan = null;
            }

            if (_parentCanvas != null)
            {
                transform.SetParent(_parentCanvas.transform, true);
            }

            transform.SetAsLastSibling();
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale * 1.14f;
        }

        public void OnDrag(PointerEventData eventData)
        {
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
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;

            ScalePan targetPan = FindTargetPan(eventData);

            if (targetPan != null)
            {
                _currentPan = targetPan;
                _currentPan.AddWeight(this);
                OnPlacedOnPan?.Invoke(this, targetPan);
            }
            else
            {
                _moveRoutine = StartCoroutine(ReturnToHomeRoutine());
            }
        }

        public void SnapToPanLocal(ScalePan pan, Vector2 targetLocalPos)
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            transform.SetParent(pan.ItemsAnchor, true);
            _moveRoutine = StartCoroutine(SnapToPanLocalRoutine(targetLocalPos));
        }

        private IEnumerator SnapToPanLocalRoutine(Vector2 targetLocalPos)
        {
            float elapsed = 0f;
            float duration = 0.16f;
            Vector2 startPos = _rectTransform.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, targetLocalPos, Mathf.SmoothStep(0f, 1f, t));
                yield return null;
            }

            _rectTransform.anchoredPosition = targetLocalPos;
            _moveRoutine = null;
        }

        private ScalePan FindTargetPan(PointerEventData eventData)
        {
            Camera cam = _parentCanvas != null && _parentCanvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _parentCanvas.worldCamera
                : null;

            ScalePan[] pans = FindObjectsByType<ScalePan>(FindObjectsSortMode.None);
            ScalePan bestPan = null;
            float bestDistance = float.MaxValue;

            foreach (var pan in pans)
            {
                if (pan == null || !pan.AcceptsDrop) continue;

                if (RectTransformUtility.RectangleContainsScreenPoint(pan.RectTransform, eventData.position, cam))
                {
                    return pan;
                }

                Vector2 panScreenPos = RectTransformUtility.WorldToScreenPoint(cam, pan.RectTransform.position);
                float dist = Vector2.Distance(eventData.position, panScreenPos);
                if (dist < 180f && dist < bestDistance)
                {
                    bestDistance = dist;
                    bestPan = pan;
                }
            }

            return bestPan;
        }

        private IEnumerator ReturnToHomeRoutine()
        {
            float elapsed = 0f;
            float duration = 0.2f;

            if (_homeParent != null && transform.parent != _homeParent)
            {
                transform.SetParent(_homeParent, true);
            }

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
