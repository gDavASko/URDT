#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace UrdtSetup
{
    /// <summary>
    /// Env-gated auto-launch used by the external polygon orchestrator (Tools/run-polygon-orchestration.ps1).
    /// When the editor is opened with URDT_AUTOPLAY=1, it loads the polygon scene and enters Play Mode,
    /// so the polygon's own UrdtServerHost boots on :7777 for an external runner to drive. It is inert
    /// during normal editor use (the env var is only set for the orchestrated launch).
    /// </summary>
    [InitializeOnLoad]
    public static class UrdtOrchestrator
    {
        private const string ScenePath = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";
        private static int _settleFrames;

        static UrdtOrchestrator()
        {
            if (Environment.GetEnvironmentVariable("URDT_AUTOPLAY") != "1")
            {
                return;
            }

            _settleFrames = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            // Keep-alive diagnostic mode: while URDT_AUTOPLAY=1, re-enter Play Mode whenever the
            // editor drops back to Edit Mode, so an external probe always has a live :7777 server.
            if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                _settleFrames = 0;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _settleFrames = 0;
                return;
            }

            // Let the editor settle for a few idle frames before switching scenes / entering play.
            if (_settleFrames++ < 15)
            {
                return;
            }

            _settleFrames = 0;

            try
            {
                if (!EditorSceneManager.GetActiveScene().path.EndsWith("URDT_TestPoligon_UI.unity", StringComparison.Ordinal))
                {
                    EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
                }

                // Pin the Game View to a fixed 1080p resolution. The free-aspect Game View
                // otherwise matches the desktop (e.g. 4K), which pushes controls (dropdown lists,
                // modal) out of the external runner's fixed pixel-space coordinate scans.
                try { PlayModeWindow.SetCustomRenderingResolution(1920, 1080, "URDT 1080p"); }
                catch (Exception resExc) { Debug.LogWarning("URDT orchestrator: could not pin Game View resolution: " + resExc.Message); }

                EditorApplication.EnterPlaymode();
                Debug.Log("URDT orchestrator: opened polygon scene and requested Play Mode.");
            }
            catch (Exception exception)
            {
                Debug.LogError("URDT orchestrator autoplay failed: " + exception);
            }
        }
    }
}
#endif
