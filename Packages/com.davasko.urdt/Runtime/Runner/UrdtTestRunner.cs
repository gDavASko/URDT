using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using KBP.URDT;
using URDT.Runtime.IPC;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace URDT.Runtime.Runner
{
    /// <summary>
    /// Data contract serialized to disk for zero-collision CI handshake synchronization.
    /// </summary>
    [Serializable]
    public struct UrdtHandshakeInfo
    {
        public int port;
        public string pipeName;
        public string token;
        public int pid;
        public string status;
    }

    /// <summary>
    /// Headless CI/CD test runner entry point invoked via Unity command-line:
    /// -executeMethod URDT.Runtime.Runner.UrdtTestRunner.RunHeadless
    /// Supports ephemeral port binding (port 0), memory recycling (exit 42), and watchdog timeouts.
    /// </summary>
    public static class UrdtTestRunner
    {
        public const int EXIT_CODE_SUCCESS = 0;
        public const int EXIT_CODE_FAILURE = 1;
        public const int EXIT_CODE_RECYCLE_REQUESTED = 42;

        public static event Action<int> OnAuditFinished;

        private static Timer s_watchdogTimer;

        /// <summary>
        /// Command line entry point for automated headless CI runs.
        /// </summary>
        public static void RunHeadless()
        {
            string[] args = Environment.GetCommandLineArgs();
            int port = 9002;
            string token = Guid.NewGuid().ToString("N");
            string handshakePath = null;
            string pipeName = UrdtNamedPipeServer.DEFAULT_PIPE_NAME;
            int maxDurationSec = 600;

            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] == "-urdtPort" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out port);
                }
                else if (args[i] == "-urdtToken" && i + 1 < args.Length)
                {
                    token = args[i + 1];
                }
                else if (args[i] == "-urdtHandshakeFile" && i + 1 < args.Length)
                {
                    handshakePath = args[i + 1];
                }
                else if (args[i] == "-urdtPipeName" && i + 1 < args.Length)
                {
                    pipeName = args[i + 1];
                }
                else if (args[i] == "-maxDuration" && i + 1 < args.Length)
                {
                    int.TryParse(args[i + 1], out maxDurationSec);
                }
            }

            Debug.Log($"[URDT] Starting Headless Test Runner: port={port}, pipe={pipeName}, timeout={maxDurationSec}s");

            // Configure pipe name if customized
            if (!string.IsNullOrEmpty(pipeName))
            {
                Environment.SetEnvironmentVariable("URDT_PIPE_NAME", pipeName);
            }

            // Ensure UrdtServerHost is instantiated and listening
            UrdtServerHost host = UrdtServerHost.Instance;
            if (host == null)
            {
                var go = new GameObject("[URDT_ServerHost]");
                host = go.AddComponent<UrdtServerHost>();
                UnityEngine.Object.DontDestroyOnLoad(go);
            }

            host.StartServer(port, token);

            int actualPort = host.Port > 0 ? host.Port : port;

            // Write atomic handshake file if requested
            if (!string.IsNullOrEmpty(handshakePath))
            {
                try
                {
                    string dir = Path.GetDirectoryName(handshakePath);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var info = new UrdtHandshakeInfo
                    {
                        port = actualPort,
                        pipeName = pipeName,
                        token = token,
                        pid = Process.GetCurrentProcess().Id,
                        status = "READY"
                    };

                    string json = JsonUtility.ToJson(info, true);
                    string tmpPath = handshakePath + ".tmp";
                    File.WriteAllText(tmpPath, json);

                    if (File.Exists(handshakePath))
                    {
                        File.Delete(handshakePath);
                    }
                    File.Move(tmpPath, handshakePath);

                    Debug.Log($"[URDT] Handshake file written to: {handshakePath} (port={actualPort}, pid={info.pid})");
                }
                catch (Exception ex)
                {
                    Debug.LogError($"[URDT] Failed writing handshake file: {ex}");
                }
            }

            // Start safety watchdog
            if (maxDurationSec > 0)
            {
                s_watchdogTimer = new Timer(_ =>
                {
                    Debug.LogError($"[URDT] CI Watchdog timeout reached ({maxDurationSec}s). Forcing termination.");
                    Terminate(EXIT_CODE_FAILURE);
                }, null, TimeSpan.FromSeconds(maxDurationSec), Timeout.InfiniteTimeSpan);
            }

            OnAuditFinished += (exitCode) =>
            {
                Terminate(exitCode);
            };
        }

        /// <summary>
        /// Explicitly completes the audit session, notifying listeners and terminating process.
        /// </summary>
        public static void FinishAudit(int exitCode = EXIT_CODE_SUCCESS)
        {
            Debug.Log($"[URDT] FinishAudit called with exit code: {exitCode}");
            OnAuditFinished?.Invoke(exitCode);
            Terminate(exitCode);
        }

        /// <summary>
        /// Request engine termination with specific exit code (e.g. 0 for Success, 42 for Recycle).
        /// </summary>
        public static void Terminate(int exitCode)
        {
            s_watchdogTimer?.Dispose();
            s_watchdogTimer = null;

            Debug.Log($"[URDT] Terminating engine with exit code: {exitCode} " +
                (exitCode == EXIT_CODE_RECYCLE_REQUESTED ? "(RECYCLE_REQUESTED)" :
                 exitCode == EXIT_CODE_SUCCESS ? "(SUCCESS)" : "(FAILURE)"));

            try
            {
                Resources.UnloadUnusedAssets();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true, true);
            }
            catch
            {
                // Best-effort cleanup before exit
            }

#if UNITY_EDITOR
            EditorApplication.Exit(exitCode);
#else
            Application.Quit(exitCode);
#endif
        }
    }
}
