using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for dropdown selection elements (supports both uGUI Dropdown and TextMeshPro TMP_Dropdown).
    /// Single Responsibility: tracks selected index, selected label, options list, and selection interaction.
    /// Free of any button, toggle, slider, input, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiDropdownTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_DROPDOWN_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Click
        };

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("Dropdown Component")]
        [Tooltip("Dropdown or TMP_Dropdown component bound to this beacon (auto-detected if null).")]
        [SerializeField] private Selectable _dropdownSelectable = null;

        [TestInspectable]
        public int DropdownValue
        {
            get { return ReadDropdownValue(); }
        }

        [TestInspectable]
        public string DropdownLabel
        {
            get { return ReadDropdownLabel(); }
        }

        [TestInspectable]
        public string DropdownOptions
        {
            get { return ReadDropdownOptions(); }
        }

        [TestInspectable]
        public override bool IsInteractable
        {
            get { return _dropdownSelectable == null || _dropdownSelectable.interactable; }
        }

        public Selectable DropdownSelectable
        {
            get { return _dropdownSelectable; }
            set { _dropdownSelectable = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindDropdown();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            HookDropdown(true);
        }

        protected virtual void OnDisable()
        {
            HookDropdown(false);
        }

        private void HookDropdown(bool subscribe)
        {
            if (_dropdownSelectable == null) return;
            PropertyInfo prop = _dropdownSelectable.GetType().GetProperty("onValueChanged", FLAGS);
            if (prop != null && prop.CanRead)
            {
                var evt = prop.GetValue(_dropdownSelectable) as UnityEngine.Events.UnityEvent<int>;
                if (evt != null)
                {
                    if (subscribe) evt.AddListener(OnDropdownIntChanged);
                    else evt.RemoveListener(OnDropdownIntChanged);
                }
            }
        }

        private void OnDropdownIntChanged(int val)
        {
            RecordInput("click", "dropdown_value=" + ReadDropdownLabel() + ";index=" + val);
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindDropdown();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindDropdown();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "dropdown";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_DROPDOWN_COMMANDS);
            }
        }

        public void AutoBindDropdown()
        {
            if (_dropdownSelectable == null)
            {
                Selectable[] selectables = GetComponents<Selectable>();
                for (int i = 0; i < selectables.Length; i++)
                {
                    if (selectables[i] == null) continue;
                    string typeName = selectables[i].GetType().Name;
                    if (typeName == "TMP_Dropdown" || typeName == "Dropdown")
                    {
                        _dropdownSelectable = selectables[i];
                        break;
                    }
                }

                if (_dropdownSelectable == null && selectables.Length > 0)
                {
                    _dropdownSelectable = selectables[0];
                }
            }
        }

        public void ConfigureDropdown(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            Selectable dropdownSelectable = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "dropdown",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_DROPDOWN_COMMANDS,
                rectTransform,
                canvas);

            _dropdownSelectable = dropdownSelectable != null ? dropdownSelectable : _dropdownSelectable;
            AutoBindDropdown();
        }

        protected override string InferTargetKind()
        {
            return "dropdown";
        }

        private int ReadDropdownValue()
        {
            if (_dropdownSelectable == null)
            {
                return -1;
            }

            PropertyInfo prop = _dropdownSelectable.GetType().GetProperty("value", FLAGS);
            if (prop != null && prop.CanRead)
            {
                object val = prop.GetValue(_dropdownSelectable);
                if (val is int intVal) return intVal;
            }

            return -1;
        }

        private string ReadDropdownLabel()
        {
            if (_dropdownSelectable == null)
            {
                return string.Empty;
            }

            int index = ReadDropdownValue();
            if (index < 0)
            {
                return string.Empty;
            }

            IList options = GetDropdownOptionsList();
            if (options != null && index >= 0 && index < options.Count)
            {
                object option = options[index];
                if (option != null)
                {
                    PropertyInfo textProp = option.GetType().GetProperty("text", FLAGS);
                    if (textProp != null && textProp.CanRead)
                    {
                        return textProp.GetValue(option) as string ?? string.Empty;
                    }
                }
            }

            return string.Empty;
        }

        private string ReadDropdownOptions()
        {
            IList options = GetDropdownOptionsList();
            if (options == null || options.Count == 0)
            {
                return string.Empty;
            }

            StringBuilder builder = new StringBuilder();
            PropertyInfo textProp = null;

            for (int i = 0; i < options.Count; i++)
            {
                if (i > 0)
                {
                    builder.Append('|');
                }

                object option = options[i];
                if (option != null)
                {
                    if (textProp == null)
                    {
                        textProp = option.GetType().GetProperty("text", FLAGS);
                    }

                    if (textProp != null)
                    {
                        string text = textProp.GetValue(option) as string;
                        builder.Append(text ?? string.Empty);
                    }
                }
            }

            return builder.ToString();
        }

        private IList GetDropdownOptionsList()
        {
            if (_dropdownSelectable == null)
            {
                return null;
            }

            PropertyInfo prop = _dropdownSelectable.GetType().GetProperty("options", FLAGS);
            if (prop != null && prop.CanRead)
            {
                return prop.GetValue(_dropdownSelectable) as IList;
            }

            return null;
        }
    }
}
