using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>swipe</c> as a semantic drag path.
    /// </summary>
    public sealed class SwipeHandler : ICommandHandler
    {
        private const float DEFAULT_DISTANCE = 160f;

        private readonly UrdtRuntime _runtime;
        private readonly List<Vector2> _points = new List<Vector2>(8);

        public SwipeHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);
            if (!InputPayloadReader.TryBuildDirectionalPath(_runtime, payload, _points, DEFAULT_DISTANCE))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Swipe needs a target, point, or path.");
            }

            int steps = Mathf.Max(1, PayloadReader.GetInt(payload, "steps", 8));
            int queuedFrames = _runtime.Input != null
                ? _runtime.Input.ScheduleDrag(_points, steps)
                : InjectFallback(steps);

            return Response.Success(command.Id, new JObject
            {
                ["swiped"] = true,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }

        private int InjectFallback(int steps)
        {
            Vector2 from = _points[0];
            Vector2 to = _points[_points.Count - 1];
            _runtime.Driver.InjectPointer(from, PointerPhase.Move);
            _runtime.Driver.InjectPointer(from, PointerPhase.Down);
            for (int i = 1; i <= steps; i++)
            {
                _runtime.Driver.InjectPointer(Vector2.Lerp(from, to, (float)i / steps), PointerPhase.Move);
            }

            _runtime.Driver.InjectPointer(to, PointerPhase.Up);
            return steps + 3;
        }
    }
}
