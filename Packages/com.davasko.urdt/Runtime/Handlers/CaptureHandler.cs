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

            // ScreenCapture.CaptureScreenshotAsTexture only works at end of frame; a command handler runs
            // mid-frame, so it always failed (and logged a console error that polluted failure evidence).
            // Prefer the dashcam's latest end-of-frame JPEG when the dashcam is running.
            var dashcam = global::URDT.Runtime.Inspectors.UrdtCrashDashcam.Instance;
            string latestJpeg = wantScreenshot && dashcam != null ? dashcam.ExportLatestFrameBase64() : null;
            CaptureData data = _runtime.Diagnostics.Capture(wantScreenshot && string.IsNullOrEmpty(latestJpeg), logTail);

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

            if (!string.IsNullOrEmpty(latestJpeg))
            {
                result["screenshot_jpeg_datauri"] = latestJpeg;
            }
            else if (data.HasScreenshot)
            {
                result["screenshot_b64"] = Convert.ToBase64String(data.ScreenshotPng);
            }

            // Failure-evidence dashcam: the last ~5 s of low-res JPEG frames kept in RAM (oldest first).
            if (PayloadReader.GetBool(payload, "dashcam", false) && dashcam != null)
            {
                result["dashcam_jpeg_b64"] = new JArray(dashcam.ExportBase64Frames());
            }

            return Response.Success(command.Id, result);
        }
    }
}
