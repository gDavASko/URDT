using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>
    /// Handles <c>pointer_up</c>: releases a previously held pointer down state.
    /// </summary>
    public sealed class PointerUpHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public PointerUpHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Vector2 point;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out point))
            {
                point = Vector2.zero;
            }

            int pointerId = PayloadReader.GetPointerId(payload);
            if (pointerId <= 0 && payload["pointerId"] != null)
            {
                pointerId = (int)payload["pointerId"];
            }

            _runtime.Driver.InjectPointer(point, PointerPhase.Up, pointerId);

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
                        holdBtn.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(es) { position = point });
                    }
                    var stick = target.GetComponent<Inspect.UrdtVirtualStick>();
                    if (stick != null)
                    {
                        var es = UnityEngine.EventSystems.EventSystem.current;
                        stick.OnPointerUp(new UnityEngine.EventSystems.PointerEventData(es) { position = point });
                    }
                }
            }

            return Response.Success(command.Id, new JObject
            {
                ["pointer_up"] = true,
                ["pointerId"] = pointerId,
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y }
            });
        }
    }
}
