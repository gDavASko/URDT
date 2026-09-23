using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace URDT.Runtime.IPC
{
    /// <summary>
    /// Binary command types transmitted over fast ticker Named Pipe.
    /// </summary>
    public static class UrdtCommandTypes
    {
        public const byte STEER = 0x01;
        public const byte RESISTANCE = 0x02;
        public const byte TAP = 0x03;
        public const byte DRAG = 0x04;
        public const byte RELEASE = 0x05;
        public const byte HEARTBEAT = 0xFF;
    }

    /// <summary>
    /// Fixed 27-byte binary layout for zero-allocation IPC transmission over Windows Named Pipes.
    /// Exactly 27 bytes packed with Pack = 1.
    /// </summary>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UrdtRawCommand
    {
        public const int STRUCT_SIZE = 27;

        public byte CmdType;           // 1 byte: Command identifier (e.g. 0x01=STEER, 0x02=RESISTANCE)
        public byte PointerId;         // 1 byte: 1..10 (Unity Touch / Pointer ID)
        public uint SequenceNumber;    // 4 bytes: Monotonically increasing sequence counter
        public float ScreenX;          // 4 bytes: Physical screen pixel coordinate X
        public float ScreenY;          // 4 bytes: Physical screen pixel coordinate Y
        public float Pressure;         // 4 bytes: Touch pressure (0.0 .. 1.0)
        public uint TargetTimestampMs; // 4 bytes: Target timestamp for quantization
        public uint IdempotencyKey;    // 4 bytes: Unique packet hash for duplicate filtering
        public byte Crc8;              // 1 byte: CRC-8 checksum over preceding 26 bytes

        static UrdtRawCommand()
        {
            int size = Marshal.SizeOf(typeof(UrdtRawCommand));
            Debug.Assert(size == STRUCT_SIZE, $"[URDT] UrdtRawCommand size mismatch! Expected {STRUCT_SIZE} bytes, got {size}.");
            if (size != STRUCT_SIZE)
            {
                throw new InvalidProgramException($"[URDT] UrdtRawCommand must be exactly {STRUCT_SIZE} bytes. Actual: {size}");
            }
        }

        /// <summary>
        /// Computes CRC-8 (polynomial 0x07, init 0x00) over the given byte span.
        /// </summary>
        public static byte ComputeCrc8(byte[] data, int offset, int length)
        {
            if (data == null || length <= 0) return 0;
            byte crc = 0x00;
            int end = offset + length;
            for (int i = offset; i < end; i++)
            {
                crc ^= data[i];
                for (int b = 0; b < 8; b++)
                {
                    if ((crc & 0x80) != 0)
                    {
                        crc = (byte)((crc << 1) ^ 0x07);
                    }
                    else
                    {
                        crc = (byte)(crc << 1);
                    }
                }
            }
            return crc;
        }

        /// <summary>
        /// Validates whether the provided 27-byte buffer matches its CRC-8 checksum.
        /// </summary>
        public static bool ValidateBufferCrc(byte[] buffer, int offset)
        {
            if (buffer == null || buffer.Length < offset + STRUCT_SIZE) return false;
            byte expectedCrc = buffer[offset + STRUCT_SIZE - 1];
            byte calculatedCrc = ComputeCrc8(buffer, offset, STRUCT_SIZE - 1);
            return expectedCrc == calculatedCrc;
        }
    }

    /// <summary>
    /// High-level decoupled UI command dispatched to Unity's main thread Update() loop.
    /// </summary>
    public struct UrdtUiCommand
    {
        public uint IdempotencyKey;
        public int PointerId;
        public Vector2 ScreenPos;
        public byte ActionType; // 0=Tap, 1=Drag, 2=Release, 3=Move, 4=Hover
    }

    /// <summary>
    /// High-level decoupled Physics command dispatched to Unity's main thread FixedUpdate() loop.
    /// </summary>
    public struct UrdtPhysicsCommand
    {
        public uint IdempotencyKey;
        public int PointerId;
        public Vector2 WorldTargetPos;
        public float DragForce;
    }

    /// <summary>
    /// Thread-safe bounded Multi-Producer Single-Consumer (MPSC) Ring Buffer.
    /// Operates with zero heap allocations during enqueue/dequeue cycles.
    /// Under command bursts, an oldest-discard policy (DiscardOldestWithWarning) is enforced.
    /// </summary>
    public sealed class MpscRingBuffer<T>
    {
        private readonly T[] _buffer;
        private readonly int _mask;
        private readonly object _lock = new();
        private int _head;
        private int _tail;
        private int _count;
        private long _droppedCount;

        public int Capacity => _buffer.Length;

        public int Count
        {
            get
            {
                lock (_lock) return _count;
            }
        }

        public MpscRingBuffer(int capacity = 256)
        {
            int pow2 = 1;
            while (pow2 < capacity) pow2 <<= 1;
            _buffer = new T[pow2];
            _mask = pow2 - 1;
        }

        public bool TryEnqueue(in T item)
        {
            lock (_lock)
            {
                if (_count == _buffer.Length)
                {
                    _head = (_head + 1) & _mask;
                    _count--;
                    _droppedCount++;
                    if ((_droppedCount % 50) == 1)
                    {
                        Debug.LogWarning($"[URDT] MpscRingBuffer<{typeof(T).Name}> buffer burst! Discarded {_droppedCount} frames. Capacity={_buffer.Length}");
                    }
                }

                _buffer[_tail] = item;
                _tail = (_tail + 1) & _mask;
                _count++;
                return true;
            }
        }

        public bool TryDequeue(out T item)
        {
            lock (_lock)
            {
                if (_count == 0)
                {
                    item = default;
                    return false;
                }

                item = _buffer[_head];
                _buffer[_head] = default;
                _head = (_head + 1) & _mask;
                _count--;
                return true;
            }
        }

        public void Clear()
        {
            lock (_lock)
            {
                Array.Clear(_buffer, 0, _buffer.Length);
                _head = 0;
                _tail = 0;
                _count = 0;
            }
        }
    }

    /// <summary>
    /// Dispatches inbound commands from asynchronous Named Pipe / WebSocket threads to Unity's main thread.
    /// Decouples UI commands (executed in Update) from Physics commands (quantized in FixedUpdate).
    /// Uses configurable Action delegates to avoid direct cross-namespace coupling.
    /// </summary>
    [DefaultExecutionOrder(-10000)]
    public sealed class UrdtMainThreadDispatcher : MonoBehaviour
    {
        public static UrdtMainThreadDispatcher Instance { get; private set; }

        /// <summary>
        /// Callback invoked on Update() for each dequeued UI command.
        /// Registered by UrdtInputInjector at runtime to avoid compile-time coupling.
        /// </summary>
        public static System.Action<UrdtUiCommand> OnProcessUiCommand;

        /// <summary>
        /// Callback invoked on FixedUpdate() for each dequeued Physics command.
        /// Registered by UrdtInputInjector at runtime to avoid compile-time coupling.
        /// </summary>
        public static System.Action<UrdtPhysicsCommand> OnProcessPhysicsCommand;

        private readonly MpscRingBuffer<UrdtUiCommand> _uiQueue = new(256);
        private readonly MpscRingBuffer<UrdtPhysicsCommand> _physicsQueue = new(256);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_MainThreadDispatcher]");
                Instance = go.AddComponent<UrdtMainThreadDispatcher>();
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
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public bool EnqueueUi(in UrdtUiCommand cmd)
        {
            return _uiQueue.TryEnqueue(cmd);
        }

        public bool EnqueuePhysics(in UrdtPhysicsCommand cmd)
        {
            return _physicsQueue.TryEnqueue(cmd);
        }

        private void Update()
        {
            while (_uiQueue.TryDequeue(out var cmd))
            {
                OnProcessUiCommand?.Invoke(cmd);
            }
        }

        private void FixedUpdate()
        {
            while (_physicsQueue.TryDequeue(out var cmd))
            {
                OnProcessPhysicsCommand?.Invoke(cmd);
            }
        }
    }
}

namespace KBP.URDT.IPC
{
    // Type aliases in the KBP.URDT namespace for seamless integration with existing codebase
    public static class UrdtCommandTypes
    {
        public const byte STEER = global::URDT.Runtime.IPC.UrdtCommandTypes.STEER;
        public const byte RESISTANCE = global::URDT.Runtime.IPC.UrdtCommandTypes.RESISTANCE;
        public const byte TAP = global::URDT.Runtime.IPC.UrdtCommandTypes.TAP;
        public const byte DRAG = global::URDT.Runtime.IPC.UrdtCommandTypes.DRAG;
        public const byte RELEASE = global::URDT.Runtime.IPC.UrdtCommandTypes.RELEASE;
        public const byte HEARTBEAT = global::URDT.Runtime.IPC.UrdtCommandTypes.HEARTBEAT;
    }

    public struct UrdtRawCommand
    {
        public global::URDT.Runtime.IPC.UrdtRawCommand Inner;
    }
}

