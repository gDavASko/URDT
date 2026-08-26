namespace KBP.URDT.Net
{
    /// <summary>
    /// Host-supplied endpoint, identity and readiness facts returned by handshake and
    /// health. The protocol layer remains independent from UnityEngine.
    /// </summary>
    public readonly struct ServerInfo
    {
        /// <summary>Creates the backwards-compatible minimal server information.</summary>
        public ServerInfo(string serverVersion, string origin, int screenWidth, int screenHeight)
            : this(
                serverVersion,
                origin,
                screenWidth,
                screenHeight,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                string.Empty,
                0,
                0,
                true,
                "Ready",
                0L,
                0,
                0)
        {
        }

        /// <summary>Creates complete multi-instance server information.</summary>
        public ServerInfo(
            string serverVersion,
            string origin,
            int screenWidth,
            int screenHeight,
            string projectId,
            string projectPathHash,
            string instanceId,
            string runId,
            string peerRole,
            int processId,
            int port,
            bool runtimeReady,
            string serverState,
            long mainThreadTickUtcMs,
            int pendingCommands,
            int pendingResponses)
        {
            ServerVersion = serverVersion;
            Origin = origin;
            ScreenWidth = screenWidth;
            ScreenHeight = screenHeight;
            ProjectId = projectId;
            ProjectPathHash = projectPathHash;
            InstanceId = instanceId;
            RunId = runId;
            PeerRole = peerRole;
            ProcessId = processId;
            Port = port;
            RuntimeReady = runtimeReady;
            ServerState = serverState;
            MainThreadTickUtcMs = mainThreadTickUtcMs;
            PendingCommands = pendingCommands;
            PendingResponses = pendingResponses;
        }

        public string ServerVersion { get; }

        public string Origin { get; }

        public int ScreenWidth { get; }

        public int ScreenHeight { get; }

        public string ProjectId { get; }

        public string ProjectPathHash { get; }

        public string InstanceId { get; }

        public string RunId { get; }

        public string PeerRole { get; }

        public int ProcessId { get; }

        public int Port { get; }

        public bool RuntimeReady { get; }

        public string ServerState { get; }

        public long MainThreadTickUtcMs { get; }

        public int PendingCommands { get; }

        public int PendingResponses { get; }

        /// <summary>Creates minimal information for engine-agnostic tests.</summary>
        public static ServerInfo Default(int screenWidth, int screenHeight)
        {
            return new ServerInfo(
                ProtocolConstants.SERVER_VERSION,
                ProtocolConstants.ORIGIN_BOTTOM_LEFT,
                screenWidth,
                screenHeight);
        }
    }
}
