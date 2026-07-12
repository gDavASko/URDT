using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>wait_for</c> as a single condition poll: evaluates whether the
    /// addressed object's whitelisted field equals the expected value. The client drives the
    /// polling loop (poll → step_frame → poll), so this stays a non-blocking main-thread op.</summary>
    public sealed class WaitForHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public WaitForHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            JObject condition = payload["condition"] as JObject;
            string path = condition != null ? condition.Value<string>("path") : null;
            string expected = condition != null && condition["equals"] != null
                ? condition["equals"].ToString()
                : null;

            string component = null;
            string field = null;
            if (!string.IsNullOrEmpty(path))
            {
                int dot = path.IndexOf('.');
                if (dot > 0 && dot < path.Length - 1)
                {
                    component = path.Substring(0, dot);
                    field = path.Substring(dot + 1);
                }
            }

            Handle handle;
            if (component == null || field == null || !PayloadReader.TryResolveHandle(_runtime, payload, out handle))
            {
                return Response.Success(command.Id, new JObject { ["satisfied"] = false, ["waited_frames"] = 0 });
            }

            List<string> whitelist = new List<string> { component };
            NodeState state = _runtime.Driver.InspectNode(handle, whitelist);

            bool satisfied = false;
            if (state != null && state.Components != null)
            {
                Dictionary<string, object> slice;
                object value;
                if (state.Components.TryGetValue(component, out slice) && slice.TryGetValue(field, out value))
                {
                    satisfied = string.Equals(value != null ? value.ToString() : null, expected);
                }
            }

            // Single poll — the client drives the poll → step_frame → poll loop (deterministic).
            return Response.Success(command.Id, new JObject { ["satisfied"] = satisfied, ["waited_frames"] = 0 });
        }
    }
}
