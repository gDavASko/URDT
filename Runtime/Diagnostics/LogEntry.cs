namespace KBP.URDT.Diagnostics
{
    /// <summary>
    /// One captured log line (protocol §4.9 <c>logs</c> entry): level, message, unix-ms timestamp.
    /// </summary>
    public readonly struct LogEntry
    {
        public LogEntry(string level, string message, long timestampMs)
        {
            Level = level;
            Message = message;
            TimestampMs = timestampMs;
        }

        public string Level { get; }

        public string Message { get; }

        public long TimestampMs { get; }
    }
}
