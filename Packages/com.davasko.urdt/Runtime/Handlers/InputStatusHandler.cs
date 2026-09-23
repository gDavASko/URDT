using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>input_status</c>: how many frame-paced pointer steps are still queued and the current
    /// frame time. Lets a client wait until a realtime-paced gesture has actually played out in game time.
    /// </summary>
    public sealed class InputStatusHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public InputStatusHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            int pending = _runtime.Input != null ? _runtime.Input.PendingPointerFrameCount : 0;
            return Response.Success(command.Id, new JObject
            {
                ["pending_frames"] = pending,
                ["frame_ms"] = Mathf.Round(Time.smoothDeltaTime * 100000f) / 100f,
                ["frame"] = Time.frameCount
            });
        }
    }
}
