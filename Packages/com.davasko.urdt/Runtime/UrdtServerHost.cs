using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using KBP.URDT.Diagnostics;
using KBP.URDT.Discovery;
using KBP.URDT.Driver;
using KBP.URDT.Handlers;
using KBP.URDT.Input;
using KBP.URDT.Inspect;
using KBP.URDT.Lifecycle;
using KBP.URDT.Net;
using KBP.URDT.Registry;
using KBP.URDT.Timing;
using KBP.URDT.Transport;
using KBP.URDT.Visualization;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace KBP.URDT
{
    /// <summary>
    /// Unity composition root for URDT transport, identity, command routing and runtime
    /// diagnostics. Every WebSocket connection owns its protocol session and responses
    /// are delivered only to the connection that submitted the command.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-32000)]
    public sealed class UrdtServerHost : MonoBehaviour
    {
        private const int DEFAULT_PORT_SEARCH_COUNT = 20;
        private const int DEFAULT_COMMAND_TIMEOUT_MS = 10000;
        private const int MAX_FALLBACK_INPUT_STEPS = 256;
        private const int MAX_TRANSACTION_INPUT_STEPS = 256;
        private const int INPUT_TRANSACTION_SETTLE_STEPS = 2;
        private const float DEFAULT_REGISTRY_HEARTBEAT_SECONDS = 2f;
        private const long RUNTIME_READY_MAX_AGE_MS = 2000L;

        [SerializeField] private SelectorRuleDefinition[] _selectorRules = null;
        [SerializeField] private bool _visualizationEnabled = true;

        [Header("Multi-instance identity")]
        [SerializeField] private string _projectId = null;
        [SerializeField] private string _runId = "default";
        [SerializeField] private string _peerRole = "standalone";

        [Header("Reliability")]
        [SerializeField] private int _portSearchCount = DEFAULT_PORT_SEARCH_COUNT;
        [SerializeField] private int _commandTimeoutMs = DEFAULT_COMMAND_TIMEOUT_MS;
        [SerializeField] private float _registryHeartbeatSeconds = DEFAULT_REGISTRY_HEARTBEAT_SECONDS;

        private readonly List<SelectorRule> _runtimeSelectorRules = new List<SelectorRule>();
        private readonly ConcurrentQueue<VisualizerCommand> _visualizerCommands =
            new ConcurrentQueue<VisualizerCommand>();
        private readonly ConcurrentQueue<string> _transportLogs = new ConcurrentQueue<string>();
        private readonly ConcurrentDictionary<long, UrdtProtocolSession> _sessions =
            new ConcurrentDictionary<long, UrdtProtocolSession>();

        private DebugServer _server;
        private MainThreadDispatcher _dispatcher;
        private CommandRouter _router;
        private UrdtEventPublisher _events;
        private InputSimulator _input;
        private TimeController _time;
        private TestIdRegistry _registry;
        private SceneLifecycleManager _scenes;
        private DiagnosticsCapture _diagnostics;
        private UnityUrdtDriver _driver;
        private UrdtRuntime _runtime;
        private UrdtRuntimeVisualizer _visualizer;
        private UrdtInstanceIdentity _identity;
        private UrdtInstanceRegistry _instanceRegistry;
        private string _token;
        private string _activeSceneName = string.Empty;
        private string _lastError = string.Empty;
        private long _lastMainThreadTickUtcTicks;
        private float _nextRegistryHeartbeatTime;
        private int _screenWidth;
        private int _screenHeight;
        private int _stateValue = (int)UrdtServerState.Stopped;
        private bool _restoreRunInBackground;
        private bool _previousRunInBackground;
        private bool _running;

        /// <summary>Gets the globally accessible active host instance.</summary>
        public static UrdtServerHost Instance { get; private set; }

        /// <summary>Gets the active runtime facade.</summary>
        public UrdtRuntime Runtime
        {
            get { return _runtime; }
        }

        /// <summary>Gets the active TestId registry.</summary>
        public TestIdRegistry Registry
        {
            get { return _registry; }
        }

        /// <summary>Gets the actual bound port.</summary>
        public int Port
        {
            get { return _server != null ? _server.Port : 0; }
        }

        /// <summary>Gets whether the host completed startup.</summary>
        public bool IsRunning
        {
            get { return _running; }
        }

        /// <summary>Gets the current lifecycle state.</summary>
        public UrdtServerState State
        {
            get { return (UrdtServerState)Volatile.Read(ref _stateValue); }
        }

        /// <summary>Gets the multi-instance identity created for this run.</summary>
        public UrdtInstanceIdentity Identity
        {
            get { return _identity; }
        }

        /// <summary>Gets the last startup or runtime reliability error.</summary>
        public string LastError
        {
            get { return _lastError; }
        }

        /// <summary>Gets whether transport and the Unity main-thread pump are both ready.</summary>
        public bool IsRuntimeReady
        {
            get
            {
                if (!HasCommandRuntime())
                {
                    return false;
                }

                return !IsUpdateHeartbeatStale() || _dispatcher.HasSynchronizationContextPump;
            }
        }

        /// <summary>
        /// Starts URDT on the preferred port, scanning subsequent ports and finally using
        /// an ephemeral port when necessary.
        /// </summary>
        public void StartServer(int port, string token)
        {
            if (_running || State == UrdtServerState.Starting)
            {
                return;
            }

            SetState(UrdtServerState.Starting);
            _lastError = string.Empty;
            _token = token ?? string.Empty;
            EnableRunInBackground();
            EnableInputWithoutGameViewFocus();
            global::URDT.Runtime.Inspectors.UrdtAudioTap.Ensure();
            UrdtKnowledge.Resolve();

            try
            {
                CacheUnityState();
                _identity = UrdtInstanceIdentity.Create(_projectId, _runId, _peerRole);
                InitializeRuntime();
                InitializeTransport(port);

                _running = true;
                Interlocked.Exchange(ref _lastMainThreadTickUtcTicks, DateTime.UtcNow.Ticks);
                SetState(UrdtServerState.Ready);

                _instanceRegistry = new UrdtInstanceRegistry(_identity);
                PublishRegistry();
            }
            catch (Exception exception)
            {
                _lastError = exception.ToString();
                SetState(UrdtServerState.Faulted);
                CleanupRuntime();
                Debug.LogError("URDT startup failed: " + exception);
            }
        }

        /// <summary>Registers the optional game services-reset hook.</summary>
        public void RegisterServicesReset(IUrdtServicesReset servicesReset)
        {
            if (_scenes != null)
            {
                _scenes.SetServicesReset(servicesReset);
            }
        }

        /// <summary>Registers a selector rule applied on the next or current scan.</summary>
        public void RegisterSelectorRule(ISelectorPredicate predicate, string testIdTemplate)
        {
            if (predicate == null || string.IsNullOrEmpty(testIdTemplate))
            {
                return;
            }

            SelectorRule rule = new SelectorRule(predicate, testIdTemplate);
            _runtimeSelectorRules.Add(rule);

            if (_registry != null)
            {
                _registry.AddRule(rule);
                ScanActiveScene();
            }
        }

        /// <summary>Stops transport and releases every runtime-owned service.</summary>
        public void StopServer()
        {
            if (State == UrdtServerState.Stopping || State == UrdtServerState.Stopped)
            {
                return;
            }

            SetState(UrdtServerState.Stopping);
            _running = false;
            CleanupRuntime();
            SetState(UrdtServerState.Stopped);
        }

        private void Update()
        {
            if (!_running)
            {
                PumpTransportLogs();
                return;
            }

            Interlocked.Exchange(ref _lastMainThreadTickUtcTicks, DateTime.UtcNow.Ticks);
            KBP.URDT.Registry.UrdtBeaconDiscovery.Drain(_registry);
            CacheUnityState();
            PumpTransportLogs();
            PumpVisualizerCommandsSafely();
            PumpInputSafely();
            PumpDispatcherSafely();
            PumpResponsesSafely();

            if (Time.realtimeSinceStartup >= _nextRegistryHeartbeatTime)
            {
                PublishRegistry();
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void OnDisable()
        {
            StopServer();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
            StopServer();
        }

        private void InitializeRuntime()
        {
            _input = new InputSimulator();
            _input.BindVirtualDevicesToUiModules();
            StateInspector inspector = new StateInspector();
            _time = new TimeController();
            _registry = new TestIdRegistry();
            ApplyConfiguredSelectorRules();
            ApplyRuntimeSelectorRules();
            _registry.EnableSceneScan();
            ScanActiveScene();

            _scenes = new SceneLifecycleManager();
            _diagnostics = new DiagnosticsCapture();
            _driver = new UnityUrdtDriver(_input, inspector, _time, _registry);
            _runtime = new UrdtRuntime(
                _driver,
                _registry,
                inspector,
                _time,
                _scenes,
                _diagnostics,
                _input);
            ConfigureVisualizer();

            _router = new CommandRouter();
            UrdtCommandRegistrar.RegisterAll(_router, _runtime);

            if (!TryGetComponent(out _dispatcher))
            {
                _dispatcher = gameObject.AddComponent<MainThreadDispatcher>();
            }

            _dispatcher.Configure(_router, 32, true);
            _dispatcher.ResponseFinalizer = FinalizeCommandResponse;
            _dispatcher.ResponseAvailable = OnDispatcherResponseAvailable;
            _events = new UrdtEventPublisher(
                _registry,
                _scenes,
                _diagnostics,
                _runtime.Subscriptions,
                PushEvent);
            _scenes.EnableSceneEvents();
            _diagnostics.EnableLogCapture();
        }

        private void InitializeTransport(int port)
        {
            _server = new DebugServer
            {
                OnLog = EnqueueTransportLog,
                ConnectionMessageHandler = OnClientMessage,
                ClientConnected = OnClientConnected,
                ClientDisconnected = OnClientDisconnected
            };
            _server.StartWithFallback(port, Math.Max(0, _portSearchCount));
            SetState(UrdtServerState.Listening);
        }

        private void CleanupRuntime()
        {
            if (_instanceRegistry != null)
            {
                _instanceRegistry.Dispose();
                _instanceRegistry = null;
            }

            _sessions.Clear();

            if (_events != null)
            {
                _events.Dispose();
                _events = null;
            }

            if (_server != null)
            {
                _server.Dispose();
                _server = null;
            }

            if (_driver != null)
            {
                _driver.Dispose();
                _driver = null;
            }

            if (_diagnostics != null)
            {
                _diagnostics.Dispose();
                _diagnostics = null;
            }

            if (_scenes != null)
            {
                _scenes.Dispose();
                _scenes = null;
            }

            if (_registry != null)
            {
                _registry.Dispose();
                _registry = null;
            }

            if (_time != null)
            {
                _time.Dispose();
                _time = null;
            }

            if (_input != null)
            {
                _input.Dispose();
                _input = null;
            }

            if (_visualizer != null)
            {
                _visualizer.Configure(null, null);
            }

            if (_dispatcher != null)
            {
                _dispatcher.ResponseAvailable = null;
                _dispatcher.ResponseFinalizer = null;
            }

            _router = null;
            _runtime = null;
            _token = null;
            RestoreRunInBackground();
            RestoreInputFocusSettings();
            Interlocked.Exchange(ref _lastMainThreadTickUtcTicks, 0L);

            while (_visualizerCommands.TryDequeue(out _))
            {
            }
        }

        private void EnableRunInBackground()
        {
#if UNITY_EDITOR
            if (Application.runInBackground)
            {
                return;
            }

            _previousRunInBackground = false;
            _restoreRunInBackground = true;
            Application.runInBackground = true;
#endif
        }

#if UNITY_EDITOR
        private bool _restoreInputFocusSettings;
        private InputSettings.EditorInputBehaviorInPlayMode _previousEditorInputBehavior;
        private InputSettings.BackgroundBehavior _previousBackgroundBehavior;
#endif

        /// <summary>
        /// In the Editor the Input System drops pointer/keyboard events while the Game view is not focused
        /// (default PointersAndKeyboardsRespectGameViewFocus). An externally driven session (Play Mode entered
        /// by a script, Editor minimized) then accepts clicks but uGUI never fires onClick. Route all device
        /// input to the game for the lifetime of the URDT session and restore the settings on stop.
        /// </summary>
        private void EnableInputWithoutGameViewFocus()
        {
#if UNITY_EDITOR
            InputSettings settings = InputSystem.settings;
            if (settings == null || _restoreInputFocusSettings)
            {
                return;
            }

            _previousEditorInputBehavior = settings.editorInputBehaviorInPlayMode;
            _previousBackgroundBehavior = settings.backgroundBehavior;
            settings.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            _restoreInputFocusSettings = true;
#endif
        }

        private void RestoreInputFocusSettings()
        {
#if UNITY_EDITOR
            if (!_restoreInputFocusSettings || InputSystem.settings == null)
            {
                return;
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = _previousEditorInputBehavior;
            InputSystem.settings.backgroundBehavior = _previousBackgroundBehavior;
            _restoreInputFocusSettings = false;
#endif
        }

        private void RestoreRunInBackground()
        {
#if UNITY_EDITOR
            if (!_restoreRunInBackground)
            {
                return;
            }

            Application.runInBackground = _previousRunInBackground;
            _restoreRunInBackground = false;
#endif
        }

        private void OnClientConnected(long connectionId)
        {
            UrdtProtocolSession session = new UrdtProtocolSession(
                ProtocolConstants.API_VERSION,
                _token,
                BuildServerInfo,
                request => DispatchAuthenticated(connectionId, request));
            _sessions[connectionId] = session;
        }

        private void OnClientDisconnected(long connectionId)
        {
            _sessions.TryRemove(connectionId, out _);
        }

        private void OnClientMessage(UrdtConnectionMessage message)
        {
            UrdtProtocolSession session;
            if (!_sessions.TryGetValue(message.ConnectionId, out session))
            {
                EnqueueTransportLog("message received without a protocol session: " + message.ConnectionId);
                return;
            }

            try
            {
                HandleOutcome outcome = session.Handle(message.Text);
                if (!string.IsNullOrEmpty(outcome.ResponseText))
                {
                    _server.Send(message.ConnectionId, outcome.ResponseText);
                }

                if (outcome.CloseSocket)
                {
                    _server.Close(message.ConnectionId);
                }
            }
            catch (Exception exception)
            {
                EnqueueTransportLog("protocol[" + message.ConnectionId + "]: " + exception);
                _server.Close(message.ConnectionId);
            }
        }

        private HandleOutcome DispatchAuthenticated(long connectionId, RequestEnvelope request)
        {
            if (!HasCommandRuntime())
            {
                return HandleOutcome.Reply(ProtocolCodec.Serialize(ResponseEnvelope.Fail(
                    request.Id,
                    ErrorCodes.E_RUNTIME_UNAVAILABLE,
                    "Unity main-thread runtime is not ready.")));
            }

            if (_visualizer != null)
            {
                _visualizerCommands.Enqueue(new VisualizerCommand(request.Action, request.Payload));
            }

            _dispatcher.Submit(
                connectionId,
                new Command(request.Id, request.Action, request.Payload),
                _commandTimeoutMs);
            return HandleOutcome.None();
        }

        private bool HasCommandRuntime()
        {
            return _running
                && State == UrdtServerState.Ready
                && _runtime != null
                && _router != null
                && _dispatcher != null
                && !_dispatcher.IsShuttingDown;
        }

        private bool IsUpdateHeartbeatStale()
        {
            long tickUtc = Interlocked.Read(ref _lastMainThreadTickUtcTicks);
            return tickUtc <= 0L || UnixNowMs() - TicksToUnixMs(tickUtc) > RUNTIME_READY_MAX_AGE_MS;
        }

        private void PumpDispatcherSafely()
        {
            try
            {
                _dispatcher.PumpInbound();
            }
            catch (Exception exception)
            {
                SetDegraded("dispatcher pump", exception);
            }
        }

        private void PumpResponsesSafely()
        {
            try
            {
                long connectionId;
                Response response;
                while (_dispatcher.TryDequeueResponse(out connectionId, out response))
                {
                    ResponseEnvelope envelope = response.Ok
                        ? ResponseEnvelope.Ok(response.Id, response.Data as JToken)
                        : ResponseEnvelope.Fail(
                            response.Id,
                            response.ErrorCode,
                            response.ErrorMessage,
                            response.ErrorDetails as JToken);

                    if (!_server.Send(connectionId, ProtocolCodec.Serialize(envelope)))
                    {
                        EnqueueTransportLog(
                            "response dropped for stale connection " + connectionId + ", request=" + response.Id);
                    }
                }
            }
            catch (Exception exception)
            {
                SetDegraded("response pump", exception);
            }
        }

        private void OnDispatcherResponseAvailable()
        {
            PumpResponsesSafely();
            PumpQueuedInputFallbackSafely();
        }

        private Response FinalizeCommandResponse(Command command, Response response)
        {
            if (response == null || !response.Ok || _input == null)
            {
                return response;
            }

            int queuedFrames;
            if (!TryGetQueuedFrameCount(response, out queuedFrames) || queuedFrames <= 0)
            {
                return response;
            }

            if (IsRealtimePaced(command))
            {
                // Realtime gestures are pumped one pointer state per Update, so the game observes them over
                // real frames (velocities, dwell and hold timers behave as for a human). Compressing a whole
                // drag into one synchronous transaction would make every gesture instantaneous in game time.
                JObject paced = response.Data as JObject;
                if (paced != null)
                {
                    paced["pumped_frames"] = 0;
                    paced["input_transaction"] = "frame_paced";
                    paced["frame_ms"] = Mathf.Round(Time.smoothDeltaTime * 100000f) / 100f;
                }

                return response;
            }

            try
            {
                int pumpedFrames = PumpInputTransaction(queuedFrames);
                JObject data = response.Data as JObject;
                if (data != null)
                {
                    data["pumped_frames"] = pumpedFrames;
                    data["input_transaction"] = pumpedFrames >= queuedFrames
                        ? "completed_before_response"
                        : "partial_before_response";
                }
            }
            catch (Exception exception)
            {
                SetDegraded("input transaction", exception);
                return Response.Error(
                    response.Id,
                    ErrorCodes.E_INTERNAL,
                    "Input transaction failed before command response.",
                    JObject.FromObject(new { action = command != null ? command.Action : string.Empty, error = exception.Message }));
            }

            return response;
        }

        private void PumpInputSafely()
        {
            if (_input == null)
            {
                return;
            }

            try
            {
                if (_input.PumpQueuedPointerStep())
                {
                    InputSystem.Update();
                    ProcessEventSystemOnce();
                }
            }
            catch (Exception exception)
            {
                SetDegraded("input pump", exception);
            }
        }

        private void PumpQueuedInputFallbackSafely()
        {
            if (_input == null || !IsUpdateHeartbeatStale())
            {
                return;
            }

            try
            {
                int steps = 0;
                while (_input.PendingPointerFrameCount > 0 && steps < MAX_FALLBACK_INPUT_STEPS)
                {
                    if (!_input.PumpQueuedPointerStep())
                    {
                        break;
                    }

                    InputSystem.Update();
                    ProcessEventSystemOnce();
                    steps++;
                }
            }
            catch (Exception exception)
            {
                SetDegraded("input fallback pump", exception);
            }
        }

        private int PumpInputTransaction(int requestedFrames)
        {
            int limit = Mathf.Min(Mathf.Max(0, requestedFrames), MAX_TRANSACTION_INPUT_STEPS);
            int pumpedFrames = 0;
            for (int i = 0; i < limit; i++)
            {
                if (!_input.PumpQueuedPointerStep())
                {
                    break;
                }

                InputSystem.Update();
                ProcessEventSystemOnce();
                Canvas.ForceUpdateCanvases();
                pumpedFrames++;
            }

            for (int i = 0; i < INPUT_TRANSACTION_SETTLE_STEPS; i++)
            {
                InputSystem.Update();
                ProcessEventSystemOnce();
                Canvas.ForceUpdateCanvases();
            }

            return pumpedFrames;
        }

        private static bool IsRealtimePaced(Command command)
        {
            JObject payload = command != null ? command.Payload as JObject : null;
            JToken pacing = payload != null ? payload["pacing"] : null;
            return pacing != null
                && pacing.Type == JTokenType.String
                && string.Equals((string)pacing, ProtocolConstants.PACING_REALTIME, StringComparison.Ordinal);
        }

        private static bool TryGetQueuedFrameCount(Response response, out int queuedFrames)
        {
            queuedFrames = 0;
            JObject data = response.Data as JObject;
            if (data == null)
            {
                return false;
            }

            JToken token = data["queued_frames"];
            if (token == null || token.Type == JTokenType.Null)
            {
                return false;
            }

            queuedFrames = token.Value<int>();
            return true;
        }

        private static void ProcessEventSystemOnce()
        {
            EventSystem eventSystem = EventSystem.current;
            if (eventSystem == null)
            {
                return;
            }

            eventSystem.UpdateModules();
            BaseInputModule module = eventSystem.currentInputModule;
            if (module != null)
            {
                module.Process();
            }
        }

        private void PumpVisualizerCommandsSafely()
        {
            if (_visualizer == null)
            {
                while (_visualizerCommands.TryDequeue(out _))
                {
                }

                return;
            }

            VisualizerCommand command;
            while (_visualizerCommands.TryDequeue(out command))
            {
                try
                {
                    _visualizer.RecordCommand(command.Action, command.Payload);
                }
                catch (Exception exception)
                {
                    EnqueueTransportLog("visualizer command ignored: " + exception.Message);
                }
            }
        }

        private void PushEvent(ResponseEnvelope envelope)
        {
            DebugServer server = _server;
            if (server != null && server.HasClient)
            {
                server.Send(server.ActiveConnectionId, ProtocolCodec.Serialize(envelope));
            }
        }

        private ServerInfo BuildServerInfo()
        {
            UrdtInstanceIdentity identity = _identity;
            bool runtimeReady = IsRuntimeReady;
            UrdtServerState state = runtimeReady ? State : UrdtServerState.Degraded;
            return new ServerInfo(
                ProtocolConstants.SERVER_VERSION,
                ProtocolConstants.ORIGIN_BOTTOM_LEFT,
                _screenWidth,
                _screenHeight,
                identity != null ? identity.ProjectId : string.Empty,
                identity != null ? identity.ProjectPathHash : string.Empty,
                identity != null ? identity.InstanceId : string.Empty,
                identity != null ? identity.RunId : string.Empty,
                identity != null ? identity.PeerRole : string.Empty,
                identity != null ? identity.ProcessId : 0,
                Port,
                runtimeReady,
                state.ToString(),
                TicksToUnixMs(Interlocked.Read(ref _lastMainThreadTickUtcTicks)),
                _dispatcher != null ? _dispatcher.PendingCommandCount : 0,
                _dispatcher != null ? _dispatcher.PendingResponseCount : 0);
        }

        private void PublishRegistry()
        {
            if (_instanceRegistry == null || _identity == null)
            {
                return;
            }

            try
            {
                ServerInfo info = BuildServerInfo();
                _instanceRegistry.Publish(
                    info.Port,
                    info.ServerState,
                    info.RuntimeReady,
                    _activeSceneName,
                    info.MainThreadTickUtcMs,
                    _server != null ? _server.ActiveConnectionId : 0L);

                if (!string.IsNullOrEmpty(_instanceRegistry.LastError)
                    && !string.Equals(_lastError, _instanceRegistry.LastError, StringComparison.Ordinal))
                {
                    _lastError = _instanceRegistry.LastError;
                    EnqueueTransportLog("instance registry: " + _lastError);
                }
            }
            catch (Exception exception)
            {
                EnqueueTransportLog("instance registry publish: " + exception.Message);
            }

            _nextRegistryHeartbeatTime =
                Time.realtimeSinceStartup + Mathf.Max(0.5f, _registryHeartbeatSeconds);
        }

        private void CacheUnityState()
        {
            _screenWidth = Screen.width;
            _screenHeight = Screen.height;
            Scene scene = SceneManager.GetActiveScene();
            _activeSceneName = scene.IsValid() ? scene.name : string.Empty;
        }

        private void PumpTransportLogs()
        {
            string message;
            while (_transportLogs.TryDequeue(out message))
            {
                Debug.LogWarning("URDT: " + message);
            }
        }

        private void EnqueueTransportLog(string message)
        {
            if (!string.IsNullOrEmpty(message))
            {
                _transportLogs.Enqueue(message);
            }
        }

        private void SetDegraded(string subsystem, Exception exception)
        {
            _lastError = subsystem + ": " + exception;
            SetState(UrdtServerState.Degraded);
            EnqueueTransportLog(_lastError);
        }

        private void SetState(UrdtServerState state)
        {
            Volatile.Write(ref _stateValue, (int)state);
        }

        private void ApplyConfiguredSelectorRules()
        {
            if (_selectorRules == null)
            {
                return;
            }

            for (int i = 0; i < _selectorRules.Length; i++)
            {
                SelectorRuleDefinition definition = _selectorRules[i];
                SelectorRule rule;
                if (definition != null && definition.TryBuild(out rule))
                {
                    _registry.AddRule(rule);
                }
            }
        }

        private void ApplyRuntimeSelectorRules()
        {
            for (int i = 0; i < _runtimeSelectorRules.Count; i++)
            {
                _registry.AddRule(_runtimeSelectorRules[i]);
            }
        }

        /// <summary>Performs a full scan of the active scene using registered selector rules.</summary>
        public void ScanActiveScene()
        {
            if (_registry == null)
            {
                return;
            }

            Scene scene = SceneManager.GetActiveScene();
            if (scene.IsValid() && scene.isLoaded)
            {
                _registry.ScanScene(scene);
            }
        }

        /// <summary>Registers an entire transform subtree incrementally.</summary>
        public void RegisterHierarchy(Transform root)
        {
            if (_registry == null || root == null)
            {
                return;
            }

            _registry.RegisterHierarchy(root, RegistrationSource.Incremental);
        }

        private void ConfigureVisualizer()
        {
            if (!_visualizationEnabled)
            {
                return;
            }

            if (!TryGetComponent(out _visualizer))
            {
                _visualizer = gameObject.AddComponent<UrdtRuntimeVisualizer>();
            }

            _visualizer.Configure(_input, _registry);
        }

        private static long UnixNowMs()
        {
            return TicksToUnixMs(DateTime.UtcNow.Ticks);
        }

        private static long TicksToUnixMs(long ticks)
        {
            if (ticks <= 0L)
            {
                return 0L;
            }

            return (ticks - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).Ticks)
                / TimeSpan.TicksPerMillisecond;
        }

        private readonly struct VisualizerCommand
        {
            public VisualizerCommand(string action, JToken payload)
            {
                Action = action;
                Payload = payload;
            }

            public string Action { get; }

            public JToken Payload { get; }
        }
    }
}
