using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>pointer_down</c>: honest device-level pointer press and hold without immediate release.
    /// Allows multi-finger simultaneous input (e.g. holding a draw button while dragging a virtual stick).
    /// </summary>
    public sealed class PointerDownHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public PointerDownHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Vector2 point;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out point))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "No target or point for pointer_down.");
            }

            int pointerId = PayloadReader.GetPointerId(payload);
            if (pointerId <= 0 && payload["pointerId"] != null)
            {
                pointerId = (int)payload["pointerId"];
            }

            _runtime.Driver.InjectPointer(point, PointerPhase.Move, pointerId);
            _runtime.Driver.InjectPointer(point, PointerPhase.Down, pointerId);

            Handle handle;
            if (PayloadReader.TryResolveHandle(_runtime, payload, out handle) && _runtime.Registry != null)
            {
                GameObject target;
                if (_runtime.Registry.TryResolveHandle(handle, out target) && target != null)
                {
                    var holdBtn = target.GetComponent<Inspect.UrdtHoldButton>();
                    if (holdBtn != null)
                    {
                        var es = UnityEngine.EventSystems.EventSystem.current;
                        holdBtn.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(es) { position = point });
                    }
                    var stick = target.GetComponent<Inspect.UrdtVirtualStick>();
                    if (stick != null)
                    {
                        var es = UnityEngine.EventSystems.EventSystem.current;
                        stick.OnPointerDown(new UnityEngine.EventSystems.PointerEventData(es) { position = point });
                    }
                }
            }

            return Response.Success(command.Id, new JObject
            {
                ["pointer_down"] = true,
                ["pointerId"] = pointerId,
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y }
            });
        }
    }
}
