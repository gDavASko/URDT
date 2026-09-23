using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using KBP.URDT.Inspect;

namespace URDT.Runtime.Inspectors
{
    /// <summary>
    /// Auto-scaffolding instrumentation tool.
    /// Discovers all Selectable controls in the active scene and injects matching
    /// UrdtUiTarget proxy beacons (Button, Toggle, Slider, Dropdown, InputField, ScrollRect, Generic).
    /// Extracts semantic text labels without requiring manual developer markup.
    /// </summary>
    [DefaultExecutionOrder(-9900)]
    public sealed class UrdtAutoInstrumentation : MonoBehaviour
    {
        public static UrdtAutoInstrumentation Instance { get; private set; }

        private readonly List<UrdtUiTarget> _activeTargets = new List<UrdtUiTarget>(256);
        private static readonly Regex RichTextRegex = new Regex("<.*?>", RegexOptions.Compiled);

        private float _lastScanTime;
        private const float SCAN_INTERVAL = 0.5f; // 2 Hz hierarchy scan

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_AutoInstrumentation]");
                Instance = go.AddComponent<UrdtAutoInstrumentation>();
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
            if (Time.unscaledTime - _lastScanTime >= SCAN_INTERVAL)
            {
                _lastScanTime = Time.unscaledTime;
                ScanHierarchy();
            }
        }

        public IReadOnlyList<UrdtUiTarget> ActiveTargets => _activeTargets;

        public void ScanHierarchy()
        {
            _activeTargets.Clear();

            // Find all Selectables in the active scene
            var selectables = FindObjectsByType<Selectable>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            for (int i = 0; i < selectables.Length; i++)
            {
                var selectable = selectables[i];
                if (selectable == null || !selectable.gameObject.activeInHierarchy)
                {
                    continue;
                }

                var target = selectable.GetComponent<UrdtUiTarget>();
                if (target == null)
                {
                    target = InjectMatchingBeacon(selectable);
                }

                if (target != null)
                {
                    _activeTargets.Add(target);
                }
            }
        }

        public static UrdtUiTarget InjectMatchingBeacon(Selectable selectable)
        {
            if (selectable == null) return null;
            GameObject go = selectable.gameObject;

            UrdtUiTarget target;
            if (selectable is Button)
            {
                target = go.AddComponent<UrdtUiButtonTarget>();
            }
            else if (selectable is Toggle)
            {
                target = go.AddComponent<UrdtUiToggleTarget>();
            }
            else if (selectable is Slider)
            {
                target = go.AddComponent<UrdtUiSliderTarget>();
            }
            else if (selectable is Dropdown)
            {
                target = go.AddComponent<UrdtUiDropdownTarget>();
            }
            else if (selectable is InputField)
            {
                target = go.AddComponent<UrdtUiInputTarget>();
            }
            else if (go.GetComponent<ScrollRect>() != null)
            {
                target = go.AddComponent<UrdtUiScrollTarget>();
            }
            else
            {
                string typeName = selectable.GetType().Name.ToUpperInvariant();
                if (typeName.Contains("BUTTON"))
                    target = go.AddComponent<UrdtUiButtonTarget>();
                else if (typeName.Contains("TOGGLE"))
                    target = go.AddComponent<UrdtUiToggleTarget>();
                else if (typeName.Contains("SLIDER"))
                    target = go.AddComponent<UrdtUiSliderTarget>();
                else if (typeName.Contains("DROPDOWN"))
                    target = go.AddComponent<UrdtUiDropdownTarget>();
                else if (typeName.Contains("INPUT"))
                    target = go.AddComponent<UrdtUiInputTarget>();
                else
                    target = go.AddComponent<UrdtUiGenericTarget>();
            }

            return target;
        }

        public static string ExtractSemanticLabel(GameObject root)
        {
            if (root == null) return string.Empty;

            // 1. Check standard UnityEngine.UI.Text
            var uiTexts = root.GetComponentsInChildren<Text>(false);
            for (int i = 0; i < uiTexts.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(uiTexts[i].text))
                {
                    return CleanLabel(uiTexts[i].text);
                }
            }

            // 2. Check TextMeshPro via reflection (decoupled from asmdef direct reference)
            var components = root.GetComponentsInChildren<Component>(false);
            for (int i = 0; i < components.Length; i++)
            {
                var c = components[i];
                if (c == null) continue;

                string typeName = c.GetType().Name;
                if (typeName.Contains("TMP_Text") || typeName.Contains("TextMeshPro"))
                {
                    PropertyInfo textProp = c.GetType().GetProperty("text", BindingFlags.Public | BindingFlags.Instance);
                    if (textProp != null)
                    {
                        var val = textProp.GetValue(c) as string;
                        if (!string.IsNullOrWhiteSpace(val))
                        {
                            return CleanLabel(val);
                        }
                    }
                }
            }

            // 3. Fallback: GameObject name cleaned
            return CleanLabel(root.name);
        }

        public static string ComputeBeaconPath(GameObject go)
        {
            if (go == null) return "null";
            string path = go.name;
            Transform parent = go.transform.parent;
            int depth = 0;
            while (parent != null && depth < 4)
            {
                path = parent.name + "/" + path;
                parent = parent.parent;
                depth++;
            }
            return path.Replace(" ", "_");
        }

        private static string CleanLabel(string raw)
        {
            if (string.IsNullOrEmpty(raw)) return string.Empty;
            string clean = RichTextRegex.Replace(raw, string.Empty).Trim();
            clean = clean.Replace("\r", " ").Replace("\n", " ");
            while (clean.Contains("  "))
            {
                clean = clean.Replace("  ", " ");
            }
            return clean.Length > 64 ? clean.Substring(0, 64) : clean;
        }
    }
}
