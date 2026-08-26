using System;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>pin_fixed_delta</c> (protocol §4.13): pins fixedDeltaTime/maximumDeltaTime
    /// for reproducible physics. Deterministic mode only (else E_TIME_MODE).</summary>
    public sealed class PinFixedDeltaHandler : ICommandHandler
    {
        private const float DEFAULT_FIXED_DELTA_MS = 20f;
        private const float DEFAULT_MAX_DELTA_MS = 100f;

        private readonly UrdtRuntime _runtime;

        public PinFixedDeltaHandler(UrdtRuntime runtime)
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
            float fixedDeltaMs = PayloadReader.GetFloat(payload, "fixed_delta_ms", DEFAULT_FIXED_DELTA_MS);
            float maxDeltaMs = PayloadReader.GetFloat(payload, "max_delta_ms", DEFAULT_MAX_DELTA_MS);

            try
            {
                _runtime.Time.PinFixedDelta(fixedDeltaMs, maxDeltaMs);
            }
            catch (NotSupportedException)
            {
                return Response.Error(command.Id, ErrorCodes.E_TIME_MODE, "pin_fixed_delta requires deterministic mode.");
            }

            return Response.Success(command.Id, new JObject { ["fixed_delta_ms"] = fixedDeltaMs });
        }
    }
}
