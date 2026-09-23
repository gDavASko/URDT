using System;
using KBP.URDT.Transport;
using Newtonsoft.Json.Linq;

namespace KBP.URDT.Net
{
    /// <summary>
    /// Per-connection protocol state machine. Authentication and subscriptions never
    /// leak into a replacement WebSocket connection.
    /// </summary>
    public sealed class UrdtProtocolSession
    {
        private readonly int _expectedApi;
        private readonly string _expectedToken;
        private readonly Func<ServerInfo> _serverInfoProvider;
        private readonly Func<RequestEnvelope, HandleOutcome> _authenticatedDispatch;

        private bool _authenticated;

        /// <summary>Creates a session backed by immutable server information.</summary>
        public UrdtProtocolSession(
            int expectedApi,
            string expectedToken,
            ServerInfo serverInfo,
            Func<RequestEnvelope, HandleOutcome> authenticatedDispatch = null)
            : this(expectedApi, expectedToken, () => serverInfo, authenticatedDispatch)
        {
        }

        /// <summary>Creates a session backed by a live health provider.</summary>
        public UrdtProtocolSession(
            int expectedApi,
            string expectedToken,
            Func<ServerInfo> serverInfoProvider,
            Func<RequestEnvelope, HandleOutcome> authenticatedDispatch = null)
        {
            _expectedApi = expectedApi;
            _expectedToken = expectedToken;
            _serverInfoProvider = serverInfoProvider
                ?? throw new ArgumentNullException(nameof(serverInfoProvider));
            _authenticatedDispatch = authenticatedDispatch;
        }

        /// <summary>Gets whether this connection completed authentication.</summary>
        public bool IsAuthenticated
        {
            get { return _authenticated; }
        }

        /// <summary>Processes one protocol request.</summary>
        public HandleOutcome Handle(string inboundText)
        {
            RequestEnvelope request;
            string parseError;
            if (!ProtocolCodec.TryParseRequest(inboundText, out request, out parseError))
            {
                return Reply(ResponseEnvelope.Fail(
                    null,
                    ErrorCodes.E_BAD_REQUEST,
                    "Malformed request: " + parseError));
            }

            if (string.Equals(request.Action, ProtocolConstants.ACTION_HANDSHAKE, StringComparison.Ordinal))
            {
                return HandleHandshake(request);
            }

            if (!_authenticated)
            {
                return Reply(ResponseEnvelope.Fail(
                    request.Id,
                    ErrorCodes.E_UNAUTHORIZED,
                    "Handshake required before '" + request.Action + "'."));
            }

            if (string.Equals(request.Action, ProtocolConstants.ACTION_PING, StringComparison.Ordinal))
            {
                return Reply(ResponseEnvelope.Ok(request.Id, new JObject
                {
                    ["pong"] = true,
                    ["server_time_ms"] = UnixNowMs()
                }));
            }

            if (string.Equals(request.Action, ProtocolConstants.ACTION_HEALTH, StringComparison.Ordinal))
            {
                return Reply(ResponseEnvelope.Ok(request.Id, BuildServerData(_serverInfoProvider())));
            }

            if (_authenticatedDispatch != null)
            {
                return _authenticatedDispatch(request);
            }

            return Reply(ResponseEnvelope.Fail(
                request.Id,
                ErrorCodes.E_UNKNOWN_ACTION,
                "Unknown action: " + request.Action));
        }

        private HandleOutcome HandleHandshake(RequestEnvelope request)
        {
            if (request.Api != _expectedApi)
            {
                JObject details = new JObject { ["expected"] = _expectedApi, ["got"] = request.Api };
                return Reply(ResponseEnvelope.Fail(
                    request.Id,
                    ErrorCodes.E_API_VERSION,
                    "Incompatible API version.",
                    details));
            }

            string token = ReadPayloadString(request.Payload, "token");
            if (!string.Equals(token, _expectedToken, StringComparison.Ordinal))
            {
                return HandleOutcome.ReplyAndClose(ProtocolCodec.Serialize(
                    ResponseEnvelope.Fail(
                        request.Id,
                        ErrorCodes.E_UNAUTHORIZED,
                        "Invalid or missing token.")));
            }

            ServerInfo info = _serverInfoProvider();
            string mismatch = GetIdentityMismatch(request.Payload, info);
            if (!string.IsNullOrEmpty(mismatch))
            {
                JObject details = new JObject
                {
                    ["reason"] = mismatch,
                    ["projectId"] = info.ProjectId,
                    ["instanceId"] = info.InstanceId,
                    ["runId"] = info.RunId,
                    ["peerRole"] = info.PeerRole
                };
                return HandleOutcome.ReplyAndClose(ProtocolCodec.Serialize(
                    ResponseEnvelope.Fail(
                        request.Id,
                        ErrorCodes.E_WRONG_INSTANCE,
                        "Connected URDT instance does not match the requested identity.",
                        details)));
            }

            _authenticated = true;
            JObject data = BuildServerData(info);
            data["status"] = info.RuntimeReady ? ProtocolConstants.STATUS_READY : "degraded";
            data["api"] = _expectedApi;
            data["resumed"] = false;
            data["time_mode"] = ReadPayloadString(request.Payload, "time_mode") ?? "deterministic";
            return Reply(ResponseEnvelope.Ready(request.Id, data));
        }

        private static JObject BuildServerData(ServerInfo info)
        {
            return new JObject
            {
                ["server_version"] = info.ServerVersion,
                ["origin"] = info.Origin,
                ["screen"] = new JObject
                {
                    ["width"] = info.ScreenWidth,
                    ["height"] = info.ScreenHeight
                },
                ["projectId"] = info.ProjectId,
                ["projectPathHash"] = info.ProjectPathHash,
                ["instanceId"] = info.InstanceId,
                ["runId"] = info.RunId,
                ["peerRole"] = info.PeerRole,
                ["processId"] = info.ProcessId,
                ["port"] = info.Port,
                ["runtime_ready"] = info.RuntimeReady,
                ["server_state"] = info.ServerState,
                ["main_thread_tick_utc_ms"] = info.MainThreadTickUtcMs,
                ["pending_commands"] = info.PendingCommands,
                ["pending_responses"] = info.PendingResponses,
                ["knowledge_dir"] = UrdtKnowledge.RootPath,
                ["product_name"] = UrdtKnowledge.ProductName,
                ["app_version"] = UrdtKnowledge.AppVersion,
                ["build_id"] = UrdtKnowledge.BuildId,
                ["server_time_ms"] = UnixNowMs()
            };
        }

        private static string GetIdentityMismatch(JObject payload, ServerInfo info)
        {
            string mismatch = CompareExpected(payload, "expectedProjectId", info.ProjectId);
            if (!string.IsNullOrEmpty(mismatch))
            {
                return mismatch;
            }

            mismatch = CompareExpected(payload, "expectedInstanceId", info.InstanceId);
            if (!string.IsNullOrEmpty(mismatch))
            {
                return mismatch;
            }

            mismatch = CompareExpected(payload, "expectedRunId", info.RunId);
            if (!string.IsNullOrEmpty(mismatch))
            {
                return mismatch;
            }

            return CompareExpected(payload, "expectedPeerRole", info.PeerRole);
        }

        private static string CompareExpected(JObject payload, string key, string actual)
        {
            string expected = ReadPayloadString(payload, key);
            if (string.IsNullOrEmpty(expected)
                || string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return key + " expected '" + expected + "' but connected to '" + actual + "'.";
        }

        private static string ReadPayloadString(JObject payload, string key)
        {
            JToken value = payload != null ? payload[key] : null;
            return value != null && value.Type != JTokenType.Null ? value.ToString() : null;
        }

        private static HandleOutcome Reply(ResponseEnvelope response)
        {
            return HandleOutcome.Reply(ProtocolCodec.Serialize(response));
        }

        private static long UnixNowMs()
        {
            return (long)(DateTime.UtcNow
                - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalMilliseconds;
        }
    }
}
