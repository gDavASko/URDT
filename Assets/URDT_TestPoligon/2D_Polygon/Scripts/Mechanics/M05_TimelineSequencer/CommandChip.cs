using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M05_TimelineSequencer
{
    public enum CommandType
    {
        Forward,
        Turn,
        Interact,
        Glitch
    }

    /// <summary>
    /// Фишка команды для размещения в слотах очереди исполнения.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class CommandChip : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Параметры команды")]
        [SerializeField] private CommandType _commandType = CommandType.Forward;
        [SerializeField] private string _commandName = "Вперед";
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Vector2 _homeAnchoredPosition;
        [SerializeField] private float _snapDistance = 85f;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private Vector3 _originalScale = Vector3.one;
        private CommandSlot _currentSlot;
        private Coroutine _moveRoutine;

        public CommandType CommandType => _commandType;
        public string CommandName => _commandName;
        public bool IsJunk => _isJunk;

        public void SetJunk(bool j) { _isJunk = j; if (j) _commandType = CommandType.Glitch; }
        public void SetCommand(CommandType t, string label) { _commandType = t; _commandName = label; }
        public CommandSlot CurrentSlot => _currentSlot;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<CommandChip, CommandSlot> OnChipPlaced;
        public event Action<CommandChip> OnChipRemoved;

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

        public void ResetChip()
        {
            if (_moveRoutine != null)
            {
                StopCoroutine(_moveRoutine);
                _moveRoutine = null;
            }

            if (_currentSlot != null)
            {
                _currentSlot.ClearSlot();
                _currentSlot = null;
            }

            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale;
            if (_rectTransform == null) _rectTransform = GetComponent<RectTransform>();
            if (_rectTransform != null) _rectTransform.anchoredPosition = _homeAnchoredPosition;
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

            if (_currentSlot != null)
            {
                _currentSlot.ClearSlot();
                OnChipRemoved?.Invoke(this);
                _currentSlot = null;
            }

            transform.SetAsLastSibling();
            if (_originalScale == Vector3.zero) _originalScale = Vector3.one;
            transform.localScale = _originalScale * 1.15f;
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_canvasRectTransform != null && _parentCanvas != null)
            {
                Camera cam = _parentCanvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : _parentCanvas.worldCamera;
                Vector3 worldPos;
                if (RectTransformUtility.ScreenPointToWorldPointInRectangle(
                        _rectTransform,
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
            CommandSlot targetSlot = FindNearestSlot();

            if (targetSlot != null && !targetSlot.IsOccupied)
            {
                _currentSlot = targetSlot;
                targetSlot.AssignChip(this);
                OnChipPlaced?.Invoke(this, targetSlot);
                _moveRoutine = StartCoroutine(SnapRoutine(targetSlot.transform.position));
            }
            else
            {
                _moveRoutine = StartCoroutine(ReturnHomeRoutine());
            }
        }

        private CommandSlot FindNearestSlot()
        {
            CommandSlot[] slots = FindObjectsByType<CommandSlot>();
            CommandSlot best = null;
            float bestDist = _snapDistance;

            foreach (var slot in slots)
            {
                if (slot == null || slot.IsOccupied) continue;
                float dist = Vector2.Distance(_rectTransform.position, slot.RectTransform.position);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = slot;
                }
            }

            return best;
        }

        private IEnumerator SnapRoutine(Vector3 targetWorldPos)
        {
            float elapsed = 0f;
            float duration = 0.16f;
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
