using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Inbound protocol request envelope (engine-agnostic, no UnityEngine types).
    /// </summary>
    public sealed class RequestEnvelope
    {
        [JsonProperty("api")]
        public int Api { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("action")]
        public string Action { get; set; }

        [JsonProperty("payload")]
        public JObject Payload { get; set; }

        [JsonProperty("timeout_ms")]
        public int TimeoutMs { get; set; }
    }
}
