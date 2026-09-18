using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for text input fields (supports both uGUI InputField and TextMeshPro TMP_InputField).
    /// Single Responsibility: tracks text value, focus, keyboard input events, and interactability.
    /// Free of any button, toggle, slider, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiInputTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_INPUT_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click,
            UrdtCommandType.TypeText,
            UrdtCommandType.KeyPress
        };

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("Input Component")]
        [Tooltip("InputField or TMP_InputField component bound to this beacon (auto-detected if null).")]
        [SerializeField] private Selectable _inputSelectable = null;

        [TestInspectable]
        public string InputValue
        {
            get { return ReadInputText(); }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _inputSelectable == null || _inputSelectable.interactable; }
        }

        public Selectable InputSelectable
        {
            get { return _inputSelectable; }
            set { _inputSelectable = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindInput();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            HookInput(true);
        }

        protected virtual void OnDisable()
        {
            HookInput(false);
        }

        private void HookInput(bool subscribe)
        {
            if (_inputSelectable == null)
            {
                AutoBindInput();
            }

            if (_inputSelectable == null)
            {
                return;
            }

            PropertyInfo prop = _inputSelectable.GetType().GetProperty("onValueChanged", FLAGS);
            if (prop != null && prop.CanRead)
            {
                var evt = prop.GetValue(_inputSelectable) as UnityEngine.Events.UnityEvent<string>;
                if (evt != null)
                {
                    if (subscribe)
                    {
                        evt.AddListener(OnInputStringChanged);
                    }
                    else
                    {
                        evt.RemoveListener(OnInputStringChanged);
                    }
                }
            }
        }

        private void OnInputStringChanged(string val)
        {
            RecordInput("keyboard", "input_length=" + (val != null ? val.Length : 0));
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindInput();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindInput();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "input";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_INPUT_COMMANDS);
            }
        }

        public void AutoBindInput()
        {
            if (_inputSelectable == null)
            {
                Selectable[] selectables = GetComponents<Selectable>();
                for (int i = 0; i < selectables.Length; i++)
                {
                    if (selectables[i] == null) continue;
                    string typeName = selectables[i].GetType().Name;
                    if (typeName == "TMP_InputField" || typeName == "InputField")
                    {
                        _inputSelectable = selectables[i];
                        break;
                    }
                }

                if (_inputSelectable == null && selectables.Length > 0)
                {
                    _inputSelectable = selectables[0];
                }
            }
        }

        public void ConfigureInput(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            Selectable inputSelectable = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "input",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_INPUT_COMMANDS,
                rectTransform,
                canvas);

            _inputSelectable = inputSelectable != null ? inputSelectable : _inputSelectable;
            AutoBindInput();
        }

        protected override string InferTargetKind()
        {
            return "input";
        }

        private string ReadInputText()
        {
            if (_inputSelectable == null)
            {
                return string.Empty;
            }

            PropertyInfo prop = _inputSelectable.GetType().GetProperty("text", FLAGS);
            if (prop != null && prop.CanRead)
            {
                return prop.GetValue(_inputSelectable) as string ?? string.Empty;
            }

            return string.Empty;
        }
    }
}
