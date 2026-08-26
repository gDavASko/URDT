using System;
using System.Collections.Concurrent;
using System.Threading;
using UnityEngine;

namespace KBP.URDT.Transport
{
    /// <summary>
    /// Bridges background transport work to Unity's main thread. Commands and responses
    /// retain their connection generation, preventing delivery to a replacement client.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class MainThreadDispatcher : MonoBehaviour, ICommandSink
    {
        private const int DEFAULT_BUDGET = 16;
        private const int DEFAULT_TIMEOUT_MS = 10000;

        private static MainThreadDispatcher _instance;

        private readonly ConcurrentQueue<QueuedCommand> _inbound =
            new ConcurrentQueue<QueuedCommand>();
        private readonly ConcurrentQueue<QueuedResponse> _outbound =
            new ConcurrentQueue<QueuedResponse>();

        private CommandRouter _router;
        private int _maxCommandsPerFrame = DEFAULT_BUDGET;
        private bool _externallyPumped;
        private volatile bool _shuttingDown;
        private SynchronizationContext _mainThreadContext;
        private int _mainThreadId;
        private int _pumpPosted;

        /// <summary>Raised after the dispatcher produces one or more outbound responses.</summary>
        public Action ResponseAvailable { get; set; }

        /// <summary>Allows the host to finalize main-thread effects before a response is published.</summary>
        public Func<Command, Response, Response> ResponseFinalizer { get; set; }

        /// <summary>Gets or creates the optional persistent dispatcher.</summary>
        public static MainThreadDispatcher Instance
        {
            get { return EnsureInstance(); }
        }

        /// <summary>Gets the maximum commands processed per pump.</summary>
        public int MaxCommandsPerFrame
        {
            get { return _maxCommandsPerFrame; }
        }

        /// <summary>Gets the current inbound queue depth.</summary>
        public int PendingCommandCount
        {
            get { return _inbound.Count; }
        }

        /// <summary>Gets the current outbound queue depth.</summary>
        public int PendingResponseCount
        {
            get { return _outbound.Count; }
        }

        /// <summary>Gets whether new work is being rejected.</summary>
        public bool IsShuttingDown
        {
            get { return _shuttingDown; }
        }

        /// <summary>Gets whether background submissions can wake the Unity main thread.</summary>
        public bool HasSynchronizationContextPump
        {
            get { return _mainThreadContext != null && _mainThreadId > 0; }
        }

        /// <summary>Creates a DontDestroyOnLoad dispatcher for standalone integrations.</summary>
        public static MainThreadDispatcher EnsureInstance()
        {
            if (_instance == null)
            {
                GameObject host = new GameObject("URDT_MainThreadDispatcher");
                _instance = host.AddComponent<MainThreadDispatcher>();
                DontDestroyOnLoad(host);
            }

            return _instance;
        }

        /// <summary>Configures an automatically pumped dispatcher.</summary>
        public void Configure(CommandRouter router, int maxCommandsPerFrame)
        {
            Configure(router, maxCommandsPerFrame, false);
        }

        /// <summary>
        /// Configures the router and resets lifecycle state for a fresh server run.
        /// </summary>
        public void Configure(CommandRouter router, int maxCommandsPerFrame, bool externallyPumped)
        {
            _router = router;
            _externallyPumped = externallyPumped;
            _shuttingDown = false;
            _mainThreadContext = SynchronizationContext.Current;
            _mainThreadId = Thread.CurrentThread.ManagedThreadId;
            Interlocked.Exchange(ref _pumpPosted, 0);
            ClearQueues();

            if (maxCommandsPerFrame > 0)
            {
                _maxCommandsPerFrame = maxCommandsPerFrame;
            }
        }

        /// <summary>Enqueues an unscoped command for backwards-compatible callers.</summary>
        public void Submit(Command command)
        {
            Submit(0L, command, DEFAULT_TIMEOUT_MS);
        }

        /// <summary>Enqueues a connection-scoped command with a bounded deadline.</summary>
        public void Submit(long connectionId, Command command, int timeoutMs)
        {
            if (command == null)
            {
                return;
            }

            if (_shuttingDown)
            {
                _outbound.Enqueue(new QueuedResponse(
                    connectionId,
                    Response.Error(command.Id, ErrorCodes.E_SHUTTING_DOWN, "URDT is shutting down.")));
                return;
            }

            int effectiveTimeoutMs = timeoutMs > 0 ? timeoutMs : DEFAULT_TIMEOUT_MS;
            long deadlineUtcTicks = DateTime.UtcNow.AddMilliseconds(effectiveTimeoutMs).Ticks;
            _inbound.Enqueue(new QueuedCommand(connectionId, command, deadlineUtcTicks));

            if (_externallyPumped)
            {
                ScheduleExternalPump();
            }
        }

        /// <summary>Dequeues a response while ignoring its connection generation.</summary>
        public bool TryDequeueResponse(out Response response)
        {
            long connectionId;
            return TryDequeueResponse(out connectionId, out response);
        }

        /// <summary>Dequeues a response together with its connection generation.</summary>
        public bool TryDequeueResponse(out long connectionId, out Response response)
        {
            QueuedResponse queued;
            if (_outbound.TryDequeue(out queued))
            {
                connectionId = queued.ConnectionId;
                response = queued.Response;
                return true;
            }

            connectionId = 0L;
            response = null;
            return false;
        }

        /// <summary>Processes commands on the calling Unity main thread.</summary>
        public int PumpInbound()
        {
            int processed = 0;
            QueuedCommand queued;
            while (processed < _maxCommandsPerFrame && _inbound.TryDequeue(out queued))
            {
                Response response;
                if (DateTime.UtcNow.Ticks > queued.DeadlineUtcTicks)
                {
                    response = Response.Error(
                        queued.Command.Id,
                        ErrorCodes.E_TIMEOUT,
                        "Command expired before main-thread execution.");
                }
                else
                {
                    response = _router != null
                        ? _router.Route(queued.Command)
                        : Response.Error(queued.Command.Id, ErrorCodes.E_INTERNAL, "No router configured.");
                }

                Func<Command, Response, Response> finalizer = ResponseFinalizer;
                if (finalizer != null)
                {
                    response = finalizer(queued.Command, response);
                }

                _outbound.Enqueue(new QueuedResponse(queued.ConnectionId, response));
                processed++;
            }

            if (processed > 0)
            {
                Action available = ResponseAvailable;
                if (available != null)
                {
                    available();
                }
            }

            return processed;
        }

        internal static void ClearInstanceForTests()
        {
            _instance = null;
        }

        private void Update()
        {
            if (!_externallyPumped)
            {
                PumpInbound();
            }
        }

        private void ScheduleExternalPump()
        {
            SynchronizationContext context = _mainThreadContext;
            if (context == null || _shuttingDown)
            {
                return;
            }

            if (Thread.CurrentThread.ManagedThreadId == _mainThreadId)
            {
                PumpInbound();
                return;
            }

            if (Interlocked.Exchange(ref _pumpPosted, 1) == 0)
            {
                context.Post(_ => PumpPostedCommands(), null);
            }
        }

        private void PumpPostedCommands()
        {
            Interlocked.Exchange(ref _pumpPosted, 0);
            if (_shuttingDown)
            {
                return;
            }

            PumpInbound();
            if (PendingCommandCount > 0)
            {
                ScheduleExternalPump();
            }
        }

        private void OnDisable()
        {
            DrainForShutdown();
        }

        private void OnApplicationQuit()
        {
            DrainForShutdown();
        }

        private void DrainForShutdown()
        {
            _shuttingDown = true;
            ResponseFinalizer = null;
            Interlocked.Exchange(ref _pumpPosted, 0);

            QueuedCommand queued;
            while (_inbound.TryDequeue(out queued))
            {
                _outbound.Enqueue(new QueuedResponse(
                    queued.ConnectionId,
                    Response.Error(
                        queued.Command.Id,
                        ErrorCodes.E_SHUTTING_DOWN,
                        "URDT is shutting down.")));
            }
        }

        private void ClearQueues()
        {
            while (_inbound.TryDequeue(out _))
            {
            }

            while (_outbound.TryDequeue(out _))
            {
            }
        }

        private readonly struct QueuedCommand
        {
            public QueuedCommand(long connectionId, Command command, long deadlineUtcTicks)
            {
                ConnectionId = connectionId;
                Command = command;
                DeadlineUtcTicks = deadlineUtcTicks;
            }

            public long ConnectionId { get; }

            public Command Command { get; }

            public long DeadlineUtcTicks { get; }
        }

        private readonly struct QueuedResponse
        {
            public QueuedResponse(long connectionId, Response response)
            {
                ConnectionId = connectionId;
                Response = response;
            }

            public long ConnectionId { get; }

            public Response Response { get; }
        }
    }
}
