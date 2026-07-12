using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Structured error body {code, message, details} (engine-agnostic).
    /// </summary>
    public sealed class ErrorInfo
    {
        [JsonProperty("code")]
        public string Code { get; set; }

        [JsonProperty("message")]
        public string Message { get; set; }

        [JsonProperty("details", NullValueHandling = NullValueHandling.Ignore)]
        public JToken Details { get; set; }
    }
}
