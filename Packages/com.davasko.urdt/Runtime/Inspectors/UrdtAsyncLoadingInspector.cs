using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace URDT.Runtime.Inspectors
{
    /// <summary>
    /// Tier 4 Async Asset Streaming and CDN Monitor.
    /// Distinguishes legitimate async loading / downloads from UI freezes and soft-locks.
    /// Flags CDN stalls when download progress freezes for >= 15 seconds.
    /// </summary>
    [DefaultExecutionOrder(-9880)]
    public sealed class UrdtAsyncLoadingInspector : MonoBehaviour
    {
        public static UrdtAsyncLoadingInspector Instance { get; private set; }

        public bool IsLoadingActive { get; private set; }
        public bool IsStreamingStalled { get; private set; }
        public float CurrentProgress { get; private set; }
        public string StallReason { get; private set; }

        private float _lastProgressChangeTime;
        private float _lastRecordedProgress = -1f;
        private const float STALL_THRESHOLD_SECONDS = 15.0f;
        private const float PROGRESS_DELTA_EPSILON = 0.001f;

        private readonly List<Slider> _activeSliders = new List<Slider>(8);
        private readonly List<Image> _filledImages = new List<Image>(16);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_AsyncLoadingInspector]");
                Instance = go.AddComponent<UrdtAsyncLoadingInspector>();
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
            _lastProgressChangeTime = Time.unscaledTime;
        }

        private void Update()
        {
            InspectLoadingIndicators();
        }

        public void InspectLoadingIndicators()
        {
            float maxProgress = 0f;
            bool foundIndicator = false;

            // 1. Inspect Sliders
            _activeSliders.Clear();
            var sliders = FindObjectsByType<Slider>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < sliders.Length; i++)
            {
                var s = sliders[i];
                if (s == null || !s.gameObject.activeInHierarchy) continue;

                // Typical loading slider names: "Loading", "Progress", "Download", etc.
                string name = s.gameObject.name.ToLowerInvariant();
                if (name.Contains("load") || name.Contains("progress") || name.Contains("download") || name.Contains("bar"))
                {
                    float normalized = s.maxValue > s.minValue ? (s.value - s.minValue) / (s.maxValue - s.minValue) : 0f;
                    if (normalized > maxProgress) maxProgress = normalized;
                    foundIndicator = true;
                }
            }

            // 2. Inspect Filled Images
            _filledImages.Clear();
            var images = FindObjectsByType<Image>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < images.Length; i++)
            {
                var img = images[i];
                if (img == null || !img.gameObject.activeInHierarchy) continue;

                if (img.type == Image.Type.Filled)
                {
                    string name = img.gameObject.name.ToLowerInvariant();
                    if (name.Contains("load") || name.Contains("progress") || name.Contains("fill") || name.Contains("download"))
                    {
                        if (img.fillAmount > maxProgress) maxProgress = img.fillAmount;
                        foundIndicator = true;
                    }
                }
            }

            if (foundIndicator && maxProgress < 1.0f)
            {
                IsLoadingActive = true;
                CurrentProgress = maxProgress;

                if (Mathf.Abs(CurrentProgress - _lastRecordedProgress) > PROGRESS_DELTA_EPSILON)
                {
                    _lastRecordedProgress = CurrentProgress;
                    _lastProgressChangeTime = Time.unscaledTime;
                    IsStreamingStalled = false;
                    StallReason = null;
                }
                else
                {
                    float stallDuration = Time.unscaledTime - _lastProgressChangeTime;
                    if (stallDuration >= STALL_THRESHOLD_SECONDS)
                    {
                        IsStreamingStalled = true;
                        StallReason = $"ASSET_STREAMING_STALL: Progress frozen at {CurrentProgress * 100f:F1}% for {stallDuration:F1}s (>= 15s threshold)";
                    }
                }
            }
            else
            {
                IsLoadingActive = false;
                IsStreamingStalled = false;
                _lastRecordedProgress = -1f;
                _lastProgressChangeTime = Time.unscaledTime;
            }
        }
    }
}
