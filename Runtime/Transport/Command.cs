namespace KBP.URDT.Transport
{
    /// <summary>
    /// Inbound command envelope (in-proc form of the protocol Envelope). Carries a
    /// correlation <see cref="Id"/>, the <see cref="Action"/> to route, and an opaque
    /// <see cref="Payload"/>. The response echoes <see cref="Id"/> (FR-15).
    /// </summary>
    public sealed class Command
    {
        public Command(string id, string action, object payload = null)
        {
            Id = id;
            Action = action;
            Payload = payload;
        }

        public string Id { get; }

        public string Action { get; }

        public object Payload { get; }
    }
}
