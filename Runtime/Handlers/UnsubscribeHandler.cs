using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Handlers
{
    /// <summary>Handles <c>unsubscribe</c> (protocol §4.10): removes the given event names from
    /// the subscription set.</summary>
    public sealed class UnsubscribeHandler : ICommandHandler
    {
        private readonly UrdtRuntime _runtime;

        public UnsubscribeHandler(UrdtRuntime runtime)
        {
            _runtime = runtime;
        }

        public Response Handle(Command command)
        {
            JObject payload = PayloadReader.AsObject(command.Payload);

            if (!(payload["events"] is JArray requested) || requested.Count == 0)
            {
                return Response.Error(command.Id, ErrorCodes.E_BAD_REQUEST, "unsubscribe requires a non-empty 'events' array.");
            }

            JArray removed = new JArray();
            for (int i = 0; i < requested.Count; i++)
            {
                string name = requested[i].ToString();
                _runtime.Subscriptions.Remove(name);
                removed.Add(name);
            }

            return Response.Success(command.Id, new JObject { ["unsubscribed"] = removed });
        }
    }
}
