using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Dedicated URDT beacon for general non-interactive UI elements (labels, icons, badges, containers).
    /// Single Responsibility: tracks position, visibility, and screen-space bounds.
    /// Free of any button, toggle, slider, input, dropdown, or scrollrect fields.
    /// </summary>
    [DisallowMultipleComponent]
    public class UrdtUiGenericTarget : UrdtUiTarget
    {
        private static readonly UrdtCommandType[] DEFAULT_GENERIC_COMMANDS = new[]
        {
            UrdtCommandType.Inspect,
            UrdtCommandType.Query
        };

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
                TargetKind = "element";
            }
        }

        private void EnsureDefaultCommands()
        {
            if (Commands == null || Commands.Count == 0)
            {
                Commands = new List<UrdtCommandType>(DEFAULT_GENERIC_COMMANDS);
            }
        }

        protected override string InferTargetKind()
        {
            return "element";
        }
    }
}
