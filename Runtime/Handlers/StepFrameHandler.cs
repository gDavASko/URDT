using System;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>step_frame</c>: advances N frames with a pinned delta (deterministic
    /// mode only — else E_TIME_MODE / E_UNSUPPORTED).</summary>
    public sealed class StepFrameHandler : ICommandHandler
    {
        private const float DEFAULT_DELTA_MS = 1000f / 60f;

        private readonly UrdtRuntime _runtime;

        public StepFrameHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Time == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "No time controller.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            int frames = PayloadReader.GetInt(payload, "frames", 1);
            float deltaMs = PayloadReader.GetFloat(payload, "delta_ms", DEFAULT_DELTA_MS);

            try
            {
                _runtime.Time.StepFrame(frames, deltaMs);
            }
            catch (NotSupportedException)
            {
                return Response.Error(command.Id, ErrorCodes.E_TIME_MODE, "step_frame requires deterministic mode.");
            }

            return Response.Success(command.Id, new JObject
            {
                ["advanced"] = frames,
                ["frame"] = UnityEngine.Time.frameCount
            });
        }
    }
}
