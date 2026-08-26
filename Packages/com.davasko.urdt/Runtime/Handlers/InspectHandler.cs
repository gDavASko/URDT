using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>inspect</c>: returns the whitelisted state slice of one addressed object.</summary>
    public sealed class InspectHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public InspectHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            Handle handle;
            if (!PayloadReader.TryResolveHandle(_runtime, payload, out handle))
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Target not found.");
            }

            NodeState state = _runtime.Driver.InspectNode(handle, ReadWhitelist(payload));
            if (state == null)
            {
                return Response.Error(command.Id, ErrorCodes.E_NOT_FOUND, "Handle not resolvable.");
            }

            string testId;
            if (_runtime.Registry.TryGetTestId(handle, out testId))
            {
                state.TestId = testId;
            }

            // Node fields are returned flattened at the top of `data` (protocol §4.5).
            return Response.Success(command.Id, UrdtJson.NodeToJson(state));
        }

        private static List<string> ReadWhitelist(JObject payload)
        {
            if (payload["components"] is JArray array && array.Count > 0)
            {
                List<string> whitelist = new List<string>(array.Count);
                for (int i = 0; i < array.Count; i++)
                {
                    whitelist.Add(array[i].ToString());
                }

                return whitelist;
            }

            return null;
        }
    }
}
