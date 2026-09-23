using System;
using System.IO;
using UnityEngine;

namespace KBP.URDT
{
    /// <summary>
    /// The game's own URDT knowledge folder: everything the testing agent learns about THIS game (application map,
    /// facts, tuned parameters, game-specific skills, skill statistics) lives next to the game project, not in the
    /// agent. Editor: &lt;project&gt;/URDT_Knowledge (outside Assets, so Unity never imports it). Player builds:
    /// &lt;build folder&gt;/URDT_Knowledge. Resolved once on the main thread and reported in the handshake/health, so
    /// the agent opens it before anything else.
    /// </summary>
    public static class UrdtKnowledge
    {
        public const string FOLDER_NAME = "URDT_Knowledge";

        public static string RootPath { get; private set; } = string.Empty;
        public static string ProductName { get; private set; } = string.Empty;
        public static string AppVersion { get; private set; } = string.Empty;
        /// <summary>Identifies the build the knowledge was gathered on (facts are re-validated when it changes).</summary>
        public static string BuildId { get; private set; } = string.Empty;

        /// <summary>Call from the main thread (Unity APIs). Idempotent.</summary>
        public static void Resolve()
        {
            if (!string.IsNullOrEmpty(RootPath)) return;
            try
            {
                // dataPath: <project>/Assets in the Editor, <build>/<Product>_Data in a player.
                string root = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
                RootPath = Path.Combine(root, FOLDER_NAME).Replace(Path.DirectorySeparatorChar, '/');
                Directory.CreateDirectory(RootPath);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[URDT] knowledge folder unavailable: {e.Message}");
                RootPath = string.Empty;
            }

            ProductName = Application.productName;
            AppVersion = Application.version;
            // Player: the build GUID. Editor: a fingerprint of the compiled game code (module version ids of the
            // project's own assemblies change on every recompile), so knowledge learned on older code is re-validated.
            string guid = Application.isEditor ? "editor-" + CodeFingerprint() : Application.buildGUID;
            BuildId = $"{Application.version}+{(string.IsNullOrEmpty(guid) ? "unknown" : guid)}";
        }
        private static string CodeFingerprint()
        {
            unchecked
            {
                ulong h = 1469598103934665603UL;
                var names = new System.Collections.Generic.List<string>();
                foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string n = a.GetName().Name;
                    if (a.IsDynamic || n.StartsWith("Unity") || n.StartsWith("System") || n.StartsWith("Mono") || n.StartsWith("mscorlib")
                        || n.StartsWith("netstandard") || n.StartsWith("nunit") || n.StartsWith("Newtonsoft") || n.StartsWith("Bee.")
                        || n.StartsWith("JetBrains") || n.StartsWith("ExCSS") || n.StartsWith("Microsoft")) continue;
                    names.Add(n + ":" + a.ManifestModule.ModuleVersionId.ToString("N"));
                }
                names.Sort(StringComparer.Ordinal);
                foreach (string s in names) foreach (char c in s) { h ^= c; h *= 1099511628211UL; }
                return h.ToString("x16").Substring(0, 10);
            }
        }
    }
}
