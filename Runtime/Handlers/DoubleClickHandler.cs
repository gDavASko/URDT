using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>double_click</c> as two honest click cycles at the same point.
    /// </summary>
    public sealed class DoubleClickHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public DoubleClickHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Vector2 point;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out point))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "No target or point to double click.");
            }

            int queuedFrames;
            if (_runtime.Input != null)
            {
                int holdFrames = PayloadReader.GetInt(payload, "hold_frames", 0);
                queuedFrames = _runtime.Input.ScheduleClick(point, holdFrames);
                queuedFrames += _runtime.Input.ScheduleClick(point, holdFrames);
            }
            else
            {
                for (int i = 0; i < 2; i++)
                {
                    _runtime.Driver.InjectPointer(point, PointerPhase.Move);
                    _runtime.Driver.InjectPointer(point, PointerPhase.Down);
                    _runtime.Driver.InjectPointer(point, PointerPhase.Up);
                }

                queuedFrames = 6;
            }

            return Response.Success(command.Id, new JObject
            {
                ["double_clicked"] = true,
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y },
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = "virtual_device"
            });
        }
    }
}
