using KBP.URDT.Driver;
using System.Collections.Generic;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>drag</c>: press at <c>from</c>, a series of moves, release at <c>to</c>
    /// (each endpoint an explicit x/y or an addressed object).</summary>
    public sealed class DragHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;
        private readonly List<Vector2> _points = new List<Vector2>(16);

        public DragHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            if (!TryReadPath(payload, _points))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Drag needs resolvable 'from' and 'to'.");
            }

            JObject fromPayload = payload["from"] as JObject;
            if (fromPayload != null
                && PayloadReader.HasAddressedTarget(fromPayload))
            {
                GameObject sourceTarget;
                GameObject topHit;
                if (PayloadReader.TryResolveGameObject(_runtime, fromPayload, out sourceTarget)
                    && !PayloadReader.IsTopHitRelated(sourceTarget, _points[0], out topHit))
                {
                    return Response.Error(
                        command.Id,
                        ErrorCodes.E_NOT_HITTABLE,
                        "Drag source is not the top EventSystem hit at the resolved point.");
                }
            }

            int steps = PayloadReader.GetInt(payload, "steps", 10);
            if (steps < 1)
            {
                steps = 1;
            }

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
                    Vector2 point = Vector2.Lerp(from, to, (float)i / steps);
                    _runtime.Driver.InjectPointer(point, PointerPhase.Move, pointerId);
                }

                _runtime.Driver.InjectPointer(to, PointerPhase.Up, pointerId);
                queuedFrames = steps + 3;
            }

            return Response.Success(command.Id, new JObject
            {
                ["dragged"] = true,
                ["points"] = steps,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = pointerId > 0 ? "virtual_touchscreen" : "virtual_mouse"
            });
        }

        private bool TryReadPath(JObject payload, List<Vector2> points)
        {
            points.Clear();

            JArray path = payload["path"] as JArray;
            if (path != null)
            {
                for (int i = 0; i < path.Count; i++)
                {
                    Vector2 point;
                    if (!PayloadReader.TryGetScreenPoint(_runtime, path[i], out point))
                    {
                        points.Clear();
                        return false;
                    }

                    points.Add(point);
                }

                return points.Count >= 2;
            }

            Vector2 from;
            Vector2 to;
            JToken fromToken = payload["from"] ?? payload;
            JToken toToken = payload["to"];
            if (PayloadReader.TryGetScreenPoint(_runtime, fromToken, out from)
                && PayloadReader.TryGetScreenPoint(_runtime, toToken, out to))
            {
                points.Add(from);
                points.Add(to);
                return true;
            }

            points.Clear();
            return false;
        }
    }
}
