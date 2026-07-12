using System;
using KBP.URDT.Diagnostics;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>capture</c>: a diagnostic screenshot (<c>authoritative:false</c>, I3) +
    /// a slice of recent logs. Never a PASS/FAIL oracle.</summary>
    public sealed class CaptureHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public CaptureHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Diagnostics == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "No diagnostics.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            bool wantScreenshot = PayloadReader.GetBool(payload, "screenshot", true);
            int logTail = PayloadReader.GetInt(payload, "log_tail", 20);

            CaptureData data = _runtime.Diagnostics.Capture(wantScreenshot, logTail);

            JArray logs = new JArray();
            for (int i = 0; i < data.Logs.Length; i++)
            {
                LogEntry entry = data.Logs[i];
                logs.Add(new JObject
                {
                    ["level"] = entry.Level,
                    ["msg"] = entry.Message,
                    ["ts"] = entry.TimestampMs
                });
            }

            JObject result = new JObject
            {
                ["authoritative"] = false,
                ["logs"] = logs
            };

            if (data.HasScreenshot)
            {
                result["screenshot_b64"] = Convert.ToBase64String(data.ScreenshotPng);
            }

            return Response.Success(command.Id, result);
        }
    }
}
