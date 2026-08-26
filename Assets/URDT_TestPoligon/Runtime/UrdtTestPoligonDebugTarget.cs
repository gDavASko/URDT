using KBP.URDT.Inspect;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace KBP.URDT.TestPoligon
{
    /// <summary>
    /// Structured debug surface for URDT test polygon targets.
    /// The autonomous agent must use these fields as truth before considering capture.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrdtTestPoligonDebugTarget : MonoBehaviour
    {
        private const string DEFAULT_COMMANDS = "inspect,query,click,double_click,multi_click";

        [SerializeField] private string _targetId = null;
        [SerializeField] private string _targetKind = "target";
        [SerializeField] private string _module = "ui";
        [SerializeField] private string _activeWindow = "window_main_menu";
        [SerializeField] private string _activeModule = "launcher";
        [SerializeField] private string _supportedCommands = DEFAULT_COMMANDS;
        [SerializeField] private RectTransform _rectTransform = null;
        [SerializeField] private Canvas _canvas = null;
        [SerializeField] private Selectable _selectable = null;
        [SerializeField] private Toggle _toggle = null;
        [SerializeField] private Slider _slider = null;
        [SerializeField] private TMP_InputField _inputField = null;
        [SerializeField] private TMP_Dropdown _dropdown = null;
        [SerializeField] private ScrollRect _scrollRect = null;

        private int _interactionCount;
        private string _lastInputAction = "none";
        private string _lastResult = "idle";
        private string _blockedReason = string.Empty;
        private readonly Vector3[] _worldCorners = new Vector3[4];

        [TestInspectable]
        public string TargetId
        {
            get { return string.IsNullOrEmpty(_targetId) ? name : _targetId; }
        }

        [TestInspectable]
        public string TargetKind
        {
            get { return _targetKind; }
        }

        [TestInspectable]
        public string Module
        {
            get { return _module; }
        }

        [TestInspectable]
        public string ActiveWindow
        {
            get { return _activeWindow; }
        }

        [TestInspectable]
        public string ActiveModule
        {
            get { return _activeModule; }
        }

        [TestInspectable]
        public string SupportedCommands
        {
            get { return _supportedCommands; }
        }

        [TestInspectable]
        public int InteractionCount
        {
            get { return _interactionCount; }
        }

        [TestInspectable]
        public string LastInputAction
        {
            get { return _lastInputAction; }
        }

        [TestInspectable]
        public string LastResult
        {
            get { return _lastResult; }
        }

        [TestInspectable]
        public string BlockedReason
        {
            get { return _blockedReason; }
        }

        [TestInspectable]
        public bool IsVisible
        {
            get { return gameObject.activeInHierarchy; }
        }

        [TestInspectable]
        public bool IsInteractable
        {
            get { return _selectable == null || _selectable.interactable; }
        }

        [TestInspectable]
        public Rect ScreenRect
        {
            get { return GetScreenRect(); }
        }

        [TestInspectable]
        public Vector2 ScreenCenter
        {
            get { return GetScreenCenter(); }
        }

        [TestInspectable]
        public Vector2 RectSize
        {
            get { return _rectTransform != null ? _rectTransform.rect.size : Vector2.zero; }
        }

        [TestInspectable]
        public bool ToggleValue
        {
            get { return _toggle != null && _toggle.isOn; }
        }

        [TestInspectable]
        public float SliderValue
        {
            get { return _slider != null ? _slider.value : 0f; }
        }

        [TestInspectable]
        public string InputValue
        {
            get { return _inputField != null ? _inputField.text : string.Empty; }
        }

        [TestInspectable]
        public int DropdownValue
        {
            get { return _dropdown != null ? _dropdown.value : -1; }
        }

        [TestInspectable]
        public string DropdownLabel
        {
            get
            {
                if (_dropdown == null || _dropdown.options == null || _dropdown.value < 0 || _dropdown.value >= _dropdown.options.Count)
                {
                    return string.Empty;
                }

                TMP_Dropdown.OptionData option = _dropdown.options[_dropdown.value];
                return option != null ? option.text : string.Empty;
            }
        }

        [TestInspectable]
        public string DropdownOptions
        {
            get
            {
                if (_dropdown == null || _dropdown.options == null)
                {
                    return string.Empty;
                }

                System.Text.StringBuilder builder = new System.Text.StringBuilder();
                for (int i = 0; i < _dropdown.options.Count; i++)
                {
                    if (i > 0)
                    {
                        builder.Append('|');
                    }

                    TMP_Dropdown.OptionData option = _dropdown.options[i];
                    builder.Append(option != null ? option.text : string.Empty);
                }

                return builder.ToString();
            }
        }

        [TestInspectable]
        public Vector2 ScrollPosition
        {
            get
            {
                return _scrollRect != null
                    ? new Vector2(_scrollRect.horizontalNormalizedPosition, _scrollRect.verticalNormalizedPosition)
                    : Vector2.zero;
            }
        }

        [TestInspectable]
        public float ScrollContentHeight
        {
            get { return _scrollRect != null && _scrollRect.content != null ? _scrollRect.content.rect.height : 0f; }
        }

        [TestInspectable]
        public float ScrollViewportHeight
        {
            get { return _scrollRect != null && _scrollRect.viewport != null ? _scrollRect.viewport.rect.height : 0f; }
        }

        public void Configure(
            string targetId,
            string module,
            string supportedCommands,
            RectTransform rectTransform,
            Canvas canvas,
            Selectable selectable = null,
            Toggle toggle = null,
            Slider slider = null,
            TMP_InputField inputField = null,
            TMP_Dropdown dropdown = null,
            ScrollRect scrollRect = null)
        {
            Configure(
                targetId,
                "target",
                "window_main_menu",
                string.IsNullOrEmpty(module) ? "ui" : module,
                module,
                supportedCommands,
                rectTransform,
                canvas,
                selectable,
                toggle,
                slider,
                inputField,
                dropdown,
                scrollRect);
        }

        public void Configure(
            string targetId,
            string targetKind,
            string activeWindow,
            string activeModule,
            string module,
            string supportedCommands,
            RectTransform rectTransform,
            Canvas canvas,
            Selectable selectable = null,
            Toggle toggle = null,
            Slider slider = null,
            TMP_InputField inputField = null,
            TMP_Dropdown dropdown = null,
            ScrollRect scrollRect = null)
        {
            _targetId = targetId;
            _targetKind = string.IsNullOrEmpty(targetKind) ? "target" : targetKind;
            _module = string.IsNullOrEmpty(module) ? "ui" : module;
            _activeWindow = string.IsNullOrEmpty(activeWindow) ? "window_main_menu" : activeWindow;
            _activeModule = string.IsNullOrEmpty(activeModule) ? _module : activeModule;
            _supportedCommands = string.IsNullOrEmpty(supportedCommands) ? DEFAULT_COMMANDS : supportedCommands;
            _rectTransform = rectTransform;
            _canvas = canvas;
            _selectable = selectable;
            _toggle = toggle;
            _slider = slider;
            _inputField = inputField;
            _dropdown = dropdown;
            _scrollRect = scrollRect;
        }

        public void SetContext(string activeWindow, string activeModule)
        {
            _activeWindow = string.IsNullOrEmpty(activeWindow) ? _activeWindow : activeWindow;
            _activeModule = string.IsNullOrEmpty(activeModule) ? _activeModule : activeModule;
        }

        public void RecordInput(string action, string result, string blockedReason = "")
        {
            _interactionCount++;
            _lastInputAction = string.IsNullOrEmpty(action) ? "unknown" : action;
            _lastResult = string.IsNullOrEmpty(result) ? "ok" : result;
            _blockedReason = blockedReason ?? string.Empty;
        }

        public void ResetState()
        {
            _interactionCount = 0;
            _lastInputAction = "reset_state";
            _lastResult = "reset";
            _blockedReason = string.Empty;
        }

        private void Awake()
        {
            CacheReferences();
        }

        private void OnValidate()
        {
            CacheReferences();
        }

        private void CacheReferences()
        {
            if (_rectTransform == null)
            {
                TryGetComponent(out _rectTransform);
            }

            if (_canvas == null)
            {
                _canvas = GetComponentInParent<Canvas>();
            }

            if (_selectable == null)
            {
                TryGetComponent(out _selectable);
            }

            if (_toggle == null)
            {
                TryGetComponent(out _toggle);
            }

            if (_slider == null)
            {
                TryGetComponent(out _slider);
            }

            if (_inputField == null)
            {
                TryGetComponent(out _inputField);
            }

            if (_dropdown == null)
            {
                TryGetComponent(out _dropdown);
            }

            if (_scrollRect == null)
            {
                TryGetComponent(out _scrollRect);
            }
        }

        private Rect GetScreenRect()
        {
            if (_rectTransform == null)
            {
                return new Rect(Vector2.zero, Vector2.zero);
            }

            Canvas.ForceUpdateCanvases();
            Camera camera = GetCanvasCamera();
            _rectTransform.GetWorldCorners(_worldCorners);

            Vector2 min = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[0]);
            Vector2 max = min;

            for (int i = 1; i < _worldCorners.Length; i++)
            {
                Vector2 point = RectTransformUtility.WorldToScreenPoint(camera, _worldCorners[i]);
                min = Vector2.Min(min, point);
                max = Vector2.Max(max, point);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private Vector2 GetScreenCenter()
        {
            if (_rectTransform == null)
            {
                return Vector2.zero;
            }

            Canvas.ForceUpdateCanvases();
            Vector3 worldCenter = _rectTransform.TransformPoint(_rectTransform.rect.center);
            return RectTransformUtility.WorldToScreenPoint(GetCanvasCamera(), worldCenter);
        }

        private Camera GetCanvasCamera()
        {
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                return _canvas.worldCamera;
            }

            return null;
        }
    }
}
