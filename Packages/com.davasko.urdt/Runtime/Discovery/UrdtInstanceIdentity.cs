using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace KBP.URDT.Discovery
{
    /// <summary>
    /// Stable project identity plus per-process identity used to address one Unity Editor
    /// or player among several simultaneous URDT instances.
    /// </summary>
    public sealed class UrdtInstanceIdentity
    {
        private const string DEFAULT_RUN_ID = "default";
        private const string DEFAULT_PEER_ROLE = "standalone";

        private UrdtInstanceIdentity(
            string projectId,
            string instanceId,
            string runId,
            string peerRole,
            string projectPath,
            string projectPathHash,
            int processId)
        {
            ProjectId = projectId;
            InstanceId = instanceId;
            RunId = runId;
            PeerRole = peerRole;
            ProjectPath = projectPath;
            ProjectPathHash = projectPathHash;
            ProcessId = processId;
        }

        /// <summary>Gets the logical project identifier.</summary>
        public string ProjectId { get; }

        /// <summary>Gets the unique identity of this Editor or player process.</summary>
        public string InstanceId { get; }

        /// <summary>Gets the multi-peer test run identifier.</summary>
        public string RunId { get; }

        /// <summary>Gets the network peer role, such as host or client-1.</summary>
        public string PeerRole { get; }

        /// <summary>Gets the normalized Unity project root.</summary>
        public string ProjectPath { get; }

        /// <summary>Gets the non-reversible project path fingerprint.</summary>
        public string ProjectPathHash { get; }

        /// <summary>Gets the operating-system process identifier.</summary>
        public int ProcessId { get; }

        /// <summary>
        /// Creates identity using explicit values first and URDT environment variables as
        /// automation-friendly overrides.
        /// </summary>
        public static UrdtInstanceIdentity Create(
            string configuredProjectId,
            string configuredRunId,
            string configuredPeerRole)
        {
            string projectPath = GetProjectPath();
            int processId = Process.GetCurrentProcess().Id;
            string projectId = FirstNonEmpty(
                Environment.GetEnvironmentVariable("URDT_PROJECT_ID"),
                configuredProjectId,
                Path.GetFileName(projectPath),
                "unity-project");
            projectId = Sanitize(projectId);

            string runId = Sanitize(FirstNonEmpty(
                Environment.GetEnvironmentVariable("URDT_RUN_ID"),
                configuredRunId,
                DEFAULT_RUN_ID));
            string peerRole = Sanitize(FirstNonEmpty(
                Environment.GetEnvironmentVariable("URDT_PEER_ROLE"),
                configuredPeerRole,
                DEFAULT_PEER_ROLE));
            string instanceId = FirstNonEmpty(
                Environment.GetEnvironmentVariable("URDT_INSTANCE_ID"),
                null);

            if (string.IsNullOrEmpty(instanceId))
            {
                instanceId = projectId + "-" + processId + "-" + Guid.NewGuid().ToString("N").Substring(0, 8);
            }

            return new UrdtInstanceIdentity(
                projectId,
                Sanitize(instanceId),
                runId,
                peerRole,
                projectPath,
                ComputeHash(projectPath),
                processId);
        }

        private static string GetProjectPath()
        {
            string assetsPath = Application.dataPath;
            DirectoryInfo directory = Directory.GetParent(assetsPath);
            string projectPath = directory != null ? directory.FullName : assetsPath;
            return Path.GetFullPath(projectPath).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string FirstNonEmpty(params string[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                if (!string.IsNullOrWhiteSpace(values[i]))
                {
                    return values[i].Trim();
                }
            }

            return string.Empty;
        }

        private static string Sanitize(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return "unknown";
            }

            StringBuilder builder = new StringBuilder(value.Length);
            for (int i = 0; i < value.Length; i++)
            {
                char character = value[i];
                if (char.IsLetterOrDigit(character) || character == '-' || character == '_' || character == '.')
                {
                    builder.Append(char.ToLowerInvariant(character));
                }
                else
                {
                    builder.Append('-');
                }
            }

            return builder.ToString().Trim('-');
        }

        private static string ComputeHash(string value)
        {
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] bytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(value.ToLowerInvariant()));
                StringBuilder builder = new StringBuilder(16);
                for (int i = 0; i < 8; i++)
                {
                    builder.Append(bytes[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }
    }
}
