using System.Collections.Generic;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>multi_click</c> with several touch pointers in the same frame windows.
    /// </summary>
    public sealed class MultiClickHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<Vector2> _points = new List<Vector2>(8);

        public MultiClickHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Multi-click requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            if (!InputPayloadReader.TryReadPointList(_runtime, payload, _points))
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_REQUEST, "Multi-click needs points or targets.");
            }

            int holdFrames = PayloadReader.GetInt(payload, "hold_frames", 0);
            int queuedFrames = _runtime.Input.ScheduleMultiClick(_points, holdFrames);

            return Response.Success(command.Id, new JObject
            {
                ["multi_clicked"] = true,
                ["pointers"] = _points.Count,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }
    }
}
