using System;
using System.Collections.Generic;
using UnityEngine;

namespace KBP.URDT.Diagnostics
{
    /// <summary>
    /// Diagnostic capture (FR-14, invariant I3): a screenshot and a slice of recent logs.
    /// The screenshot is DIAGNOSTIC-ONLY (<c>authoritative:false</c>) — never a PASS/FAIL
    /// oracle; verdicts come from inspect/query. Maintains a bounded ring buffer of structured
    /// log entries and raises <see cref="LogReceived"/> for every log (used for log_error events).
    /// </summary>
    public sealed class DiagnosticsCapture : IDisposable
    {
        private static readonly DateTime UnixEpoch =
            new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        private readonly int _capacity;
        private readonly Queue<LogEntry> _logs;
        private readonly object _logLock = new object();
        private bool _subscribed;

        /// <summary>Fired for every captured log (level, message, stack, unix-ms). May fire on a
        /// background thread (Unity's threaded log callback). Used to raise <c>log_error</c> events.</summary>
        public event Action<string, string, string, long> LogReceived;

        public DiagnosticsCapture(int logCapacity = 256)
        {
            _capacity = logCapacity > 0 ? logCapacity : 256;
            _logs = new Queue<LogEntry>(_capacity);
        }

        public void EnableLogCapture()
        {
            if (_subscribed)
            {
                return;
            }

            Application.logMessageReceivedThreaded += OnLog;
            _subscribed = true;
        }

        /// <summary>
        /// Captures a diagnostic screenshot (PNG bytes; null if unavailable/headless) and the
        /// last <paramref name="logTail"/> log entries. The screenshot is never authoritative.
        /// </summary>
        public CaptureData Capture(bool includeScreenshot, int logTail)
        {
            byte[] png = null;
            if (includeScreenshot)
            {
                png = TryCaptureScreenshot();
            }

            LogEntry[] logs = SnapshotLogs(logTail);
            return new CaptureData(png, logs);
        }

        public void Dispose()
        {
            if (_subscribed)
            {
                Application.logMessageReceivedThreaded -= OnLog;
                _subscribed = false;
            }

            lock (_logLock)
            {
                _logs.Clear();
            }
        }

        private static byte[] TryCaptureScreenshot()
        {
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                return texture.EncodeToPNG();
            }
            catch (Exception)
            {
                // Headless / no graphics device — screenshots are diagnostic-only, so a
                // failure is non-fatal.
                return null;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private LogEntry[] SnapshotLogs(int logTail)
        {
            lock (_logLock)
            {
                int count = _logs.Count;
                if (logTail > 0 && logTail < count)
                {
                    count = logTail;
                }

                LogEntry[] all = _logs.ToArray();
                LogEntry[] tail = new LogEntry[count];
                Array.Copy(all, all.Length - count, tail, 0, count);
                return tail;
            }
        }

        private void OnLog(string condition, string stackTrace, LogType type)
        {
            long unixMs = (long)(DateTime.UtcNow - UnixEpoch).TotalMilliseconds;

            lock (_logLock)
            {
                if (_logs.Count >= _capacity)
                {
                    _logs.Dequeue();
                }

                _logs.Enqueue(new LogEntry(type.ToString(), condition, unixMs));
            }

            Action<string, string, string, long> handler = LogReceived;
            if (handler != null)
            {
                handler(type.ToString(), condition, stackTrace, unixMs);
            }
        }
    }
}
