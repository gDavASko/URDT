namespace KBP.URDT.Net
{
    /// <summary>
    /// RFC 6455 frame opcodes. Engine-agnostic transport layer (no UnityEngine types).
    /// </summary>
    public enum WebSocketOpcode
    {
        Continuation = 0x0,
        Text = 0x1,
        Binary = 0x2,
        Close = 0x8,
        Ping = 0x9,
        Pong = 0xA
    }
}
