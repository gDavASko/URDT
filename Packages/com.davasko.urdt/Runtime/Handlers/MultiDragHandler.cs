using System.Collections.Generic;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>multi_drag</c> with independent touch pointer paths.
    /// </summary>
    public sealed class MultiDragHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<List<Vector2>> _paths = new List<List<Vector2>>(4);

        public MultiDragHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Multi-drag requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            if (!InputPayloadReader.TryReadPathList(_runtime, payload, _paths))
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_REQUEST, "Multi-drag needs a paths array.");
            }

            int steps = Mathf.Max(1, PayloadReader.GetInt(payload, "steps", 8));
            int queuedFrames = _runtime.Input.ScheduleMultiDrag(_paths, steps);

            return Response.Success(command.Id, new JObject
            {
                ["multi_dragged"] = true,
                ["pointers"] = _paths.Count,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }
    }
}
