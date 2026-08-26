using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.TestPoligon
{
    /// <summary>
    /// Captures launcher and UI state changes for the first URDT test polygon wave.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrdtUiTestPoligonController : MonoBehaviour
    {
        [SerializeField] private UrdtTestPoligonDebugTarget[] _targets = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _openUiTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _open2DTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _open3DTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _openIntegrationTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _runAllTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _primaryButtonTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _toggleTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _sliderTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _inputTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _dropdownTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _scrollTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _modalTarget = null;
        [SerializeField] private UrdtTestPoligonDebugTarget _modalCloseTarget = null;
        [SerializeField] private GameObject _mainMenuWindow = null;
        [SerializeField] private GameObject _uiSuiteWindow = null;
        [SerializeField] private GameObject _world2DWindow = null;
        [SerializeField] private GameObject _world3DWindow = null;
        [SerializeField] private GameObject _integrationWindow = null;
        [SerializeField] private Button _openUiButton = null;
        [SerializeField] private Button _open2DButton = null;
        [SerializeField] private Button _open3DButton = null;
        [SerializeField] private Button _openIntegrationButton = null;
        [SerializeField] private Button _runAllButton = null;
        [SerializeField] private Button _uiBackButton = null;
        [SerializeField] private Button _world2DBackButton = null;
        [SerializeField] private Button _world3DBackButton = null;
        [SerializeField] private Button _integrationBackButton = null;
        [SerializeField] private Button _primaryButton = null;
        [SerializeField] private Button _resetButton = null;
        [SerializeField] private Button _modalOpenButton = null;
        [SerializeField] private Button _modalCloseButton = null;
        [SerializeField] private Toggle _toggle = null;
        [SerializeField] private Slider _slider = null;
        [SerializeField] private TMP_InputField _inputField = null;
        [SerializeField] private TMP_Dropdown _dropdown = null;
        [SerializeField] private ScrollRect _scrollRect = null;
        [SerializeField] private GameObject _modalWindow = null;
        [SerializeField] private TMP_Text _statusText = null;

        private string _activeWindow = "window_main_menu";
        private string _activeModule = "launcher";
        private readonly List<Transform> _randomizedUiControls = new List<Transform>(7);
        private int _randomizationSeed;

        public void Configure(
            UrdtTestPoligonDebugTarget[] targets,
            UrdtTestPoligonDebugTarget openUiTarget,
            UrdtTestPoligonDebugTarget open2DTarget,
            UrdtTestPoligonDebugTarget open3DTarget,
            UrdtTestPoligonDebugTarget openIntegrationTarget,
            UrdtTestPoligonDebugTarget runAllTarget,
            UrdtTestPoligonDebugTarget primaryButtonTarget,
            UrdtTestPoligonDebugTarget toggleTarget,
            UrdtTestPoligonDebugTarget sliderTarget,
            UrdtTestPoligonDebugTarget inputTarget,
            UrdtTestPoligonDebugTarget dropdownTarget,
            UrdtTestPoligonDebugTarget scrollTarget,
            UrdtTestPoligonDebugTarget modalTarget,
            UrdtTestPoligonDebugTarget modalCloseTarget,
            GameObject mainMenuWindow,
            GameObject uiSuiteWindow,
            GameObject world2DWindow,
            GameObject world3DWindow,
            GameObject integrationWindow,
            Button openUiButton,
            Button open2DButton,
            Button open3DButton,
            Button openIntegrationButton,
            Button runAllButton,
            Button uiBackButton,
            Button world2DBackButton,
            Button world3DBackButton,
            Button integrationBackButton,
            Button primaryButton,
            Button resetButton,
            Button modalOpenButton,
            Button modalCloseButton,
            Toggle toggle,
            Slider slider,
            TMP_InputField inputField,
            TMP_Dropdown dropdown,
            ScrollRect scrollRect,
            GameObject modalWindow,
            TMP_Text statusText)
        {
            _targets = targets;
            _openUiTarget = openUiTarget;
            _open2DTarget = open2DTarget;
            _open3DTarget = open3DTarget;
            _openIntegrationTarget = openIntegrationTarget;
            _runAllTarget = runAllTarget;
            _primaryButtonTarget = primaryButtonTarget;
            _toggleTarget = toggleTarget;
            _sliderTarget = sliderTarget;
            _inputTarget = inputTarget;
            _dropdownTarget = dropdownTarget;
            _scrollTarget = scrollTarget;
            _modalTarget = modalTarget;
            _modalCloseTarget = modalCloseTarget;
            _mainMenuWindow = mainMenuWindow;
            _uiSuiteWindow = uiSuiteWindow;
            _world2DWindow = world2DWindow;
            _world3DWindow = world3DWindow;
            _integrationWindow = integrationWindow;
            _openUiButton = openUiButton;
            _open2DButton = open2DButton;
            _open3DButton = open3DButton;
            _openIntegrationButton = openIntegrationButton;
            _runAllButton = runAllButton;
            _uiBackButton = uiBackButton;
            _world2DBackButton = world2DBackButton;
            _world3DBackButton = world3DBackButton;
            _integrationBackButton = integrationBackButton;
            _primaryButton = primaryButton;
            _resetButton = resetButton;
            _modalOpenButton = modalOpenButton;
            _modalCloseButton = modalCloseButton;
            _toggle = toggle;
            _slider = slider;
            _inputField = inputField;
            _dropdown = dropdown;
            _scrollRect = scrollRect;
            _modalWindow = modalWindow;
            _statusText = statusText;
        }

        private void OnEnable()
        {
            AddListeners();
        }

        private void Start()
        {
            RandomizeUiControls();
            RandomizeDropdownOptions(new System.Random(_randomizationSeed));
            ResetUiState();
        }

        private void OnDisable()
        {
            RemoveListeners();
        }

        private void AddListeners()
        {
            if (_openUiButton != null)
            {
                _openUiButton.onClick.AddListener(OpenUiSuite);
            }

            if (_open2DButton != null)
            {
                _open2DButton.onClick.AddListener(Open2DSuite);
            }

            if (_open3DButton != null)
            {
                _open3DButton.onClick.AddListener(Open3DSuite);
            }

            if (_openIntegrationButton != null)
            {
                _openIntegrationButton.onClick.AddListener(OpenIntegrationSuite);
            }

            if (_runAllButton != null)
            {
                _runAllButton.onClick.AddListener(RunAllSuites);
            }

            if (_uiBackButton != null)
            {
                _uiBackButton.onClick.AddListener(ShowMainMenu);
            }

            if (_world2DBackButton != null)
            {
                _world2DBackButton.onClick.AddListener(ShowMainMenu);
            }

            if (_world3DBackButton != null)
            {
                _world3DBackButton.onClick.AddListener(ShowMainMenu);
            }

            if (_integrationBackButton != null)
            {
                _integrationBackButton.onClick.AddListener(ShowMainMenu);
            }

            if (_primaryButton != null)
            {
                _primaryButton.onClick.AddListener(OnPrimaryButton);
            }

            if (_resetButton != null)
            {
                _resetButton.onClick.AddListener(ResetUiState);
            }

            if (_modalOpenButton != null)
            {
                _modalOpenButton.onClick.AddListener(OpenModal);
            }

            if (_modalCloseButton != null)
            {
                _modalCloseButton.onClick.AddListener(CloseModal);
            }

            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleChanged);
            }

            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnSliderChanged);
            }

            if (_inputField != null)
            {
                _inputField.onValueChanged.AddListener(OnInputChanged);
            }

            if (_dropdown != null)
            {
                _dropdown.onValueChanged.AddListener(OnDropdownChanged);
            }

            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            }
        }

        private void RemoveListeners()
        {
            if (_openUiButton != null)
            {
                _openUiButton.onClick.RemoveListener(OpenUiSuite);
            }

            if (_open2DButton != null)
            {
                _open2DButton.onClick.RemoveListener(Open2DSuite);
            }

            if (_open3DButton != null)
            {
                _open3DButton.onClick.RemoveListener(Open3DSuite);
            }

            if (_openIntegrationButton != null)
            {
                _openIntegrationButton.onClick.RemoveListener(OpenIntegrationSuite);
            }

            if (_runAllButton != null)
            {
                _runAllButton.onClick.RemoveListener(RunAllSuites);
            }

            if (_uiBackButton != null)
            {
                _uiBackButton.onClick.RemoveListener(ShowMainMenu);
            }

            if (_world2DBackButton != null)
            {
                _world2DBackButton.onClick.RemoveListener(ShowMainMenu);
            }

            if (_world3DBackButton != null)
            {
                _world3DBackButton.onClick.RemoveListener(ShowMainMenu);
            }

            if (_integrationBackButton != null)
            {
                _integrationBackButton.onClick.RemoveListener(ShowMainMenu);
            }

            if (_primaryButton != null)
            {
                _primaryButton.onClick.RemoveListener(OnPrimaryButton);
            }

            if (_resetButton != null)
            {
                _resetButton.onClick.RemoveListener(ResetUiState);
            }

            if (_modalOpenButton != null)
            {
                _modalOpenButton.onClick.RemoveListener(OpenModal);
            }

            if (_modalCloseButton != null)
            {
                _modalCloseButton.onClick.RemoveListener(CloseModal);
            }

            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
            }

            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnSliderChanged);
            }

            if (_inputField != null)
            {
                _inputField.onValueChanged.RemoveListener(OnInputChanged);
            }

            if (_dropdown != null)
            {
                _dropdown.onValueChanged.RemoveListener(OnDropdownChanged);
            }

            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            }
        }

        private void OpenUiSuite()
        {
            RandomizeUiControls();
            ShowWindow(_uiSuiteWindow, "window_ui_suite", "ui");
            Record(_openUiTarget, "click", "open_ui_suite");
        }

        private void Open2DSuite()
        {
            ShowWindow(_world2DWindow, "window_2d_suite", "2d");
            Record(_open2DTarget, "click", "open_2d_suite");
        }

        private void Open3DSuite()
        {
            ShowWindow(_world3DWindow, "window_3d_suite", "3d");
            Record(_open3DTarget, "click", "open_3d_suite");
        }

        private void OpenIntegrationSuite()
        {
            ShowWindow(_integrationWindow, "window_integration_suite", "integration");
            Record(_openIntegrationTarget, "click", "open_integration_suite");
        }

        private void RunAllSuites()
        {
            Record(_runAllTarget, "click", "run_all_requested");
        }

        private void ShowMainMenu()
        {
            ShowWindow(_mainMenuWindow, "window_main_menu", "launcher");
            Record(null, "click", "open_main_menu");
        }

        private void OnPrimaryButton()
        {
            Record(_primaryButtonTarget, "click", "primary_button_clicked");
        }

        private void OnToggleChanged(bool value)
        {
            Record(_toggleTarget, "click", value ? "toggle_on" : "toggle_off");
        }

        private void OnSliderChanged(float value)
        {
            Record(_sliderTarget, "drag", "slider_value=" + value.ToString("0.00"));
        }

        private void OnInputChanged(string value)
        {
            Record(_inputTarget, "keyboard", "input_length=" + value.Length);
        }

        private void OnDropdownChanged(int value)
        {
            Record(_dropdownTarget, "click", "dropdown_value=" + GetDropdownLabel() + ";index=" + value);
        }

        private void OnScrollChanged(Vector2 value)
        {
            Record(_scrollTarget, "scroll", "scroll_y=" + value.y.ToString("0.00"));
        }

        private void OpenModal()
        {
            if (_modalWindow != null)
            {
                _modalWindow.SetActive(true);
            }

            Record(_modalTarget, "click", "modal_open");
        }

        private void CloseModal()
        {
            if (_modalWindow != null)
            {
                _modalWindow.SetActive(false);
            }

            Record(_modalCloseTarget, "click", "modal_closed");
        }

        private void ResetUiState()
        {
            bool keepUiSuiteOpen = _uiSuiteWindow != null && _uiSuiteWindow.activeSelf;

            if (_toggle != null)
            {
                _toggle.SetIsOnWithoutNotify(false);
            }

            if (_slider != null)
            {
                _slider.SetValueWithoutNotify(0.25f);
            }

            if (_inputField != null)
            {
                _inputField.SetTextWithoutNotify(string.Empty);
            }

            if (_dropdown != null)
            {
                _dropdown.SetValueWithoutNotify(0);
                _dropdown.RefreshShownValue();
            }

            if (_scrollRect != null)
            {
                _scrollRect.verticalNormalizedPosition = 1f;
            }

            if (_modalWindow != null)
            {
                _modalWindow.SetActive(false);
            }

            if (_targets != null)
            {
                for (int i = 0; i < _targets.Length; i++)
                {
                    if (_targets[i] != null)
                    {
                        _targets[i].ResetState();
                    }
                }
            }

            if (keepUiSuiteOpen)
            {
                ShowWindow(_uiSuiteWindow, "window_ui_suite", "ui");
            }
            else
            {
                ShowWindow(_mainMenuWindow, "window_main_menu", "launcher");
            }

            SetStatus("ready");
        }

        private void RandomizeUiControls()
        {
            _randomizationSeed = Environment.TickCount ^ GetInstanceID();
            System.Random random = new System.Random(_randomizationSeed);
            RandomizeUiControlOrder(random);
            Canvas.ForceUpdateCanvases();
        }

        private void RandomizeUiControlOrder(System.Random random)
        {
            if (_primaryButton == null)
            {
                return;
            }

            Transform content = _primaryButton.transform.parent;
            if (content == null)
            {
                return;
            }

            _randomizedUiControls.Clear();
            AddUiControlIfChildOfContent(_primaryButton != null ? _primaryButton.transform : null, content);
            AddUiControlIfChildOfContent(_dropdown != null ? _dropdown.transform : null, content);
            AddUiControlIfChildOfContent(_toggle != null ? _toggle.transform : null, content);
            AddUiControlIfChildOfContent(_slider != null ? _slider.transform : null, content);
            AddUiControlIfChildOfContent(_inputField != null ? _inputField.transform : null, content);
            AddUiControlIfChildOfContent(_scrollRect != null ? _scrollRect.transform : null, content);
            AddUiControlIfChildOfContent(_resetButton != null ? _resetButton.transform : null, content);

            Shuffle(_randomizedUiControls, random);
            for (int i = 0; i < _randomizedUiControls.Count; i++)
            {
                _randomizedUiControls[i].SetSiblingIndex(i);
            }
        }

        private void RandomizeDropdownOptions(System.Random random)
        {
            if (_dropdown == null || _dropdown.options == null)
            {
                return;
            }

            Shuffle(_dropdown.options, random);
            _dropdown.SetValueWithoutNotify(0);
            _dropdown.RefreshShownValue();
        }

        private void AddUiControlIfChildOfContent(Transform control, Transform content)
        {
            if (control != null && control.parent == content)
            {
                _randomizedUiControls.Add(control);
            }
        }

        private static void Shuffle<T>(IList<T> values, System.Random random)
        {
            for (int i = values.Count - 1; i > 0; i--)
            {
                int swapIndex = random.Next(i + 1);
                T value = values[i];
                values[i] = values[swapIndex];
                values[swapIndex] = value;
            }
        }

        private string GetDropdownLabel()
        {
            if (_dropdown == null || _dropdown.options == null || _dropdown.value < 0 || _dropdown.value >= _dropdown.options.Count)
            {
                return string.Empty;
            }

            TMP_Dropdown.OptionData option = _dropdown.options[_dropdown.value];
            return option != null ? option.text : string.Empty;
        }

        private void ShowWindow(GameObject visibleWindow, string activeWindow, string activeModule)
        {
            SetWindowActive(_mainMenuWindow, visibleWindow);
            SetWindowActive(_uiSuiteWindow, visibleWindow);
            SetWindowActive(_world2DWindow, visibleWindow);
            SetWindowActive(_world3DWindow, visibleWindow);
            SetWindowActive(_integrationWindow, visibleWindow);

            _activeWindow = string.IsNullOrEmpty(activeWindow) ? "window_main_menu" : activeWindow;
            _activeModule = string.IsNullOrEmpty(activeModule) ? "launcher" : activeModule;

            if (_targets != null)
            {
                for (int i = 0; i < _targets.Length; i++)
                {
                    if (_targets[i] != null)
                    {
                        _targets[i].SetContext(_activeWindow, _activeModule);
                    }
                }
            }

            SetStatus(_activeWindow);
        }

        private static void SetWindowActive(GameObject window, GameObject visibleWindow)
        {
            if (window != null)
            {
                window.SetActive(window == visibleWindow);
            }
        }

        private void Record(UrdtTestPoligonDebugTarget target, string action, string result)
        {
            if (target != null)
            {
                target.RecordInput(action, result);
            }

            SetStatus(result);
        }

        private void SetStatus(string value)
        {
            if (_statusText != null)
            {
                _statusText.text = value;
            }
        }
    }
}
