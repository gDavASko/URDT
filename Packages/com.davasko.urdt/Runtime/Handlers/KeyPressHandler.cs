using System;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>key_press</c> through the virtual keyboard. This is honest Input
    /// System device input; it never invokes UI callbacks or changes component state.
    /// </summary>
    public sealed class KeyPressHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public KeyPressHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Key input requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            string keyName = PayloadReader.GetString(payload, "key");
            Key key;
            if (!TryParseKey(keyName, out key))
            {
                return Response.Error(
                    command.Id,
                    ErrorCodes.E_BAD_PAYLOAD,
                    "Unknown key '" + (keyName ?? string.Empty) + "'.");
            }

            int holdFrames = Mathf.Max(0, PayloadReader.GetInt(payload, "hold_frames", 0));
            int queuedFrames = _runtime.Input.ScheduleKeyPress(key, holdFrames);

            // Device key events do not reach IMGUI-reading fields on the new Input System, so
            // also deliver the key to a focused uGUI field (navigation/editing keys). See
            // UiKeyboardBridge for the rationale; this stays keyboard-level, never a text setter.
            bool uiDelivered = false;
            KeyCode keyCode;
            char character;
            if (MapToUiKey(key, out keyCode, out character))
            {
                uiDelivered = UiKeyboardBridge.SendKey(keyCode, character);
            }

            return Response.Success(command.Id, new JObject
            {
                ["pressed"] = true,
                ["key"] = key.ToString(),
                ["queued_frames"] = queuedFrames,
                ["ui_delivered"] = uiDelivered,
                ["input_tier"] = "virtual_keyboard"
            });
        }

        private static bool MapToUiKey(Key key, out KeyCode keyCode, out char character)
        {
            character = '\0';
            switch (key)
            {
                case Key.Backspace: keyCode = KeyCode.Backspace; return true;
                case Key.Delete: keyCode = KeyCode.Delete; return true;
                case Key.LeftArrow: keyCode = KeyCode.LeftArrow; return true;
                case Key.RightArrow: keyCode = KeyCode.RightArrow; return true;
                case Key.UpArrow: keyCode = KeyCode.UpArrow; return true;
                case Key.DownArrow: keyCode = KeyCode.DownArrow; return true;
                case Key.Home: keyCode = KeyCode.Home; return true;
                case Key.End: keyCode = KeyCode.End; return true;
                case Key.Enter: keyCode = KeyCode.Return; character = '\n'; return true;
                case Key.Escape: keyCode = KeyCode.Escape; character = '\x1b'; return true;
                case Key.Tab: keyCode = KeyCode.Tab; character = '\t'; return true;
                case Key.Space: keyCode = KeyCode.Space; character = ' '; return true;
            }

            keyCode = KeyCode.None;
            return false;
        }

        private static bool TryParseKey(string keyName, out Key key)
        {
            key = Key.None;
            if (string.IsNullOrEmpty(keyName))
            {
                return false;
            }

            switch (keyName.Trim().ToLowerInvariant())
            {
                case "return":
                case "enter":
                    key = Key.Enter;
                    return true;
                case "esc":
                case "escape":
                    key = Key.Escape;
                    return true;
                case "up":
                    key = Key.UpArrow;
                    return true;
                case "down":
                    key = Key.DownArrow;
                    return true;
                case "left":
                    key = Key.LeftArrow;
                    return true;
                case "right":
                    key = Key.RightArrow;
                    return true;
            }

            return Enum.TryParse(keyName, true, out key) && key != Key.None;
        }
    }
}
