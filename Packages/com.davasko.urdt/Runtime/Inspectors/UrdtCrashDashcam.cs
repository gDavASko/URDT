using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace URDT.Runtime.Inspectors
{
    /// <summary>
    /// In-Memory rolling 5-second crash dashcam (25 JPEG frames @ 5 FPS, ~1.5 MB total RAM).
    /// Captures low-resolution visual evidence without disk I/O.
    /// Freezes upon exceptions, soft-locks, or invariant failures, exporting base64 frames into defect evidence.
    /// </summary>
    [DefaultExecutionOrder(-9980)]
    public sealed class UrdtCrashDashcam : MonoBehaviour
    {
        public const int BUFFER_SIZE = 25;       // 5 seconds @ 5 FPS
        public const float SAMPLE_INTERVAL = 0.2f; // 200 ms
        public const int CAPTURE_WIDTH = 480;
        public const int CAPTURE_HEIGHT = 270;
        public const int JPEG_QUALITY = 60;

        public struct DashcamFrame
        {
            public byte[] JpegBytes;
            public long TimestampMs;
            public int FrameIndex;
        }

        public static UrdtCrashDashcam Instance { get; private set; }

        private readonly DashcamFrame[] _ringBuffer = new DashcamFrame[BUFFER_SIZE];
        private int _writeIndex;
        private int _totalCaptured;
        private bool _isFrozen;
        private float _frozenAtRealtime;

        /// <summary>A frozen buffer is kept for this long (evidence window), then recording resumes.</summary>
        public const float FREEZE_HOLD_SECONDS = 10f;
        private Coroutine _captureCoroutine;

        private RenderTexture _captureRt;
        private Texture2D _readbackTex;

        public bool IsFrozen => _isFrozen;
        public int TotalCaptured => _totalCaptured;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_CrashDashcam]");
                Instance = go.AddComponent<UrdtCrashDashcam>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            Application.logMessageReceivedThreaded += OnLogMessageReceived;
            StartRecording();
        }

        private void OnDestroy()
        {
            StopRecording();
            Application.logMessageReceivedThreaded -= OnLogMessageReceived;

            if (_captureRt != null)
            {
                _captureRt.Release();
                Destroy(_captureRt);
            }
            if (_readbackTex != null)
            {
                Destroy(_readbackTex);
            }

            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (type == LogType.Exception || type == LogType.Assert)
            {
                Freeze();
            }
        }

        public void StartRecording()
        {
            if (_captureCoroutine != null) return;
            _isFrozen = false;
            _captureCoroutine = StartCoroutine(CaptureLoop());
        }

        public void StopRecording()
        {
            if (_captureCoroutine != null)
            {
                StopCoroutine(_captureCoroutine);
                _captureCoroutine = null;
            }
        }

        public void Freeze()
        {
            if (_isFrozen) return;
            _isFrozen = true;
            _frozenAtRealtime = -1f; // stamped on the main thread by the capture loop
            Debug.Log($"[URDT] Crash Dashcam FROZEN. Preserved {_totalCaptured} frames in RAM buffer.");
        }

        public void ResumeRecording()
        {
            _isFrozen = false;
            Debug.Log("[URDT] Crash Dashcam RESUMED recording.");
        }

        private IEnumerator CaptureLoop()
        {
            var waitInterval = new WaitForSecondsRealtime(SAMPLE_INTERVAL);

            while (true)
            {
                yield return new WaitForEndOfFrame();

                if (_isFrozen)
                {
                    // Freeze() may be called from a logging thread; stamp and expire the hold on the main thread
                    // so one exception does not disable the dashcam for the rest of the session.
                    if (_frozenAtRealtime < 0f) _frozenAtRealtime = Time.realtimeSinceStartup;
                    else if (Time.realtimeSinceStartup - _frozenAtRealtime > FREEZE_HOLD_SECONDS) _isFrozen = false;
                }
                else if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
                {
                    CaptureCurrentFrame();
                }

                yield return waitInterval;
            }
        }

        private void CaptureCurrentFrame()
        {
            try
            {
                if (_captureRt == null)
                {
                    _captureRt = new RenderTexture(CAPTURE_WIDTH, CAPTURE_HEIGHT, 0, RenderTextureFormat.ARGB32)
                    {
                        filterMode = FilterMode.Bilinear
                    };
                    _captureRt.Create();
                }

                if (_readbackTex == null)
                {
                    _readbackTex = new Texture2D(CAPTURE_WIDTH, CAPTURE_HEIGHT, TextureFormat.RGB24, false);
                }

                ScreenCapture.CaptureScreenshotIntoRenderTexture(_captureRt);

                RenderTexture prev = RenderTexture.active;
                RenderTexture.active = _captureRt;
                _readbackTex.ReadPixels(new Rect(0, 0, CAPTURE_WIDTH, CAPTURE_HEIGHT), 0, 0, false);
                _readbackTex.Apply(false);
                RenderTexture.active = prev;

                byte[] jpeg = _readbackTex.EncodeToJPG(JPEG_QUALITY);

                long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

                _ringBuffer[_writeIndex] = new DashcamFrame
                {
                    JpegBytes = jpeg,
                    TimestampMs = now,
                    FrameIndex = _totalCaptured
                };

                _writeIndex = (_writeIndex + 1) % BUFFER_SIZE;
                _totalCaptured++;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URDT] Dashcam capture error: {ex.Message}");
            }
        }

        /// <summary>
        /// Exports all valid recorded frames in chronological order as base64 data strings.
        /// </summary>
        public string[] ExportBase64Frames()
        {
            var list = new List<string>(BUFFER_SIZE);
            int count = Mathf.Min(_totalCaptured, BUFFER_SIZE);
            int startIndex = (_totalCaptured < BUFFER_SIZE) ? 0 : _writeIndex;

            for (int i = 0; i < count; i++)
            {
                int idx = (startIndex + i) % BUFFER_SIZE;
                var frame = _ringBuffer[idx];
                if (frame.JpegBytes != null && frame.JpegBytes.Length > 0)
                {
                    list.Add("data:image/jpeg;base64," + Convert.ToBase64String(frame.JpegBytes));
                }
            }

            return list.ToArray();
        }

        /// <summary>
        /// Exports the most recent frame as a base64 JPEG string, or null if no frames captured.
        /// </summary>
        public string ExportLatestFrameBase64()
        {
            if (_totalCaptured == 0) return null;
            int lastIndex = (_writeIndex - 1 + BUFFER_SIZE) % BUFFER_SIZE;
            var frame = _ringBuffer[lastIndex];
            if (frame.JpegBytes != null && frame.JpegBytes.Length > 0)
            {
                return "data:image/jpeg;base64," + Convert.ToBase64String(frame.JpegBytes);
            }
            return null;
        }
    }
}
