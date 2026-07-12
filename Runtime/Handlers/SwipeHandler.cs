using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

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
            if (!TryBuildPath(payload))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Swipe needs a target, point, or path.");
            }

            int steps = Mathf.Max(1, PayloadReader.GetInt(payload, "steps", 8));
            int pointerId = PayloadReader.GetPointerId(payload);
            int queuedFrames = _runtime.Input != null
                ? _runtime.Input.ScheduleDrag(_points, steps, pointerId)
                : InjectFallback(steps, pointerId);

            return Response.Success(command.Id, new JObject
            {
                ["swiped"] = true,
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = pointerId > 0 ? "virtual_touchscreen" : "virtual_mouse"
            });
        }

        private bool TryBuildPath(JObject payload)
        {
            GameObject target;
            bool isDirectionalTargetedSwipe = payload["path"] == null
                && PayloadReader.TryResolveGameObject(_runtime, payload, out target)
                && target != null
                && target.GetComponent<ScrollRect>() != null;
            if (!isDirectionalTargetedSwipe)
            {
                return InputPayloadReader.TryBuildDirectionalPath(_runtime, payload, _points, DEFAULT_DISTANCE);
            }

            Vector2 origin;
            if (!PayloadReader.TryGetScrollInputPoint(_runtime, payload, out origin))
            {
                return false;
            }

            float distance = PayloadReader.GetFloat(payload, "distance", DEFAULT_DISTANCE);
            Vector2 direction = InputPayloadReader.DirectionToVector(
                PayloadReader.GetString(payload, "direction", "right"));
            _points.Clear();
            _points.Add(origin);
            _points.Add(origin + direction * distance);
            return true;
        }

        private int InjectFallback(int steps, int pointerId)
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
            return steps + 3;
        }
    }
}
