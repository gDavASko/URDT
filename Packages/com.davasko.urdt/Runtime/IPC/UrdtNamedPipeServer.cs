using System;
using System.IO;
using System.IO.Pipes;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;

namespace URDT.Runtime.IPC
{
    /// <summary>
    /// High-speed Windows Named Pipe Server for L1 kinematics and fast touch ticker frames.
    /// Reads fixed 27-byte binary frames with sub-millisecond IPC latency (< 0.05 ms).
    /// </summary>
    [DefaultExecutionOrder(-9990)]
    public sealed class UrdtNamedPipeServer : MonoBehaviour
    {
        public const string DEFAULT_PIPE_NAME = "urdt_fast_ticker";

        [SerializeField]
        private string _pipeName = DEFAULT_PIPE_NAME;

        [SerializeField]
        private bool _autoStartOnAwake = true;

        public static UrdtNamedPipeServer Instance { get; private set; }

        private CancellationTokenSource _cts;
        private Task _serverWorkerTask;
        private NamedPipeServerStream _activeStream;
        private readonly byte[] _readBuffer = new byte[UrdtRawCommand.STRUCT_SIZE];
        private uint _lastSequenceNumber;
        private long _framesReceived;
        private long _crcErrors;

        public bool IsRunning => _cts != null && !_cts.IsCancellationRequested;
        public long FramesReceived => _framesReceived;
        public long CrcErrors => _crcErrors;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_NamedPipeServer]");
                Instance = go.AddComponent<UrdtNamedPipeServer>();
                DontDestroyOnLoad(go);
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            string envPipe = Environment.GetEnvironmentVariable("URDT_PIPE_NAME");
            if (!string.IsNullOrEmpty(envPipe))
            {
                _pipeName = envPipe;
            }

            if (_autoStartOnAwake)
            {
                StartServer();
            }
        }

        private void OnDestroy()
        {
            StopServer();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void StartServer()
        {
            if (IsRunning) return;

            _cts = new CancellationTokenSource();
            _serverWorkerTask = Task.Run(() => ServerLoopAsync(_pipeName, _cts.Token));
            Debug.Log($"[URDT] Named Pipe Server started on pipe: \\\\.\\pipe\\{_pipeName}");
        }

        public void StopServer()
        {
            if (!IsRunning) return;

            CancellationTokenSource cts = _cts;
            Task worker = _serverWorkerTask;
            _cts = null;
            _serverWorkerTask = null;

            try
            {
                cts.Cancel();
                // Closing the listening stream aborts a pending WaitForConnectionAsync so the worker exits.
                NamedPipeServerStream active = Interlocked.Exchange(ref _activeStream, null);
                active?.Dispose();
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[URDT] Exception during Named Pipe Server shutdown: {ex.Message}");
            }

            // The CTS must outlive every pending pipe I/O callback: disposing it while the overlapped
            // connect completion is still registered throws ObjectDisposedException on a thread-pool
            // thread, which is unhandled and takes down the Unity Editor on domain reload / play-mode exit.
            if (worker == null)
            {
                cts.Dispose();
                return;
            }

            worker.ContinueWith(_ => cts.Dispose(), TaskScheduler.Default);
        }

        private async Task ServerLoopAsync(string pipeName, CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                NamedPipeServerStream serverStream = null;
                try
                {
                    serverStream = new NamedPipeServerStream(
                        pipeName,
                        PipeDirection.InOut,
                        NamedPipeServerStream.MaxAllowedServerInstances,
                        PipeTransmissionMode.Byte,
                        PipeOptions.Asynchronous,
                        inBufferSize: 4096,
                        outBufferSize: 4096
                    );
                    Volatile.Write(ref _activeStream, serverStream);

                    await serverStream.WaitForConnectionAsync(ct).ConfigureAwait(false);
                    Debug.Log($"[URDT] Named Pipe client connected to \\\\.\\pipe\\{pipeName}");

                    while (!ct.IsCancellationRequested && serverStream.IsConnected)
                    {
                        int totalRead = 0;
                        while (totalRead < UrdtRawCommand.STRUCT_SIZE)
                        {
                            int read = await serverStream.ReadAsync(
                                _readBuffer,
                                totalRead,
                                UrdtRawCommand.STRUCT_SIZE - totalRead,
                                ct
                            ).ConfigureAwait(false);

                            if (read <= 0)
                            {
                                // Client disconnected
                                break;
                            }
                            totalRead += read;
                        }

                        if (totalRead < UrdtRawCommand.STRUCT_SIZE)
                        {
                            break; // Stream ended or client disconnected
                        }

                        // Validate CRC-8
                        if (!UrdtRawCommand.ValidateBufferCrc(_readBuffer, 0))
                        {
                            Interlocked.Increment(ref _crcErrors);
                            continue;
                        }

                        Interlocked.Increment(ref _framesReceived);

                        // Decode fixed binary fields (Zero Heap Allocation)
                        byte cmdType = _readBuffer[0];
                        byte pointerId = _readBuffer[1];
                        uint seq = BitConverter.ToUInt32(_readBuffer, 2);
                        float screenX = BitConverter.ToSingle(_readBuffer, 6);
                        float screenY = BitConverter.ToSingle(_readBuffer, 10);
                        float pressure = BitConverter.ToSingle(_readBuffer, 14);
                        uint timestampMs = BitConverter.ToUInt32(_readBuffer, 18);
                        uint idempotencyKey = BitConverter.ToUInt32(_readBuffer, 22);
                        byte crc8 = _readBuffer[26];

                        _lastSequenceNumber = seq;

                        var dispatcher = UrdtMainThreadDispatcher.Instance;
                        if (dispatcher == null) continue;

                        if (cmdType == UrdtCommandTypes.STEER || cmdType == UrdtCommandTypes.RESISTANCE)
                        {
                            var physCmd = new UrdtPhysicsCommand
                            {
                                IdempotencyKey = idempotencyKey,
                                PointerId = pointerId,
                                WorldTargetPos = new Vector2(screenX, screenY),
                                DragForce = pressure
                            };
                            dispatcher.EnqueuePhysics(physCmd);
                        }
                        else
                        {
                            var uiCmd = new UrdtUiCommand
                            {
                                IdempotencyKey = idempotencyKey,
                                PointerId = pointerId,
                                ScreenPos = new Vector2(screenX, screenY),
                                ActionType = cmdType
                            };
                            dispatcher.EnqueueUi(uiCmd);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    // Expected during server shutdown
                    break;
                }
                catch (ObjectDisposedException) when (ct.IsCancellationRequested)
                {
                    break;
                }
                catch (Exception ex)
                {
                    if (!ct.IsCancellationRequested)
                    {
                        Debug.LogWarning($"[URDT] Named Pipe error: {ex.Message}. Reconnecting...");
                        try
                        {
                            await Task.Delay(200, ct).ConfigureAwait(false);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    Interlocked.CompareExchange(ref _activeStream, null, serverStream);
                    if (serverStream != null)
                    {
                        try
                        {
                            if (serverStream.IsConnected) serverStream.Disconnect();
                            serverStream.Dispose();
                        }
                        catch { }
                    }
                }
            }
        }
    }
}
