using System;
using System.IO;
using System.Diagnostics;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Discovery
{
    /// <summary>
    /// Publishes one process-owned discovery record to a global registry and to the
    /// Unity project's Library folder. Records are replaced atomically and removed by
    /// the owner on shutdown.
    /// </summary>
    public sealed class UrdtInstanceRegistry : IDisposable
    {
        private const int REGISTRY_SCHEMA = 1;

        private readonly UrdtInstanceIdentity _identity;
        private readonly UTF8Encoding _encoding = new UTF8Encoding(false);

        /// <summary>Gets the latest non-fatal registry I/O error.</summary>
        public string LastError { get; private set; } = string.Empty;

        /// <summary>Creates registry paths for one URDT instance.</summary>
        public UrdtInstanceRegistry(UrdtInstanceIdentity identity)
        {
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));

            string localApplicationData = Environment.GetFolderPath(
                Environment.SpecialFolder.LocalApplicationData);
            GlobalRecordPath = Path.Combine(
                localApplicationData,
                "DavASko",
                "URDT",
                "instances",
                identity.InstanceId + ".json");
            ProjectRecordPath = Path.Combine(
                identity.ProjectPath,
                "Library",
                "URDT",
                "active-instance.json");

            PruneStaleGlobalRecords(Path.GetDirectoryName(GlobalRecordPath));
        }

        /// <summary>Gets the machine-wide discovery record path.</summary>
        public string GlobalRecordPath { get; }

        /// <summary>Gets the project-local discovery record path.</summary>
        public string ProjectRecordPath { get; }

        /// <summary>Publishes current endpoint and health information.</summary>
        public void Publish(
            int port,
            string serverState,
            bool runtimeReady,
            string sceneName,
            long mainThreadTickUtcMs,
            long activeConnectionId)
        {
            JObject record = new JObject
            {
                ["schema"] = REGISTRY_SCHEMA,
                ["projectId"] = _identity.ProjectId,
                ["projectPathHash"] = _identity.ProjectPathHash,
                ["projectPath"] = _identity.ProjectPath,
                ["instanceId"] = _identity.InstanceId,
                ["runId"] = _identity.RunId,
                ["peerRole"] = _identity.PeerRole,
                ["processId"] = _identity.ProcessId,
                ["port"] = port,
                ["serverState"] = serverState,
                ["runtimeReady"] = runtimeReady,
                ["scene"] = sceneName ?? string.Empty,
                ["activeConnectionId"] = activeConnectionId,
                ["mainThreadTickUtcMs"] = mainThreadTickUtcMs,
                ["heartbeatUtcMs"] = UnixNowMs()
            };

            string json = record.ToString(Formatting.Indented);
            string globalError = TryWriteAtomic(GlobalRecordPath, json);
            string projectError = TryWriteAtomic(ProjectRecordPath, json);
            LastError = string.Join("; ", new[] { globalError, projectError })
                .Trim(' ', ';');
        }

        /// <summary>Removes records owned by this process.</summary>
        public void Dispose()
        {
            DeleteOwnedRecord(GlobalRecordPath);
            DeleteOwnedRecord(ProjectRecordPath);
        }

        private void DeleteOwnedRecord(string path)
        {
            try
            {
                if (!File.Exists(path))
                {
                    return;
                }

                JObject record = JObject.Parse(File.ReadAllText(path));
                if (string.Equals(
                    record["instanceId"] != null ? record["instanceId"].ToString() : null,
                    _identity.InstanceId,
                    StringComparison.Ordinal))
                {
                    File.Delete(path);
                }
            }
            catch (Exception)
            {
                // Registry cleanup is best effort; stale records are rejected by heartbeat and PID.
            }
        }

        private string TryWriteAtomic(string path, string content)
        {
            try
            {
                string directory = Path.GetDirectoryName(path);
                if (string.IsNullOrEmpty(directory))
                {
                    return "Registry path has no parent directory: " + path;
                }

                Directory.CreateDirectory(directory);
                string temporaryPath = path + "." + _identity.ProcessId + ".tmp";
                File.WriteAllText(temporaryPath, content, _encoding);

                if (File.Exists(path))
                {
                    File.Delete(path);
                }

                File.Move(temporaryPath, path);
                return string.Empty;
            }
            catch (Exception exception)
            {
                return path + ": " + exception.Message;
            }
        }

        private static void PruneStaleGlobalRecords(string directory)
        {
            if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            {
                return;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(directory, "*.json");
            }
            catch (Exception)
            {
                return;
            }

            long staleBefore = UnixNowMs() - 60000L;
            for (int i = 0; i < files.Length; i++)
            {
                try
                {
                    JObject record = JObject.Parse(File.ReadAllText(files[i]));
                    long heartbeat = record["heartbeatUtcMs"] != null
                        ? (long)record["heartbeatUtcMs"]
                        : 0L;
                    int processId = record["processId"] != null ? (int)record["processId"] : 0;
                    if (heartbeat >= staleBefore || IsProcessAlive(processId))
                    {
                        continue;
                    }

                    File.Delete(files[i]);
                }
                catch (Exception)
                {
                }
            }
        }

        private static bool IsProcessAlive(int processId)
        {
            if (processId <= 0)
            {
                return false;
            }

            try
            {
                Process process = Process.GetProcessById(processId);
                return !process.HasExited;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static long UnixNowMs()
        {
            return (long)(DateTime.UtcNow
                - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
