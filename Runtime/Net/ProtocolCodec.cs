using Newtonsoft.Json;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Newtonsoft (de)serialization of protocol envelopes. Engine-agnostic (no UnityEngine).
    /// </summary>
    public static class ProtocolCodec
    {
        private static readonly JsonSerializerSettings SETTINGS = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore,
            Formatting = Formatting.None
        };

        public static bool TryParseRequest(string text, out RequestEnvelope request, out string parseError)
        {
            request = null;
            parseError = null;

            if (string.IsNullOrWhiteSpace(text))
            {
                parseError = "empty message";
                return false;
            }

            try
            {
                request = JsonConvert.DeserializeObject<RequestEnvelope>(text, SETTINGS);
                if (request == null)
                {
                    parseError = "null request";
                    return false;
                }

                return true;
            }
            catch (JsonException exception)
            {
                parseError = exception.Message;
                return false;
            }
        }

        public static string Serialize(ResponseEnvelope response)
        {
            return JsonConvert.SerializeObject(response, SETTINGS);
        }

        public static ResponseEnvelope DeserializeResponse(string text)
        {
            return JsonConvert.DeserializeObject<ResponseEnvelope>(text, SETTINGS);
        }
    }
}
