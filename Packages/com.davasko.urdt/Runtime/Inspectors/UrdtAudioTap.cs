using System;
using System.Collections.Generic;
using UnityEngine;

namespace URDT.Runtime.Inspectors
{
    /// <summary>
    /// Game audio tap for the L3 agent's hearing: the final mix reaching the AudioListener is downmixed to mono,
    /// decimated to 16 kHz and kept in a 30 s RAM ring buffer (no OS recording device, works minimized).
    /// It also logs which AudioSource started which clip on which object — cheap, exact sound cues.
    /// Read through the URDT <c>audio</c> command; nothing is written to disk.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrdtAudioTap : MonoBehaviour
    {
        public const int TARGET_RATE = 16000;
        public const int SECONDS = 30;

        public struct SoundEvent
        {
            public long SampleIndex;
            public string Source;
            public string Clip;
            public float Volume;
        }

        public static UrdtAudioTap Instance { get; private set; }

        private readonly float[] _ring = new float[TARGET_RATE * SECONDS];
        private readonly object _lock = new object();
        private long _written;          // total 16 kHz samples ever written
        private int _inputRate = 48000;
        private double _phase;
        private float _acc;
        private int _accN;

        private readonly List<SoundEvent> _events = new List<SoundEvent>(128);
        private readonly Dictionary<int, int> _playingClip = new Dictionary<int, int>();
        private readonly Dictionary<int, float> _lastTime = new Dictionary<int, float>();
        private float _nextSourceScan;

        /// <summary>Attaches the tap to the active AudioListener (idempotent).</summary>
        public static void Ensure()
        {
            if (Instance != null && Instance.isActiveAndEnabled) return;
            AudioListener listener = UnityEngine.Object.FindAnyObjectByType<AudioListener>();
            if (listener == null) return;
            if (!listener.TryGetComponent(out UrdtAudioTap tap)) tap = listener.gameObject.AddComponent<UrdtAudioTap>();
            Instance = tap;
        }

        private void Awake()
        {
            Instance = this;
            _inputRate = AudioSettings.outputSampleRate;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // Audio thread.
        private void OnAudioFilterRead(float[] data, int channels)
        {
            if (channels <= 0) return;
            double step = (double)TARGET_RATE / _inputRate;
            lock (_lock)
            {
                for (int i = 0; i < data.Length; i += channels)
                {
                    float mono = 0f;
                    for (int c = 0; c < channels; c++) mono += data[i + c];
                    _acc += mono / channels;
                    _accN++;
                    _phase += step;
                    if (_phase >= 1.0)
                    {
                        _phase -= 1.0;
                        _ring[(int)(_written % _ring.Length)] = _acc / _accN;
                        _written++;
                        _acc = 0f;
                        _accN = 0;
                    }
                }
            }
        }

        private void Update()
        {
            if (Time.unscaledTime < _nextSourceScan) return;
            _nextSourceScan = Time.unscaledTime + 0.1f;
            AudioSource[] sources = UnityEngine.Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
            long now;
            lock (_lock) now = _written;
            for (int i = 0; i < sources.Length; i++)
            {
                AudioSource s = sources[i];
                int id = s.GetInstanceID();
                int clip = s.isPlaying && s.clip != null ? s.clip.GetInstanceID() : 0;
                _playingClip.TryGetValue(id, out int prev);
                _lastTime.TryGetValue(id, out float lastT);
                bool restarted = clip != 0 && clip == prev && s.time + 0.05f < lastT;   // same clip played again
                _lastTime[id] = s.isPlaying ? s.time : 0f;
                if (clip != 0 && (clip != prev || restarted))
                {
                    lock (_lock)
                    {
                        _events.Add(new SoundEvent { SampleIndex = now, Source = s.gameObject.name, Clip = s.clip.name, Volume = s.volume });
                        if (_events.Count > 128) _events.RemoveAt(0);
                    }
                }
                _playingClip[id] = clip;
            }
        }

        /// <summary>Copies up to <paramref name="maxSeconds"/> of 16 kHz mono audio written after <paramref name="sinceSample"/>.</summary>
        public float[] Read(long sinceSample, float maxSeconds, out long fromSample, out long toSample)
        {
            lock (_lock)
            {
                toSample = _written;
                long oldest = Math.Max(0, _written - _ring.Length);
                long start = Math.Max(Math.Max(sinceSample, oldest), _written - (long)(maxSeconds * TARGET_RATE));
                fromSample = start;
                int n = (int)(toSample - start);
                float[] outBuf = new float[Math.Max(0, n)];
                for (int i = 0; i < n; i++) outBuf[i] = _ring[(int)((start + i) % _ring.Length)];
                return outBuf;
            }
        }

        public List<SoundEvent> EventsSince(long sinceSample)
        {
            lock (_lock) return _events.FindAll(e => e.SampleIndex >= sinceSample);
        }

        public long WrittenSamples
        {
            get { lock (_lock) return _written; }
        }
    }
}
