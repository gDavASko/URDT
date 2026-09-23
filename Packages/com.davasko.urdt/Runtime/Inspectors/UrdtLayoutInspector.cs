using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace URDT.Runtime.Inspectors
{
    public enum LayoutDefectType
    {
        LAYOUT_TEXT_TRUNCATED,
        LAYOUT_BOUNDS_OVERFLOW,
        LAYOUT_FONT_DEGRADATION,
        LAYOUT_MISSING_GLYPHS
    }

    [Serializable]
    public struct LayoutDefect
    {
        public LayoutDefectType DefectType;
        public string GameObjectName;
        public string Path;
        public string Details;
        public long TimestampMs;
    }

    /// <summary>
    /// Tier 3 Layout and Localization Inspector.
    /// Detects text truncation, bounds overflows, auto-sizing font degradation (< 10pt),
    /// and missing glyphs (\uFFFD).
    /// </summary>
    [DefaultExecutionOrder(-9890)]
    public sealed class UrdtLayoutInspector : MonoBehaviour
    {
        public static UrdtLayoutInspector Instance { get; private set; }

        private readonly List<LayoutDefect> _detectedDefects = new List<LayoutDefect>(64);
        public IReadOnlyList<LayoutDefect> DetectedDefects => _detectedDefects;

        private float _lastAuditTime;
        private const float AUDIT_INTERVAL = 1.0f; // 1 Hz audit

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_LayoutInspector]");
                Instance = go.AddComponent<UrdtLayoutInspector>();
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
        }

        private void Update()
        {
            if (Time.unscaledTime - _lastAuditTime >= AUDIT_INTERVAL)
            {
                _lastAuditTime = Time.unscaledTime;
                AuditActiveCanvases();
            }
        }

        public void AuditActiveCanvases()
        {
            _detectedDefects.Clear();

            // 1. Audit standard UnityEngine.UI.Text components
            var texts = FindObjectsByType<Text>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            long nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            for (int i = 0; i < texts.Length; i++)
            {
                var t = texts[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;

                string content = t.text;
                if (string.IsNullOrEmpty(content)) continue;

                // Check missing glyphs
                if (content.Contains("\uFFFD"))
                {
                    _detectedDefects.Add(new LayoutDefect
                    {
                        DefectType = LayoutDefectType.LAYOUT_MISSING_GLYPHS,
                        GameObjectName = t.gameObject.name,
                        Path = UrdtAutoInstrumentation.ComputeBeaconPath(t.gameObject),
                        Details = $"Text contains unresolved replacement character \\uFFFD in: \"{content.Substring(0, Math.Min(32, content.Length))}\"",
                        TimestampMs = nowMs
                    });
                }

                // Check font size degradation
                if (t.fontSize < 10)
                {
                    _detectedDefects.Add(new LayoutDefect
                    {
                        DefectType = LayoutDefectType.LAYOUT_FONT_DEGRADATION,
                        GameObjectName = t.gameObject.name,
                        Path = UrdtAutoInstrumentation.ComputeBeaconPath(t.gameObject),
                        Details = $"Font size degraded to {t.fontSize}pt (< 10.0pt threshold)",
                        TimestampMs = nowMs
                    });
                }

                // Check bounds overflow relative to parent RectTransform
                CheckBoundsOverflow(t.rectTransform, nowMs);
            }

            // 2. Audit TextMeshPro via reflection
            AuditTextMeshProComponents(nowMs);
        }

        private void CheckBoundsOverflow(RectTransform childRect, long nowMs)
        {
            if (childRect == null || childRect.parent == null) return;
            var parentRect = childRect.parent as RectTransform;
            if (parentRect == null) return;

            // Only check if parent is an interactive control or panel
            if (parentRect.GetComponent<Selectable>() == null) return;

            Vector3[] childCorners = new Vector3[4];
            Vector3[] parentCorners = new Vector3[4];
            childRect.GetWorldCorners(childCorners);
            parentRect.GetWorldCorners(parentCorners);

            // If child extends beyond parent by > 8 pixels in world coordinate space
            float childMinX = Mathf.Min(childCorners[0].x, childCorners[2].x);
            float childMaxX = Mathf.Max(childCorners[0].x, childCorners[2].x);
            float parentMinX = Mathf.Min(parentCorners[0].x, parentCorners[2].x);
            float parentMaxX = Mathf.Max(parentCorners[0].x, parentCorners[2].x);

            if (childMinX < parentMinX - 8.0f || childMaxX > parentMaxX + 8.0f)
            {
                _detectedDefects.Add(new LayoutDefect
                {
                    DefectType = LayoutDefectType.LAYOUT_BOUNDS_OVERFLOW,
                    GameObjectName = childRect.gameObject.name,
                    Path = UrdtAutoInstrumentation.ComputeBeaconPath(childRect.gameObject),
                    Details = $"Child bounds [{childMinX:F1}..{childMaxX:F1}] exceed parent container [{parentMinX:F1}..{parentMaxX:F1}]",
                    TimestampMs = nowMs
                });
            }
        }

        private void AuditTextMeshProComponents(long nowMs)
        {
            var components = FindObjectsByType<Component>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;

                string typeName = c.GetType().Name;
                if (!typeName.Contains("TMP_Text") && !typeName.Contains("TextMeshPro"))
                {
                    continue;
                }

                Type type = c.GetType();
                PropertyInfo textProp = type.GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                string textVal = textProp?.GetValue(c) as string;

                if (!string.IsNullOrEmpty(textVal))
                {
                    if (textVal.Contains("\uFFFD"))
                    {
                        _detectedDefects.Add(new LayoutDefect
                        {
                            DefectType = LayoutDefectType.LAYOUT_MISSING_GLYPHS,
                            GameObjectName = c.gameObject.name,
                            Path = UrdtAutoInstrumentation.ComputeBeaconPath(c.gameObject),
                            Details = $"TMP text contains \\uFFFD in: \"{textVal.Substring(0, Math.Min(32, textVal.Length))}\"",
                            TimestampMs = nowMs
                        });
                    }
                }

                // Check isTextTruncated or isTextOverflowing property
                PropertyInfo overflowProp = type.GetProperty("isTextOverflowing", BindingFlags.Public | BindingFlags.Instance)
                    ?? type.GetProperty("isTextTruncated", BindingFlags.Public | BindingFlags.Instance);
                if (overflowProp != null)
                {
                    var isOverflow = overflowProp.GetValue(c);
                    if (isOverflow is bool b && b)
                    {
                        _detectedDefects.Add(new LayoutDefect
                        {
                            DefectType = LayoutDefectType.LAYOUT_TEXT_TRUNCATED,
                            GameObjectName = c.gameObject.name,
                            Path = UrdtAutoInstrumentation.ComputeBeaconPath(c.gameObject),
                            Details = "TMP text is overflowing/truncated without scroll capability",
                            TimestampMs = nowMs
                        });
                    }
                }

                // Check fontSize
                PropertyInfo fontSizeProp = type.GetProperty("fontSize", BindingFlags.Public | BindingFlags.Instance);
                if (fontSizeProp != null)
                {
                    var fs = fontSizeProp.GetValue(c);
                    if (fs is float f && f < 10.0f && f > 0.0f)
                    {
                        _detectedDefects.Add(new LayoutDefect
                        {
                            DefectType = LayoutDefectType.LAYOUT_FONT_DEGRADATION,
                            GameObjectName = c.gameObject.name,
                            Path = UrdtAutoInstrumentation.ComputeBeaconPath(c.gameObject),
                            Details = $"TMP font size degraded to {f:F1}pt (< 10.0pt threshold)",
                            TimestampMs = nowMs
                        });
                    }
                }
            }
        }
    }
}
