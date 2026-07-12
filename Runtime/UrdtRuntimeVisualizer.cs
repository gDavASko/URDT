using System.Collections.Generic;
using KBP.URDT.Driver;
using KBP.URDT.Input;
using KBP.URDT.Registry;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace KBP.URDT.Visualization
{
    /// <summary>
    /// Lightweight Game View overlay for URDT diagnostics. It visualizes commands and
    /// the actual pointer states pumped into the Unity Input System.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UrdtRuntimeVisualizer : MonoBehaviour
    {
        private const int MaxPointerSamples = 96;
        private const int MaxCommandEntries = 10;

        [SerializeField] private bool _visible = true;
        [SerializeField] private float _trailLifetime = 2.5f;

        private readonly List<PointerSample> _pointerSamples = new List<PointerSample>(MaxPointerSamples);
        private readonly List<string> _commandLog = new List<string>(MaxCommandEntries);

        private InputSimulator _input;
        private TestIdRegistry _registry;
        private GUIStyle _smallLabel;
        private Texture2D _pixel;

        public void Configure(InputSimulator input, TestIdRegistry registry)
        {
            if (_input != null)
            {
                _input.PointerInjected -= OnPointerInjected;
            }

            _input = input;
            _registry = registry;

            if (_input != null)
            {
                _input.PointerInjected += OnPointerInjected;
            }
        }

        public void RecordCommand(string action, JToken payload)
        {
            if (string.IsNullOrEmpty(action))
            {
                return;
            }

            string summary = action;
            string target = TryReadTarget(payload);
            if (!string.IsNullOrEmpty(target))
            {
                summary += " -> " + target;
            }

            summary += " f" + Time.frameCount;
            _commandLog.Insert(0, summary);
            while (_commandLog.Count > MaxCommandEntries)
            {
                _commandLog.RemoveAt(_commandLog.Count - 1);
            }
        }

        private void OnGUI()
        {
            if (!_visible)
            {
                return;
            }

            EnsureGuiResources();
            PrunePointerSamples();
            DrawPointerTrail();
            DrawHud();
        }

        private void OnDestroy()
        {
            Configure(null, null);
        }

        private void OnPointerInjected(PointerInputEvent inputEvent)
        {
            _pointerSamples.Add(new PointerSample(
                ToGuiPoint(inputEvent.ScreenPos),
                inputEvent.Phase,
                inputEvent.PointerId,
                inputEvent.Frame,
                Time.realtimeSinceStartup));

            while (_pointerSamples.Count > MaxPointerSamples)
            {
                _pointerSamples.RemoveAt(0);
            }
        }

        private void DrawHud()
        {
            Rect area = new Rect(12f, 12f, 360f, 28f + 18f * Mathf.Max(3, _commandLog.Count));
            GUILayout.BeginArea(area, GUI.skin.box);
            GUILayout.Label("URDT live view", _smallLabel);
            GUILayout.Label("registered: " + (_registry != null ? _registry.Count.ToString() : "0"), _smallLabel);

            for (int i = 0; i < _commandLog.Count; i++)
            {
                GUILayout.Label(_commandLog[i], _smallLabel);
            }

            GUILayout.EndArea();
        }

        private void DrawPointerTrail()
        {
            for (int i = 1; i < _pointerSamples.Count; i++)
            {
                PointerSample previous = _pointerSamples[i - 1];
                PointerSample current = _pointerSamples[i];
                if (previous.PointerId == current.PointerId)
                {
                    DrawLine(previous.GuiPoint, current.GuiPoint, ColorForPhase(current.Phase), 2f);
                }
            }

            for (int i = 0; i < _pointerSamples.Count; i++)
            {
                PointerSample sample = _pointerSamples[i];
                DrawDot(sample.GuiPoint, ColorForPhase(sample.Phase), sample.Phase == PointerPhase.Down ? 14f : 10f);
            }
        }

        private void DrawDot(Vector2 point, Color color, float size)
        {
            Color oldColor = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(point.x - size * 0.5f, point.y - size * 0.5f, size, size), _pixel);
            GUI.color = oldColor;
        }

        private void DrawLine(Vector2 from, Vector2 to, Color color, float width)
        {
            Vector2 delta = to - from;
            if (delta.sqrMagnitude <= 0.01f)
            {
                return;
            }

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;

            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;
            GUIUtility.RotateAroundPivot(angle, from);
            GUI.DrawTexture(new Rect(from.x, from.y - width * 0.5f, delta.magnitude, width), _pixel);

            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void PrunePointerSamples()
        {
            float now = Time.realtimeSinceStartup;
            for (int i = _pointerSamples.Count - 1; i >= 0; i--)
            {
                if (now - _pointerSamples[i].Time > _trailLifetime)
                {
                    _pointerSamples.RemoveAt(i);
                }
            }
        }

        private void EnsureGuiResources()
        {
            if (_smallLabel == null)
            {
                _smallLabel = new GUIStyle(GUI.skin.label)
                {
                    fontSize = 12,
                    normal = { textColor = Color.white }
                };
            }

            if (_pixel == null)
            {
                _pixel = Texture2D.whiteTexture;
            }
        }

        private static Vector2 ToGuiPoint(Vector2 screenPoint)
        {
            return new Vector2(screenPoint.x, Screen.height - screenPoint.y);
        }

        private static Color ColorForPhase(PointerPhase phase)
        {
            switch (phase)
            {
                case PointerPhase.Down:
                    return new Color(0.25f, 1f, 0.45f, 0.9f);
                case PointerPhase.Up:
                    return new Color(1f, 0.25f, 0.25f, 0.9f);
                default:
                    return new Color(0.2f, 0.75f, 1f, 0.8f);
            }
        }

        private static string TryReadTarget(JToken payload)
        {
            JObject obj = payload as JObject;
            if (obj == null)
            {
                return null;
            }

            string testId = ReadString(obj, "testId");
            if (!string.IsNullOrEmpty(testId))
            {
                return testId;
            }

            string handle = ReadString(obj, "handle");
            if (!string.IsNullOrEmpty(handle))
            {
                return handle;
            }

            JObject selector = obj["selector"] as JObject;
            if (selector != null)
            {
                string byComponent = ReadString(selector, "byComponent");
                if (!string.IsNullOrEmpty(byComponent))
                {
                    return byComponent;
                }
            }

            return null;
        }

        private static string ReadString(JObject obj, string key)
        {
            JToken token = obj[key];
            return token != null && token.Type != JTokenType.Null ? token.ToString() : null;
        }

        private readonly struct PointerSample
        {
            public readonly Vector2 GuiPoint;
            public readonly PointerPhase Phase;
            public readonly int PointerId;
            public readonly int Frame;
            public readonly float Time;

            public PointerSample(Vector2 guiPoint, PointerPhase phase, int pointerId, int frame, float time)
            {
                GuiPoint = guiPoint;
                Phase = phase;
                PointerId = pointerId;
                Frame = frame;
                Time = time;
            }
        }
    }
}
