#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Auto-launcher: Opens the URDT test scene and enters Play Mode.
/// Delete this file after use.
/// </summary>
[InitializeOnLoad]
public static class UrdtAutoLauncher
{
    static UrdtAutoLauncher()
    {
        // Delay to let Unity finish domain reload
        EditorApplication.delayCall += Launch;
    }

    private static void Launch()
    {
        const string scenePath = "Assets/URDT_TestPoligon/Scenes/URDT_TestPoligon_UI.unity";

        if (EditorApplication.isPlaying)
        {
            Debug.Log("[UrdtAutoLauncher] Already in Play Mode. Skipping.");
            return;
        }

        // Save current scene if dirty
        if (EditorSceneManager.GetActiveScene().isDirty)
        {
            EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo();
        }

        // Open the test scene
        Debug.Log($"[UrdtAutoLauncher] Opening scene: {scenePath}");
        EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

        // Enter Play Mode
        Debug.Log("[UrdtAutoLauncher] Entering Play Mode...");
        EditorApplication.isPlaying = true;

        Debug.Log("[UrdtAutoLauncher] Done. URDT server should start on port 9002.");
    }
}
#endif
