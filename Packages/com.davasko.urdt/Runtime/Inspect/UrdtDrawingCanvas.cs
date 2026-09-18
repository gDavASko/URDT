using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Interactive Drawing Canvas driven by a Virtual Stick and a Hold Button.
    /// - Virtual Stick moves the virtual pen.
    /// - Hold Button determines whether ink is deposited (pen down) or moved freely (pen up).
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtDrawingCanvas : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RawImage _displayImage;
        [SerializeField] private RectTransform _penCursor;
        [SerializeField] private UrdtVirtualStick _stick;
        [SerializeField] private UrdtHoldButton _holdButton;

        [Header("Canvas Settings")]
        [SerializeField] private int _textureWidth = 320;
        [SerializeField] private int _textureHeight = 240;
        [SerializeField] private float _penSpeed = 140f; // pixels per second at max stick tilt
        [SerializeField] private int _brushRadius = 3;
        [SerializeField] private Color _inkColor = new Color(0.95f, 0.85f, 0.2f, 1f); // bright warm yellow
        [SerializeField] private Color _canvasBgColor = new Color(0.08f, 0.1f, 0.13f, 1f); // dark slate

        private Texture2D _texture;
        private Color32[] _pixels;
        private Vector2 _penPosition;
        private Vector2 _prevPenPosition;
        private bool _isInitialized = false;

        // Telemetry for URDT
        [SerializeField] private int _strokeCount = 0;
        [SerializeField] private float _totalDrawnLength = 0f;
        [SerializeField] private bool _isDrawing = false;
        private bool _wasDrawingLastFrame = false;
        private readonly List<Vector2> _drawnPoints = new List<Vector2>(256);

        public Vector2 PenPosition
        {
            get { return _penPosition; }
        }

        public int StrokeCount
        {
            get { return _strokeCount; }
        }

        public float TotalDrawnLength
        {
            get { return _totalDrawnLength; }
        }

        public bool IsDrawing
        {
            get { return _isDrawing; }
        }

        public int TextureWidth
        {
            get { return _textureWidth; }
        }

        public int TextureHeight
        {
            get { return _textureHeight; }
        }

        public IReadOnlyList<Vector2> DrawnPoints
        {
            get { return _drawnPoints; }
        }

        public UrdtVirtualStick Stick
        {
            get { return _stick; }
            set { _stick = value; }
        }

        [SerializeField] private Button _clearButton;

        public Button ClearButton
        {
            get { return _clearButton; }
            set { _clearButton = value; }
        }

        public UrdtHoldButton HoldButton
        {
            get { return _holdButton; }
            set { _holdButton = value; }
        }

        private void Awake()
        {
            InitializeCanvas();
            AutoBindClearButton();
        }

        private void OnEnable()
        {
            AutoBindClearButton();
        }

        public void AutoBindClearButton()
        {
            if (_clearButton == null)
            {
                var parent = transform.parent;
                if (parent != null)
                {
                    var buttons = parent.GetComponentsInChildren<Button>(true);
                    for (int i = 0; i < buttons.Length; i++)
                    {
                        if (buttons[i].name.IndexOf("Clear", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            _clearButton = buttons[i];
                            break;
                        }
                    }
                }
                if (_clearButton == null)
                {
                    var root = transform.root;
                    if (root != null)
                    {
                        var buttons = root.GetComponentsInChildren<Button>(true);
                        for (int i = 0; i < buttons.Length; i++)
                        {
                            if (buttons[i].name.IndexOf("Clear", StringComparison.OrdinalIgnoreCase) >= 0)
                            {
                                _clearButton = buttons[i];
                                break;
                            }
                        }
                    }
                }
            }

            if (_clearButton != null)
            {
                _clearButton.onClick.RemoveListener(ClearCanvas);
                _clearButton.onClick.AddListener(ClearCanvas);
            }
        }

        public void Configure(
            RawImage displayImage,
            RectTransform penCursor,
            UrdtVirtualStick stick,
            UrdtHoldButton holdButton,
            Button clearButton = null,
            int width = 320,
            int height = 240,
            float penSpeed = 140f)
        {
            _displayImage = displayImage;
            _penCursor = penCursor;
            _stick = stick;
            _holdButton = holdButton;
            _clearButton = clearButton;
            _textureWidth = width;
            _textureHeight = height;
            _penSpeed = penSpeed;
            InitializeCanvas();
            AutoBindClearButton();
        }

        public void InitializeCanvas()
        {
            if (_isInitialized && _texture != null)
            {
                return;
            }

            _texture = new Texture2D(_textureWidth, _textureHeight, TextureFormat.RGBA32, false);
            _texture.filterMode = FilterMode.Point;
            _pixels = new Color32[_textureWidth * _textureHeight];
            Color32 bg = _canvasBgColor;
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = bg;
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            if (_displayImage != null)
            {
                _displayImage.texture = _texture;
            }

            // Pen starts at bottom-left corner of the canvas suitable for drawing a house
            _penPosition = new Vector2(80f, 45f);
            _prevPenPosition = _penPosition;
            UpdateCursorVisual();
            _isInitialized = true;
        }

        public void ClearCanvas()
        {
            if (_texture == null)
            {
                return;
            }

            Color32 bg = _canvasBgColor;
            for (int i = 0; i < _pixels.Length; i++)
            {
                _pixels[i] = bg;
            }
            _texture.SetPixels32(_pixels);
            _texture.Apply(false);

            _strokeCount = 0;
            _totalDrawnLength = 0f;
            _drawnPoints.Clear();
            _wasDrawingLastFrame = false;
            _penPosition = new Vector2(80f, 45f);
            _prevPenPosition = _penPosition;
            UpdateCursorVisual();
        }

        private void Update()
        {
            if (!_isInitialized || _stick == null)
            {
                return;
            }

            Vector2 stickInput = _stick.InputVector;
            bool buttonHeld = _holdButton != null && _holdButton.IsHeld;
            _isDrawing = buttonHeld && stickInput.sqrMagnitude > 0.01f;

            if (_isDrawing && !_wasDrawingLastFrame)
            {
                _strokeCount++;
            }
            _wasDrawingLastFrame = _isDrawing;

            if (stickInput.sqrMagnitude > 0.01f)
            {
                Vector2 moveDelta = stickInput * (_penSpeed * Time.deltaTime);
                Vector2 newPos = _penPosition + moveDelta;
                newPos.x = Mathf.Clamp(newPos.x, 6f, _textureWidth - 6f);
                newPos.y = Mathf.Clamp(newPos.y, 6f, _textureHeight - 6f);

                if (buttonHeld)
                {
                    // Draw ink line from _penPosition to newPos
                    DrawLine(_penPosition, newPos, _inkColor, _brushRadius);
                    _texture.SetPixels32(_pixels);
                    _texture.Apply(false);

                    float segmentLength = Vector2.Distance(_penPosition, newPos);
                    _totalDrawnLength += segmentLength;
                    _drawnPoints.Add(newPos);
                }

                _prevPenPosition = _penPosition;
                _penPosition = newPos;
                UpdateCursorVisual();
            }
        }

        private void UpdateCursorVisual()
        {
            if (_penCursor == null || _displayImage == null)
            {
                return;
            }

            RectTransform canvasRect = _displayImage.rectTransform;
            float normX = _penPosition.x / _textureWidth - 0.5f;
            float normY = _penPosition.y / _textureHeight - 0.5f;
            _penCursor.anchoredPosition = new Vector2(normX * canvasRect.rect.width, normY * canvasRect.rect.height);
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, int radius)
        {
            int x0 = Mathf.RoundToInt(from.x);
            int y0 = Mathf.RoundToInt(from.y);
            int x1 = Mathf.RoundToInt(to.x);
            int y1 = Mathf.RoundToInt(to.y);

            int dx = Mathf.Abs(x1 - x0);
            int dy = Mathf.Abs(y1 - y0);
            int sx = x0 < x1 ? 1 : -1;
            int sy = y0 < y1 ? 1 : -1;
            int err = dx - dy;

            Color32 c = color;

            while (true)
            {
                DrawCircleBrush(x0, y0, radius, c);

                if (x0 == x1 && y0 == y1)
                {
                    break;
                }

                int e2 = 2 * err;
                if (e2 > -dy)
                {
                    err -= dy;
                    x0 += sx;
                }
                if (e2 < dx)
                {
                    err += dx;
                    y0 += sy;
                }
            }
        }

        private void DrawCircleBrush(int cx, int cy, int radius, Color32 color)
        {
            int r2 = radius * radius;
            for (int dy = -radius; dy <= radius; dy++)
            {
                int py = cy + dy;
                if (py < 0 || py >= _textureHeight)
                {
                    continue;
                }

                for (int dx = -radius; dx <= radius; dx++)
                {
                    int px = cx + dx;
                    if (px < 0 || px >= _textureWidth)
                    {
                        continue;
                    }

                    if (dx * dx + dy * dy <= r2)
                    {
                        _pixels[py * _textureWidth + px] = color;
                    }
                }
            }
        }
    }
}
