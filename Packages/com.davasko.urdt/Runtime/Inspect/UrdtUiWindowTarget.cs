using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for screen windows, dialogs, modals, and main UI panels.
    /// Single Responsibility: tracks window lifecycle, active visibility, bounds, and context boundaries.
    /// Free of any button, toggle, slider, input, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiWindowTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_WINDOW_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

        public static string CurrentActiveWindow { get; set; } = "window_main_menu";
        public static string CurrentActiveModule { get; set; } = "launcher";

        protected virtual void OnEnable()
        {
            if (!string.IsNullOrEmpty(TargetId))
            {
                CurrentActiveWindow = TargetId;
            }
            if (!string.IsNullOrEmpty(Module))
            {
                CurrentActiveModule = Module;
            }
        }

        protected override void Awake()
        {
            base.Awake();
            EnsureDefaultCommands();
        }

        protected override void Reset()
        {
            base.Reset();
            EnsureDefaultCommands();
            if (string.IsNullOrEmpty(TargetKind))
            {
                TargetKind = "window";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_WINDOW_COMMANDS);
            }
        }

        public void ConfigureWindow(
            string targetId,
            string activeWindow,
            string activeModule,
            string module,
            IEnumerable<UrdtCommandType> supportedCommands = null,
            RectTransform rectTransform = null,
            Canvas canvas = null)
        {
            base.ConfigureUi(
                targetId,
                "window",
                activeWindow,
                activeModule,
                module,
                supportedCommands != null ? supportedCommands : DEFAULT_WINDOW_COMMANDS,
                rectTransform,
                canvas);
        }

        protected override string InferTargetKind()
        {
            return "window";
        }
    }
}
