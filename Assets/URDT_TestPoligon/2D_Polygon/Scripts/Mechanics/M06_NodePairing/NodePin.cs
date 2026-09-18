using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon.Mechanics2D.M06_NodePairing
{
    /// <summary>
    /// Контактный пин (терминал) для протягивания эластичных проводных соединений.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public class NodePin : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        [Header("Параметры пина")]
        [SerializeField] private string _pairId = "A";
        [SerializeField] private bool _isSource = true; // true = левая сторона (источник), false = правая сторона (приемник)
        [SerializeField] private bool _isJunk = false;
        [SerializeField] private Image _pinImage = null;

        private RectTransform _rectTransform;
        private Canvas _parentCanvas;
        private RectTransform _canvasRectTransform;
        private bool _isConnected;
        private NodePin _connectedTarget;

        public string PairId => _pairId;
        public bool IsSource => _isSource;
        public bool IsJunk => _isJunk;
        public bool IsConnected => _isConnected;
        public NodePin ConnectedTarget => _connectedTarget;
        public RectTransform RectTransform => _rectTransform != null ? _rectTransform : (_rectTransform = GetComponent<RectTransform>());

        public event Action<NodePin, Vector2> OnDragUpdated;
        public event Action<NodePin, PointerEventData> OnDragEnded;

        private void Awake()
        {
            _rectTransform = GetComponent<RectTransform>();
            EnsureCanvas();
            ResetPin();
        }

        private void EnsureCanvas()
        {
            if (_parentCanvas == null)
            {
                _parentCanvas = GetComponentInParent<Canvas>();
                if (_parentCanvas != null) _canvasRectTransform = _parentCanvas.GetComponent<RectTransform>();
            }
        }

        public void ResetPin()
        {
            _isConnected = false;
            _connectedTarget = null;
            if (_pinImage == null) _pinImage = GetComponent<Image>();
            if (_pinImage != null)
            {
                _pinImage.color = Color.white;
            }
        }

        public void Connect(NodePin target)
        {
            _isConnected = true;
            _connectedTarget = target;
            if (_pinImage == null) _pinImage = GetComponent<Image>();
            if (_pinImage != null)
            {
                _pinImage.color = Color.white;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_isConnected) return;
            EnsureCanvas();
            OnDragUpdated?.Invoke(this, eventData.position);
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_isConnected) return;
            EnsureCanvas();
            OnDragUpdated?.Invoke(this, eventData.position);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_isConnected) return;
            EnsureCanvas();
            OnDragUpdated?.Invoke(this, eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (_isConnected) return;
            EnsureCanvas();
            OnDragEnded?.Invoke(this, eventData);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isConnected) return;
            EnsureCanvas();
            OnDragEnded?.Invoke(this, eventData);
        }
    }
}
