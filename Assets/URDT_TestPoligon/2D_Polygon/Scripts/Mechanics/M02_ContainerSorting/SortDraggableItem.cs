using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M02_ContainerSorting
{
    /// <summary>
    /// Перетаскиваемый предмет для сортировки в корзину.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class SortDraggableItem : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Идентификация предмета")]
        [SerializeField] private string _itemTypeId = "red";
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Image _itemImage = null;

        [Header("Параметры драга")]
        [SerializeField] private float _dropDistanceThreshold = 110f;
        [SerializeField] private float _dragScale = 1.15f;
        [SerializeField] private float _returnDuration = 0.22f;
        [SerializeField] private Vector2 _homeAnchoredPosition;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector3 _originalScale = Vector3.one;
        private int _originalSiblingIndex;
        private bool _isDeposited;
        private Coroutine _activeMoveRoutine;
        private SortContainer _currentHoveredContainer;

        public string ItemTypeId => _itemTypeId;
        public bool IsJunk => _isJunk;
        /// <summary>Где предмет лежит (или куда едет) в лотке.</summary>
        public Vector2 HomePosition => _homeAnchoredPosition;
        public bool IsDeposited => _isDeposited;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<SortDraggableItem, SortContainer> OnItemDeposited;
        public event Action<SortDraggableItem, SortContainer> OnItemRejected;

        public void SetItemTypeId(string id) { _itemTypeId = id; }
        public void SetJunk(bool junk) { _isJunk = junk; }

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
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
        }

        public void SetHomePosition(Vector2 position)
        {
            _homeAnchoredPosition = position;
            if (_rectTransform != null)
            {
                _rectTransform.anchoredPosition = position;
                if (transform.localScale == Vector3.zero)
                {
                    transform.localScale = _originalScale != Vector3.zero ? _originalScale : Vector3.one;
                }
            }
        }

        public void ResetItem()
        {
            if (_activeMoveRoutine != null)
            {
                StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = null;
            }

            _isDeposited = false;
            gameObject.SetActive(true);
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null) _rectTransform.anchoredPosition = _homeAnchoredPosition;
            _currentHoveredContainer = null;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isDeposited) return;
            transform.SetAsLastSibling();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isDeposited) return;

            if (_activeMoveRoutine != null)
            {
                StopCoroutine(_activeMoveRoutine);
                _activeMoveRoutine = null;
            }

            _originalSiblingIndex = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale * _dragScale;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isDeposited) return;

            if (_canvasRectTransform != null && _parentCanvas != null)
            {
                Vector2 localPoint;
                Camera eventCamera = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                        _canvasRectTransform,
                        eventData.position,
                        eventCamera,
                        out localPoint))
                {
                    Vector3 worldPos;
                    if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                            _rectTransform,
                            eventData.position,
                            eventCamera,
                            out worldPos))
                    {
                        transform.position = worldPos;
                    }
                }
            }
            else
            {
                transform.position = eventData.position;
            }

            UpdateHoverFeedback();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isDeposited) return;

            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            SortContainer targetContainer = FindClosestContainer();

            if (targetContainer != null && targetContainer.CanAccept(_itemTypeId) && !_isJunk)
            {
                // Успешный депозит
                if (targetContainer.TryDeposit(_itemTypeId))
                {
                    _isDeposited = true;
                    OnItemDeposited?.Invoke(this, targetContainer);
                    StartCoroutine(ShrinkAndDisappearRoutine(targetContainer.transform.position));
                    return;
                }
            }

            // Ошибка или мимо -> возврат
            if (_currentHoveredContainer != null)
            {
                _currentHoveredContainer.SetHoverHighlight(0);
                _currentHoveredContainer = null;
            }

            OnItemRejected?.Invoke(this, targetContainer);
            _activeMoveRoutine = StartCoroutine(ReturnToHomeRoutine());
        }

        private void UpdateHoverFeedback()
        {
            SortContainer nearest = FindClosestContainer();

            if (nearest != _currentHoveredContainer)
            {
                if (_currentHoveredContainer != null)
                {
                    _currentHoveredContainer.SetHoverHighlight(0);
                }

                _currentHoveredContainer = nearest;
                if (_currentHoveredContainer != null)
                {
                    bool isMatch = _currentHoveredContainer.CanAccept(_itemTypeId) && !_isJunk;
                    _currentHoveredContainer.SetHoverHighlight(isMatch ? 1 : -1);
                }
            }
        }

        private SortContainer FindClosestContainer()
        {
            SortContainer[] containers = FindObjectsByType<SortContainer>();
            SortContainer bestContainer = null;
            float bestDistance = _dropDistanceThreshold;

            foreach (var container in containers)
            {
                if (container == null) continue;
                float distance = Vector2.Distance(
                    _rectTransform.position,
                    container.RectTransform.position);

                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestContainer = container;
                }
            }

            return bestContainer;
        }

        private IEnumerator ShrinkAndDisappearRoutine(Vector3 targetWorldPos)
        {
            float elapsed = 0f;
            float duration = 0.2f;
            Vector3 startPos = transform.position;
            Vector3 startScale = transform.localScale;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                transform.position = Vector3.Lerp(startPos, targetWorldPos, smooth);
                transform.localScale = Vector3.Lerp(startScale, Vector3.zero, smooth);
                yield return null;
            }

            gameObject.SetActive(false);
        }

        private IEnumerator ReturnToHomeRoutine()
        {
            float elapsed = 0f;
            Vector2 startPos = _rectTransform.anchoredPosition;

            while (elapsed < _returnDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / _returnDuration);
                float smooth = Mathf.SmoothStep(0f, 1f, t);

                _rectTransform.anchoredPosition = Vector2.Lerp(startPos, _homeAnchoredPosition, smooth);
                yield return null;
            }

            _rectTransform.anchoredPosition = _homeAnchoredPosition;
            _activeMoveRoutine = null;
        }
    }
}
