namespace KBP.URDT.Driver
{
    /// <summary>
    /// Options of a diagnostics capture request (screenshot is diagnostic-only,
    /// never a PASS/FAIL oracle — invariant I3).
    /// </summary>
    public sealed class CaptureOptions
    {
        public bool IncludeScreenshot { get; set; }

        public int LogTailLines { get; set; }
    }
}
