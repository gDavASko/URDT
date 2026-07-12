namespace KBP.URDT.Driver
{
    /// <summary>
    /// Result of a diagnostics capture. <see cref="Authoritative"/> is always
    /// false in the logic lane: screenshots are diagnostic-only (invariant I3).
    /// </summary>
    public sealed class CaptureResult
    {
        public bool Authoritative { get; set; }

        public byte[] ScreenshotPng { get; set; }

        public string[] LogTail { get; set; }
    }
}
