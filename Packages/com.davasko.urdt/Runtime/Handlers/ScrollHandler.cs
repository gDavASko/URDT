using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>scroll</c> by feeding mouse wheel delta through the virtual mouse.
    /// </summary>
    public sealed class ScrollHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public ScrollHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            if (_runtime.Input == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_UNSUPPORTED, "Scroll requires InputSimulator.");
            }

            JObject payload = PayloadReader.AsObject(command.Payload);
            Vector2 point;
            bool resolved = PayloadReader.HasAddressedTarget(payload)
                ? PayloadReader.TryGetScrollInputPoint(_runtime, payload, out point)
                : PayloadReader.TryGetScreenPoint(_runtime, payload, out point);
            if (!resolved)
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_HITTABLE, "Scroll needs an unobstructed target point or explicit point.");
            }

            float deltaX = PayloadReader.GetFloat(payload, "delta_x", 0f);
            float deltaY = PayloadReader.GetFloat(payload, "delta_y", PayloadReader.GetFloat(payload, "delta", -120f));
            Vector2 scrollDelta = new Vector2(deltaX, deltaY);
            int queuedFrames = _runtime.Input.ScheduleScroll(point, scrollDelta);

            return Response.Success(command.Id, new JObject
            {
                ["scrolled"] = true,
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y },
                ["scrollDelta"] = new JObject { ["x"] = scrollDelta.x, ["y"] = scrollDelta.y },
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }
    }
}
