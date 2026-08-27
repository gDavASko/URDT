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
        private static bool _armed;
        private static int _settleFrames;

        static UrdtOrchestrator()
        {
            if (Environment.GetEnvironmentVariable("URDT_AUTOPLAY") != "1")
            {
                return;
            }

            _armed = true;
            _settleFrames = 0;
            EditorApplication.update += Tick;
        }

        private static void Tick()
        {
            if (!_armed || EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isPlaying)
            {
                _armed = false;
                EditorApplication.update -= Tick;
                return;
            }

            if (EditorApplication.isCompiling || EditorApplication.isUpdating)
            {
                _settleFrames = 0;
                return;
            }

            // Let the editor settle for a few idle frames before switching scenes / entering play.
            if (_settleFrames++ < 10)
            {
                return;
            }

            _armed = false;
            EditorApplication.update -= Tick;

            try
            {
                EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
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
