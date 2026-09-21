using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace KBP.URDT.TestPoligon.Editor
{
    /// <summary>
    /// Builds a standalone Windows 64-bit player for the URDT Test Polygon scene.
    /// Provides lightning-fast, headless-compatible execution without Editor GUI overhead.
    /// </summary>
    public static class UrdtStandaloneBuilder
    {
        private const string SCENE_PATH = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";
        private const string OUTPUT_EXE = "Builds/URDT_TestPoligon/URDT_TestPoligon.exe";

        [MenuItem("Tools/URDT 2D Polygon/Build Standalone Player")]
        public static void BuildStandalonePlayer()
        {
            Debug.Log("<color=#00FFFF>[UrdtStandaloneBuilder] Starting standalone player build...</color>");

            string[] scenes = new[] { SCENE_PATH };
            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = OUTPUT_EXE,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.Development | BuildOptions.AllowDebugging
            };

            BuildReport report = BuildPipeline.BuildPlayer(options);
            BuildSummary summary = report.summary;

            if (summary.result == BuildResult.Succeeded)
            {
                Debug.Log($"<color=#00FF99>[UrdtStandaloneBuilder] Build SUCCEEDED: {OUTPUT_EXE} ({summary.totalSize / (1024 * 1024):F1} MB, took {summary.totalTime.TotalSeconds:F1}s)</color>");
            }
            else
            {
                Debug.LogError($"<color=#FF3366>[UrdtStandaloneBuilder] Build FAILED: {summary.result} (errors: {summary.totalErrors})</color>");
            }
        }
    }
}
