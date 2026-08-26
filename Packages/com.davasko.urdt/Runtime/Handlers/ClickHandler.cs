using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>click</c>: honest device-level move→down→up at a screen point
    /// (explicit x/y or the addressed object's screenPosition). The game owns hit logic (I2).</summary>
    public sealed class ClickHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public ClickHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Vector2 point;
            if (!PayloadReader.TryGetScreenPoint(_runtime, payload, out point))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "No target or point to click.");
            }

            GameObject target;
            if (PayloadReader.HasAddressedTarget(payload)
                && PayloadReader.TryResolveGameObject(_runtime, payload, out target))
            {
                GameObject topHit;
                if (!PayloadReader.IsTopHitRelated(target, point, out topHit))
                {
                    JObject details = new JObject
                    {
                        ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y },
                        ["target"] = target.name,
                        ["targetPath"] = BuildPath(target.transform),
                        ["topHit"] = topHit != null ? topHit.name : string.Empty,
                        ["topHitPath"] = topHit != null ? BuildPath(topHit.transform) : string.Empty
                    };
                    return Response.Error(
                        command.Id,
                        ErrorCodes.E_NOT_HITTABLE,
                        "Target is not the top EventSystem hit at the resolved click point.",
                        details);
                }
            }

            int holdFrames = PayloadReader.GetInt(payload, "hold_frames", 0);
            int holdMs = PayloadReader.GetInt(payload, "hold_ms", 0);
            if (holdFrames <= 0 && holdMs > 0)
            {
                holdFrames = Mathf.CeilToInt(holdMs / (1000f / 60f));
            }

            // A UI click must keep the physical button state through at least one
            // processed input frame before release. This is still device input, not
            // a direct UI callback, and makes Button activation deterministic.
            holdFrames = Mathf.Max(1, holdFrames);

            int pointerId = PayloadReader.GetPointerId(payload);
            int queuedFrames;
            if (_runtime.Input != null)
            {
                queuedFrames = _runtime.Input.ScheduleClick(point, holdFrames, pointerId);
            }
            else
            {
                _runtime.Driver.InjectPointer(point, PointerPhase.Move, pointerId);
                _runtime.Driver.InjectPointer(point, PointerPhase.Down, pointerId);
                _runtime.Driver.InjectPointer(point, PointerPhase.Up, pointerId);
                queuedFrames = 3;
            }

            return Response.Success(command.Id, new JObject
            {
                ["clicked"] = true,
                ["screenPosition"] = new JObject { ["x"] = point.x, ["y"] = point.y },
                ["queued_frames"] = queuedFrames,
                ["input_tier"] = pointerId > 0 ? "virtual_touchscreen" : "virtual_mouse",
                ["hit_check"] = "screen_bounds"
            });
        }

        private static string BuildPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            System.Collections.Generic.Stack<string> names =
                new System.Collections.Generic.Stack<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Push(current.name);
                current = current.parent;
            }

            return string.Join("/", names.ToArray());
        }
    }
}
