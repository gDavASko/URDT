using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for scroll views and scrollable lists (ScrollRect).
    /// Single Responsibility: tracks normalized scroll position, content height, viewport bounds, and scroll/drag/swipe commands.
    /// Free of any button, toggle, slider, input, or dropdown fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiScrollTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_SCROLL_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query,
            UrdtCommandType.Scroll,
            UrdtCommandType.Drag,
            UrdtCommandType.Swipe
        };

        [Header("Scroll Component")]
        [Tooltip("ScrollRect component bound to this beacon (auto-detected if null).")]
        [SerializeField] private ScrollRect _scrollRect = null;

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

        public ScrollRect ScrollRect
        {
            get { return _scrollRect; }
            set { _scrollRect = value; }
        }

        protected override void Awake()
        {
            base.Awake();
            AutoBindScroll();
            EnsureDefaultCommands();
        }

        protected virtual void OnEnable()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.AddListener(OnScrollChanged);
            }
        }

        protected virtual void OnDisable()
        {
            if (_scrollRect != null)
            {
                _scrollRect.onValueChanged.RemoveListener(OnScrollChanged);
            }
        }

        private void OnScrollChanged(Vector2 val)
        {
            RecordInput("scroll", "scroll_y=" + val.y.ToString("0.00"));
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            AutoBindScroll();
        }

        protected override void Reset()
        {
            base.Reset();
            AutoBindScroll();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "scroll";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count <= 2)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_SCROLL_COMMANDS);
            }
        }

        public void AutoBindScroll()
        {
            if (_scrollRect == null)
            {
                _scrollRect = GetComponent<ScrollRect>();
            }
        }

        public void ConfigureScroll(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            ScrollRect scrollRect = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "scroll",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_SCROLL_COMMANDS,
                rectTransform,
                canvas);

            _scrollRect = scrollRect != null ? scrollRect : _scrollRect;
            AutoBindScroll();
        }

        protected override string InferTargetKind()
        {
            return "scroll";
        }
    }
}
