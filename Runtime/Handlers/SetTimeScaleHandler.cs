using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>set_time_scale</c> (protocol §4.12): pause/accelerate. Both modes.</summary>
    public sealed class SetTimeScaleHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public SetTimeScaleHandler(UrdtRuntime runtime)
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
            float scale = PayloadReader.GetFloat(payload, "scale", 1f);
            _runtime.Time.SetTimeScale(scale);

            return Response.Success(command.Id, new JObject { ["time_scale"] = scale });
        }
    }
}
