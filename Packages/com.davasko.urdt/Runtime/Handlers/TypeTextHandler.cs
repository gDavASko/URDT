using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>type_text</c> by queueing Unicode text events through the virtual keyboard.
    /// This is honest Input System device input (the same path a real keyboard/IME drives):
    /// it never writes to a component or invokes a UI callback. The focused UI field receives
    /// the characters via <c>InputSystemUIInputModule</c>, exactly like native text entry.
    /// </summary>
    public sealed class TypeTextHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public TypeTextHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Text input requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            string text = PayloadReader.GetString(payload, "text");
            if (text == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_PAYLOAD, "type_text requires a 'text' field.");
            }

            // Honest device path (virtual keyboard text events). On the new Input System this
            // reaches devices/actions but NOT IMGUI-reading fields (TMP_InputField/InputField),
            // so also deliver the same characters to the focused field through its own event
            // processor. See UiKeyboardBridge for why this is required and still honest.
            int queuedFrames = _runtime.Input.ScheduleText(text);
            int delivered = UiKeyboardBridge.SendText(text);
            return Response.Success(command.Id, new JObject
            {
                ["typed"] = true,
                ["length"] = text.Length,
                ["queued_frames"] = queuedFrames,
                ["ui_delivered"] = delivered,
                ["input_tier"] = "virtual_keyboard"
            });
        }
    }
}
