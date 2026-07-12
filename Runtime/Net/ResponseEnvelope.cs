using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Outbound protocol response/event envelope (protocol-spec §2.2/§2.3), correlated to a
    /// request by <see cref="Id"/> (FR-15). Every outbound message carries <see cref="Api"/>.
    /// Engine-agnostic (no UnityEngine types).
    /// </summary>
    public sealed class ResponseEnvelope
    {
        [JsonProperty("api")]
        public int Api { get; set; }

        [JsonProperty("id", NullValueHandling = NullValueHandling.Ignore)]
        public string Id { get; set; }

        [JsonProperty("type")]
        public string Type { get; set; }

        [JsonProperty("event", NullValueHandling = NullValueHandling.Ignore)]
        public string EventName { get; set; }

        [JsonProperty("status", NullValueHandling = NullValueHandling.Ignore)]
        public string Status { get; set; }

        [JsonProperty("data", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Data { get; set; }

        [JsonProperty("error", NullValueHandling = NullValueHandling.Ignore)]
        public ErrorInfo Error { get; set; }

        [JsonProperty("elapsed_ms", NullValueHandling = NullValueHandling.Ignore)]
        public long? ElapsedMs { get; set; }

        public static ResponseEnvelope Ok(string id, JToken data, long elapsedMs = 0)
        {
            return new ResponseEnvelope
            {
                Api = ProtocolConstants.API_VERSION,
                Id = id,
                Type = ProtocolConstants.TYPE_RESPONSE,
                Status = ProtocolConstants.STATUS_OK,
                Data = data,
                ElapsedMs = elapsedMs
            };
        }

        public static ResponseEnvelope Ready(string id, JToken data)
        {
            return new ResponseEnvelope
            {
                Api = ProtocolConstants.API_VERSION,
                Id = id,
                Type = ProtocolConstants.TYPE_RESPONSE,
                Status = ProtocolConstants.STATUS_READY,
                Data = data
            };
        }

        public static ResponseEnvelope Fail(string id, string code, string message, JToken details = null)
        {
            return new ResponseEnvelope
            {
                Api = ProtocolConstants.API_VERSION,
                Id = id,
                Type = ProtocolConstants.TYPE_RESPONSE,
                Status = ProtocolConstants.STATUS_ERROR,
                Error = new ErrorInfo { Code = code, Message = message, Details = details }
            };
        }

        public static ResponseEnvelope Event(string eventName, JToken data)
        {
            return new ResponseEnvelope
            {
                Api = ProtocolConstants.API_VERSION,
                Type = ProtocolConstants.TYPE_EVENT,
                EventName = eventName,
                Data = data
            };
        }
    }
}
