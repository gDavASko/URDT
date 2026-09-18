using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for Button and clickable UI elements.
    /// Single Responsibility: tracks button interactability, clicks, and state transitions.
    /// Free of any toggle, slider, input, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiButtonTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_BUTTON_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click,
            UrdtCommandType.DoubleClick,
            UrdtCommandType.MultiClick
        };

        [Header("Button Component")]
        [Tooltip("Button component bound to this beacon (auto-detected if null).")]
        [SerializeField] private Selectable _selectable = null;

        [Tooltip("Result string recorded when clicked. Defaults to 'clicked'.")]
        [SerializeField] private string _clickResult = "clicked";

        public string ClickResult
        {
            get { return _clickResult; }
            set { _clickResult = value; }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _selectable == null || _selectable.interactable; }
        }

        public Selectable Selectable
        {
            get { return _selectable; }
            set { _selectable = value; }
        }

        public Button Button
        {
            get { return _selectable as Button; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindSelectable();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            if (_selectable is Button button)
            {
                button.onClick.AddListener(OnButtonClicked);
            }
        }

        protected virtual void OnDisable()
        {
            if (_selectable is Button button)
            {
                button.onClick.RemoveListener(OnButtonClicked);
            }
        }

        private void OnButtonClicked()
        {
            RecordInput("click", string.IsNullOrEmpty(_clickResult) ? "clicked" : _clickResult);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindSelectable();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindSelectable();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "button";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_BUTTON_COMMANDS);
            }
        }

        public void AutoBindSelectable()
        {
            if (_selectable == null)
            {
                _selectable = GetComponent<Button>() ?? GetComponent<Selectable>();
            }
        }

        public void ConfigureButton(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            Selectable selectable = null,
            RectTransform rectTransform = null,
            Canvas canvas = null,
            string clickResult = null)
        {
            base.ConfigureUi(
                targetId,
                "button",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_BUTTON_COMMANDS,
                rectTransform,
                canvas);

            _selectable = selectable != null ? selectable : _selectable;
            if (!string.IsNullOrEmpty(clickResult))
            {
                _clickResult = clickResult;
            }
            AutoBindSelectable();
        }

        protected override string InferTargetKind()
        {
            return "button";
        }
    }
}
