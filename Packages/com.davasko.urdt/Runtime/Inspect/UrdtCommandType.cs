using System;

namespace KBP.URDT.Inspect
{
    /// <summary>
    /// Strongly-typed enumeration of URDT commands and actions supported by targets.
    /// Enables type-safe selection of capabilities in the Unity Inspector instead of raw strings.
    /// </summary>
    public enum UrdtCommandType
    {
        Inspect = 0,
        Query = 1,
        Click = 2,
        DoubleClick = 3,
        MultiClick = 4,
        Drag = 5,
        MultiDrag = 6,
        Swipe = 7,
        Scroll = 8,
        Pinch = 9,
        PressMove = 10,
        TypeText = 11,
        KeyPress = 12,
        HitTest = 13,
        WaitFor = 14,
        ResetState = 15
    }

    /// <summary>
    /// Utility methods for converting between <see cref="UrdtCommandType"/> and URDT protocol action strings.
    /// </summary>
    public static class UrdtCommandExtensions
    {
        public static string ToProtocolString(this UrdtCommandType command)
        {
            switch (command)
            {
                case UrdtCommandType.Inspect: return "inspect";
                case UrdtCommandType.Query: return "query";
                case UrdtCommandType.Click: return "click";
                case UrdtCommandType.DoubleClick: return "double_click";
                case UrdtCommandType.MultiClick: return "multi_click";
                case UrdtCommandType.Drag: return "drag";
                case UrdtCommandType.MultiDrag: return "multi_drag";
                case UrdtCommandType.Swipe: return "swipe";
                case UrdtCommandType.Scroll: return "scroll";
                case UrdtCommandType.Pinch: return "pinch";
                case UrdtCommandType.PressMove: return "press_move";
                case UrdtCommandType.TypeText: return "type_text";
                case UrdtCommandType.KeyPress: return "key_press";
                case UrdtCommandType.HitTest: return "hit_test";
                case UrdtCommandType.WaitFor: return "wait_for";
                case UrdtCommandType.ResetState: return "reset_state";
                default: return command.ToString().ToLowerInvariant();
            }
        }

        public static bool TryParseCommand(string raw, out UrdtCommandType command)
        {
            command = UrdtCommandType.Inspect;
            if (string.IsNullOrEmpty(raw))
            {
                return false;
            }

            string clean = raw.Trim().ToLowerInvariant();
            switch (clean)
            {
                case "inspect": command = UrdtCommandType.Inspect; return true;
                case "query": command = UrdtCommandType.Query; return true;
                case "click": command = UrdtCommandType.Click; return true;
                case "double_click":
                case "doubleclick": command = UrdtCommandType.DoubleClick; return true;
                case "multi_click":
                case "multiclick": command = UrdtCommandType.MultiClick; return true;
                case "drag": command = UrdtCommandType.Drag; return true;
                case "multi_drag":
                case "multidrag": command = UrdtCommandType.MultiDrag; return true;
                case "swipe": command = UrdtCommandType.Swipe; return true;
                case "scroll": command = UrdtCommandType.Scroll; return true;
                case "pinch": command = UrdtCommandType.Pinch; return true;
                case "press_move":
                case "pressmove": command = UrdtCommandType.PressMove; return true;
                case "type_text":
                case "typetext": command = UrdtCommandType.TypeText; return true;
                case "key_press":
                case "keypress": command = UrdtCommandType.KeyPress; return true;
                case "hit_test":
                case "hittest": command = UrdtCommandType.HitTest; return true;
                case "wait_for":
                case "waitfor": command = UrdtCommandType.WaitFor; return true;
                case "reset_state":
                case "resetstate": command = UrdtCommandType.ResetState; return true;
                default:
                    return Enum.TryParse(raw, true, out command);
            }
        }
    }
}
