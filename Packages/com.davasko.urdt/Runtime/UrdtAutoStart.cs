using System;
using UnityEngine;

namespace KBP.URDT
{
    /// <summary>
    /// Zero-setup server start for any project that has the URDT package installed.
    ///
    ///  - Editor Play Mode: starts automatically (disable with env URDT_AUTOSTART=0).
    ///  - Development builds: only with the command-line flag -urdt (or env URDT_AUTOSTART=1).
    ///  - Release builds: never.
    ///  - Port: -urdtPort N | env URDT_PORT | 7777 (the host searches upward if busy).
    ///  - Token: -urdtToken T | env URDT_TOKEN | "urdt-local". The server listens on 127.0.0.1 only.
    ///
    /// If the scene already has its own <see cref="UrdtServerHost"/> (a project bootstrap), nothing is done.
    /// </summary>
    public static class UrdtAutoStart
    {
        public const int DEFAULT_PORT = 7777;
        public const string DEFAULT_TOKEN = "urdt-local";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (UrdtServerHost.Instance != null) return;          // the project starts its own server
            string env = Environment.GetEnvironmentVariable("URDT_AUTOSTART");
            bool flag = HasArg("-urdt");
            bool enabled = Application.isEditor ? env != "0" : (Debug.isDebugBuild && (flag || env == "1"));
            if (!enabled) return;

            int port = DEFAULT_PORT;
            string p = ArgValue("-urdtPort") ?? Environment.GetEnvironmentVariable("URDT_PORT");
            if (!string.IsNullOrEmpty(p) && int.TryParse(p, out int parsed)) port = parsed;
            string token = ArgValue("-urdtToken") ?? Environment.GetEnvironmentVariable("URDT_TOKEN") ?? DEFAULT_TOKEN;

            var go = new GameObject("[URDT_Server]");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var host = go.AddComponent<UrdtServerHost>();
            host.StartServer(port, token);
            Debug.Log($"[URDT] auto-started on ws://127.0.0.1:{port}/ (token from {(ArgValue("-urdtToken") != null ? "args" : Environment.GetEnvironmentVariable("URDT_TOKEN") != null ? "env" : "default")})");
        }

        private static bool HasArg(string name)
        {
            foreach (string a in Environment.GetCommandLineArgs()) if (a == name) return true;
            return false;
        }

        private static string ArgValue(string name)
        {
            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++) if (args[i] == name) return args[i + 1];
            return null;
        }
    }
}
