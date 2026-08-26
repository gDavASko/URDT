using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;

namespace KBP.URDT.Net
{
    /// <summary>
    /// RFC 6455 opening handshake — pure BCL, engine-agnostic (no UnityEngine types).
    /// Computes the <c>Sec-WebSocket-Accept</c> value and builds the 101 response.
    /// </summary>
    public static class WebSocketHandshake
    {
        private const string WS_GUID = "258EAFA5-E914-47DA-95CA-C5AB0DC85B11";

        public static string ComputeAcceptKey(string secWebSocketKey)
        {
            using (SHA1 sha1 = SHA1.Create())
            {
                byte[] hash = sha1.ComputeHash(Encoding.ASCII.GetBytes((secWebSocketKey ?? string.Empty) + WS_GUID));
                return Convert.ToBase64String(hash);
            }
        }

        /// <summary>
        /// Parses the request headers of an HTTP upgrade request into a case-insensitive map.
        /// </summary>
        public static Dictionary<string, string> ParseHeaders(string requestText)
        {
            Dictionary<string, string> headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            if (string.IsNullOrEmpty(requestText))
            {
                return headers;
            }

            string[] lines = requestText.Split('\n');
            for (int i = 1; i < lines.Length; i++)
            {
                string line = lines[i].TrimEnd('\r');
                int colon = line.IndexOf(':');
                if (colon > 0)
                {
                    string name = line.Substring(0, colon).Trim();
                    string value = line.Substring(colon + 1).Trim();
                    headers[name] = value;
                }
            }

            return headers;
        }

        public static bool IsWebSocketUpgrade(Dictionary<string, string> headers)
        {
            string upgrade;
            string key;
            return headers != null
                && headers.TryGetValue("Upgrade", out upgrade)
                && upgrade.IndexOf("websocket", StringComparison.OrdinalIgnoreCase) >= 0
                && headers.TryGetValue("Sec-WebSocket-Key", out key)
                && !string.IsNullOrEmpty(key);
        }

        public static byte[] BuildAcceptResponse(string secWebSocketKey)
        {
            string accept = ComputeAcceptKey(secWebSocketKey);
            string response =
                "HTTP/1.1 101 Switching Protocols\r\n"
                + "Upgrade: websocket\r\n"
                + "Connection: Upgrade\r\n"
                + "Sec-WebSocket-Accept: " + accept + "\r\n\r\n";
            return Encoding.ASCII.GetBytes(response);
        }

        public static byte[] BuildRejectResponse(int statusCode, string reason)
        {
            string response =
                "HTTP/1.1 " + statusCode + " " + reason + "\r\n"
                + "Connection: close\r\n\r\n";
            return Encoding.ASCII.GetBytes(response);
        }
    }
}
