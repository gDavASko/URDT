using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for text labels, status badges, instruction text, and score displays.
    /// Single Responsibility: tracks text content, font size, and color without modifying the underlying component.
    /// Supports TextMeshPro (TMP_Text / TextMeshProUGUI) and standard uGUI Text via reflection.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiTextTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_TEXT_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        private const BindingFlags FLAGS = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

        [Header("Text Binding")]
        [SerializeField] private Component _textComponent = null;

        [TestInspectable]
        public string Text
        {
            get { return ReadText(); }
        }

        [TestInspectable]
        public float FontSize
        {
            get { return ReadFontSize(); }
        }

        [TestInspectable]
        public string ColorHex
        {
            get { return ReadColorHex(); }
        }

        public Component TextComponent
        {
            get { return _textComponent; }
            set { _textComponent = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindText();
            EnsureDefaultCommands();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindText();
            EnsureDefaultCommands();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindText();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "text_label";
            }
        }

        public void AutoBindText()
        {
            if (_textComponent == null)
            {
                Component[] comps = GetComponents<Component>();
                for (int i = 0; i < comps.Length; i++)
                {
                    if (comps[i] == null || comps[i] == this) continue;
                    string name = comps[i].GetType().Name;
                    if (name.Contains("TextMeshPro") || name.Contains("TMP_Text") || name == "Text")
                    {
                        _textComponent = comps[i];
                        break;
                    }
                }
            }
        }

        public void ConfigureText(
            string targetId,
            string activeWindow = "window_2d_suite",
            string activeModule = "2d",
            Component textComponent = null)
        {
            base.ConfigureUi(
                targetId: targetId,
                targetKind: "text_label",
                activeWindow: activeWindow,
                activeModule: activeModule,
                module: "2d",
                supportedCommands: DEFAULT_TEXT_COMMANDS
            );
            if (textComponent != null)
            {
                _textComponent = textComponent;
            }
            AutoBindText();
        }

        private string ReadText()
        {
            if (_textComponent == null) return string.Empty;
            PropertyInfo prop = _textComponent.GetType().GetProperty("text", FLAGS);
            if (prop != null && prop.CanRead)
            {
                return prop.GetValue(_textComponent) as string ?? string.Empty;
            }
            return string.Empty;
        }

        private float ReadFontSize()
        {
            if (_textComponent == null) return 0f;
            PropertyInfo prop = _textComponent.GetType().GetProperty("fontSize", FLAGS);
            if (prop != null && prop.CanRead)
            {
                object val = prop.GetValue(_textComponent);
                if (val is float f) return f;
                if (val is int i) return i;
            }
            return 0f;
        }

        private string ReadColorHex()
        {
            if (_textComponent == null) return string.Empty;
            PropertyInfo prop = _textComponent.GetType().GetProperty("color", FLAGS);
            if (prop != null && prop.CanRead)
            {
                object val = prop.GetValue(_textComponent);
                if (val is Color c) return ColorUtility.ToHtmlStringRGBA(c);
            }
            return string.Empty;
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_TEXT_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "text_label";
        }
    }
}
