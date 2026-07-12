namespace KBP.URDT.Diagnostics
{
    /// <summary>
    /// Result of a <see cref="DiagnosticsCapture"/>: optional PNG screenshot bytes
    /// (diagnostic-only) and a slice of recent structured log entries.
    /// </summary>
    public readonly struct CaptureData
    {
        public CaptureData(byte[] screenshotPng, LogEntry[] logs)
        {
            ScreenshotPng = screenshotPng;
            Logs = logs ?? System.Array.Empty<LogEntry>();
        }

        public byte[] ScreenshotPng { get; }

        public LogEntry[] Logs { get; }

        public bool HasScreenshot
        {
            get { return ScreenshotPng != null && ScreenshotPng.Length > 0; }
        }
    }
}
