namespace KBP.URDT.Net
{
    /// <summary>
    /// Text message received from a specific WebSocket connection.
    /// </summary>
    public readonly struct UrdtConnectionMessage
    {
        /// <summary>
        /// Creates a connection-scoped message.
        /// </summary>
        public UrdtConnectionMessage(long connectionId, string text)
        {
            ConnectionId = connectionId;
            Text = text;
        }

        /// <summary>Gets the monotonically increasing connection identifier.</summary>
        public long ConnectionId { get; }

        /// <summary>Gets the decoded UTF-8 payload.</summary>
        public string Text { get; }
    }
}
