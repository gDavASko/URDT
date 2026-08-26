using KBP.URDT.Net;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>subscribe</c> (protocol §4.10): validates event names against the
    /// catalog, records the subscription, and acknowledges. Unknown names → E_UNKNOWN_EVENT
    /// with the unrecognized names in <c>details</c>.</summary>
    public sealed class SubscribeHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public SubscribeHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            if (!(payload["events"] is JArray requested) || requested.Count == 0)
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_REQUEST, "subscribe requires a non-empty 'events' array.");
            }

            JArray unknown = new JArray();
            JArray subscribed = new JArray();
            for (int i = 0; i < requested.Count; i++)
            {
                string name = requested[i].ToString();
                if (!ProtocolConstants.IsKnownEvent(name))
                {
                    unknown.Add(name);
                    continue;
                }

                _runtime.Subscriptions.Add(name);
                subscribed.Add(name);
            }

            if (unknown.Count > 0)
            {
                return Response.Error(
                    command.Id, ErrorCodes.E_UNKNOWN_EVENT, "Unknown event name(s).",
                    new JObject { ["unknown"] = unknown });
            }

            return Response.Success(command.Id, new JObject { ["subscribed"] = subscribed });
        }
    }
}
