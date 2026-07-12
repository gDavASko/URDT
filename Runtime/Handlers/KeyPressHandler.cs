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
            return Response.Success(command.Id, new JObject
            {
                ["pressed"] = true,
                ["key"] = key.ToString(),
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_keyboard"
            });
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
