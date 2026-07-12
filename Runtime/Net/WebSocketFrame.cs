namespace KBP.URDT.Net
{
    /// <summary>
    /// A decoded/encodable RFC 6455 frame. Engine-agnostic (no UnityEngine types).
    /// </summary>
    public readonly struct WebSocketFrame
    {
        public WebSocketFrame(WebSocketOpcode opcode, byte[] payload, bool isFinal)
        {
            Opcode = opcode;
            Payload = payload ?? System.Array.Empty<byte>();
            IsFinal = isFinal;
        }

        public WebSocketOpcode Opcode { get; }

        public byte[] Payload { get; }

        public bool IsFinal { get; }

        public string GetText()
        {
            return System.Text.Encoding.UTF8.GetString(Payload);
        }

        public static WebSocketFrame Text(string text)
        {
            return new WebSocketFrame(WebSocketOpcode.Text, System.Text.Encoding.UTF8.GetBytes(text ?? string.Empty), true);
        }
    }
}
