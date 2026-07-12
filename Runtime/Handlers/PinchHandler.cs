using System.Collections.Generic;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>pinch</c> as two symmetric touch drags around a center point.
    /// </summary>
    public sealed class PinchHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<List<Vector2>> _paths = new List<List<Vector2>>(2);

        public PinchHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Pinch requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            Vector2 center;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out center))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Pinch needs a target or center point.");
            }

            float fromDistance = PayloadReader.GetFloat(payload, "from_distance", 80f);
            float toDistance = PayloadReader.GetFloat(payload, "to_distance", 180f);
            Vector2 axis = InputPayloadReader.DirectionToVector(PayloadReader.GetString(payload, "axis", "right"));
            BuildPinchPaths(center, axis, fromDistance, toDistance);

            int steps = Mathf.Max(1, PayloadReader.GetInt(payload, "steps", 8));
            int queuedFrames = _runtime.Input.ScheduleMultiDrag(_paths, steps);

            return Response.Success(command.Id, new JObject
            {
                ["pinched"] = true,
                ["center"] = new JObject { ["x"] = center.x, ["y"] = center.y },
                ["from_distance"] = fromDistance,
                ["to_distance"] = toDistance,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }

        private void BuildPinchPaths(Vector2 center, Vector2 axis, float fromDistance, float toDistance)
        {
            _paths.Clear();

            Vector2 fromOffset = axis.normalized * (fromDistance * 0.5f);
            Vector2 toOffset = axis.normalized * (toDistance * 0.5f);

            List<Vector2> first = new List<Vector2>(2);
            first.Add(center - fromOffset);
            first.Add(center - toOffset);

            List<Vector2> second = new List<Vector2>(2);
            second.Add(center + fromOffset);
            second.Add(center + toOffset);

            _paths.Add(first);
            _paths.Add(second);
        }
    }
}
