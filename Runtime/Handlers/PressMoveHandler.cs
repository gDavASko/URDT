using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>press_move</c>: pointer down, path movement, pointer up.
    /// </summary>
    public sealed class PressMoveHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<Vector2> _points = new List<Vector2>(16);

        public PressMoveHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            if (!InputPayloadReader.TryReadPath(_runtime, payload, _points))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Press-move needs a resolvable path.");
            }

            int steps = Mathf.Max(1, PayloadReader.GetInt(payload, "steps", 10));
            int pointerId = PayloadReader.GetPointerId(payload);
            int queuedFrames;
            if (_runtime.Input != null)
            {
                queuedFrames = _runtime.Input.ScheduleDrag(_points, steps, pointerId);
            }
            else
            {
                Vector2 from = _points[0];
                Vector2 to = _points[_points.Count - 1];
                _runtime.Driver.InjectPointer(from, PointerPhase.Move, pointerId);
                _runtime.Driver.InjectPointer(from, PointerPhase.Down, pointerId);
                for (int i = 1; i <= steps; i++)
                {
                    _runtime.Driver.InjectPointer(Vector2.Lerp(from, to, (float)i / steps), PointerPhase.Move, pointerId);
                }

                _runtime.Driver.InjectPointer(to, PointerPhase.Up, pointerId);
                queuedFrames = steps + 3;
            }

            return Response.Success(command.Id, new JObject
            {
                ["press_moved"] = true,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = pointerId > 0 ? "virtual_touchscreen" : "virtual_mouse"
            });
        }
    }
}
