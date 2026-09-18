using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for Toggle elements.
    /// Single Responsibility: tracks toggle state (isOn), interactability, and toggle click commands.
    /// Free of any button, slider, input, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiToggleTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_TOGGLE_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click
        };

        [Header("Toggle Component")]
        [Tooltip("Toggle component bound to this beacon (auto-detected if null).")]
        [SerializeField] private Toggle _toggle = null;

        [TestInspectable]
        public bool ToggleValue
        {
            get { return _toggle != null && _toggle.isOn; }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _toggle == null || _toggle.interactable; }
        }

        public Toggle Toggle
        {
            get { return _toggle; }
            set { _toggle = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindToggle();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.AddListener(OnToggleChanged);
            }
        }

        protected virtual void OnDisable()
        {
            if (_toggle != null)
            {
                _toggle.onValueChanged.RemoveListener(OnToggleChanged);
            }
        }

        private void OnToggleChanged(bool val)
        {
            RecordInput("click", val ? "toggle_on" : "toggle_off");
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindToggle();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindToggle();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "toggle";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_TOGGLE_COMMANDS);
            }
        }

        public void AutoBindToggle()
        {
            if (_toggle == null)
            {
                _toggle = GetComponent<Toggle>();
            }
        }

        public void ConfigureToggle(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            Toggle toggle = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "toggle",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_TOGGLE_COMMANDS,
                rectTransform,
                canvas);

            _toggle = toggle != null ? toggle : _toggle;
            AutoBindToggle();
        }

        protected override string InferTargetKind()
        {
            return "toggle";
        }
    }
}
