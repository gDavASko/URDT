namespace KBP.URDT.Net
{
    /// <summary>
    /// Result of processing one inbound message: the text to send back (if any) and
    /// whether the socket must be closed afterwards (e.g. failed authentication).
    /// </summary>
    public readonly struct HandleOutcome
    {
        private HandleOutcome(string responseText, bool closeSocket)
        {
            ResponseText = responseText;
            CloseSocket = closeSocket;
        }

        public string ResponseText { get; }

        public bool CloseSocket { get; }

        public static HandleOutcome Reply(string responseText)
        {
            return new HandleOutcome(responseText, false);
        }

        public static HandleOutcome ReplyAndClose(string responseText)
        {
            return new HandleOutcome(responseText, true);
        }

        /// <summary>No synchronous reply (e.g. the command was submitted for async main-thread
        /// processing and its response will be pumped back later).</summary>
        public static HandleOutcome None()
        {
            return new HandleOutcome(null, false);
        }
    }
}
