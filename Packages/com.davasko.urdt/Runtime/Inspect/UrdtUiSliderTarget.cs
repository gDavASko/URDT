using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for Slider elements.
    /// Single Responsibility: tracks slider value (float), interactability, and drag/swipe input.
    /// Free of any button, toggle, input, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiSliderTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_SLIDER_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Drag,
            UrdtCommandType.PressMove,
            UrdtCommandType.Swipe
        };

        [Header("Slider Component")]
        [Tooltip("Slider component bound to this beacon (auto-detected if null).")]
        [SerializeField] private Slider _slider = null;

        [TestInspectable]
        public float SliderValue
        {
            get { return _slider != null ? _slider.value : 0f; }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _slider == null || _slider.interactable; }
        }

        public Slider Slider
        {
            get { return _slider; }
            set { _slider = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindSlider();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.AddListener(OnSliderChanged);
            }
        }

        protected virtual void OnDisable()
        {
            if (_slider != null)
            {
                _slider.onValueChanged.RemoveListener(OnSliderChanged);
            }
        }

        private void OnSliderChanged(float val)
        {
            RecordInput("drag", "slider_value=" + val.ToString("0.00"));
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindSlider();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindSlider();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "slider";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_SLIDER_COMMANDS);
            }
        }

        public void AutoBindSlider()
        {
            if (_slider == null)
            {
                _slider = GetComponent<Slider>();
            }
        }

        public void ConfigureSlider(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            Slider slider = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "slider",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_SLIDER_COMMANDS,
                rectTransform,
                canvas);

            _slider = slider != null ? slider : _slider;
            AutoBindSlider();
        }

        protected override string InferTargetKind()
        {
            return "slider";
        }
    }
}
