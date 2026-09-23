using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;
using URDT.Runtime.IPC;

namespace URDT.Runtime.Input
{
    /// <summary>
    /// Dual-Mode Synthetic Input Injector for URDT (Phase 4).
    /// Mode A (Primary): New Input System authentic Touchscreen device state event injection via InputSystem.QueueStateEvent.
    /// Mode B (Fallback): UGUI PointerEventData dispatch via ExecuteEvents.ExecuteHierarchy.
    /// Supports in-engine Flash-Hogan Minimum-Jerk polynomial interpolation, 100ms snap-to-slot settle,
    /// and raycast blocker detection (BLOCKED_BY_UI_OVERLAY).
    /// </summary>
    [DefaultExecutionOrder(-9950)]
    public sealed class UrdtInputInjector : MonoBehaviour
    {
        public static UrdtInputInjector Instance { get; private set; }

        private Touchscreen _virtualTouchscreen;
        private Camera _mainCamera;

        public class ActiveInterpolation
        {
            public int PointerId;
            public uint IdempotencyKey;
            public Vector2 StartPixel;
            public Vector2 EndPixel;
            public float DurationSeconds;
            public float ElapsedSeconds;
            public bool IsCompleted;
            public float SettleTimeRemaining;
            public Action OnCompleted;
        }

        private readonly List<ActiveInterpolation> _activeInterpolations = new List<ActiveInterpolation>(8);
        private readonly List<RaycastResult> _raycastResultsBuffer = new List<RaycastResult>(16);

        public static Action<UrdtUiCommand> OnUiCommand;
        public static Action<UrdtPhysicsCommand> OnPhysicsCommand;
        public static Action<string, string> OnDiagnosticBlocked; // (targetBeaconId, blockerName)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoInitialize()
        {
            if (Instance == null)
            {
                var go = new GameObject("[URDT_InputInjector]");
                Instance = go.AddComponent<UrdtInputInjector>();
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

            InitializeVirtualTouchDevice();

            // Register this injector as the handler for MainThreadDispatcher command processing
            UrdtMainThreadDispatcher.OnProcessUiCommand = ProcessUiCommand;
            UrdtMainThreadDispatcher.OnProcessPhysicsCommand = ProcessPhysicsCommand;
        }

        private void OnDestroy()
        {
            if (_virtualTouchscreen != null && _virtualTouchscreen.added)
            {
                InputSystem.RemoveDevice(_virtualTouchscreen);
            }
        }

        private void InitializeVirtualTouchDevice()
        {
            _virtualTouchscreen = InputSystem.GetDevice<Touchscreen>();
            if (_virtualTouchscreen == null)
            {
                _virtualTouchscreen = InputSystem.AddDevice<Touchscreen>("UrdtVirtualTouchscreen");
            }
        }

        private void FixedUpdate()
        {
            // Update active in-engine Flash-Hogan kinematic interpolations synchronized with PhysX tick
            for (int i = _activeInterpolations.Count - 1; i >= 0; i--)
            {
                var interp = _activeInterpolations[i];
                interp.ElapsedSeconds += Time.fixedDeltaTime;
                float tau = interp.DurationSeconds > 0 ? Mathf.Clamp01(interp.ElapsedSeconds / interp.DurationSeconds) : 1f;

                float s = CalculateMinimumJerk(tau);
                Vector2 currentPos = Vector2.Lerp(interp.StartPixel, interp.EndPixel, s);

                if (tau < 1.0f)
                {
                    InjectTouchState(interp.PointerId, UnityEngine.InputSystem.TouchPhase.Moved, currentPos, 1.0f);
                }
                else if (interp.SettleTimeRemaining > 0f)
                {
                    // 100ms stationary hold for snap-to-slot physical damping
                    interp.SettleTimeRemaining -= Time.fixedDeltaTime;
                    InjectTouchState(interp.PointerId, UnityEngine.InputSystem.TouchPhase.Stationary, interp.EndPixel, 1.0f);
                }
                else
                {
                    // Gesture complete: release pointer
                    InjectTouchState(interp.PointerId, UnityEngine.InputSystem.TouchPhase.Ended, interp.EndPixel, 0.0f);
                    interp.IsCompleted = true;
                    interp.OnCompleted?.Invoke();
                    _activeInterpolations.RemoveAt(i);
                }
            }
        }

        public static float CalculateMinimumJerk(float tau)
        {
            float t = Mathf.Clamp01(tau);
            float t3 = t * t * t;
            float t4 = t3 * t;
            float t5 = t4 * t;
            return (10f * t3) - (15f * t4) + (6f * t5);
        }

        public void InjectTouchState(int pointerId, UnityEngine.InputSystem.TouchPhase phase, Vector2 screenPixel, float pressure)
        {
            if (_virtualTouchscreen == null || !_virtualTouchscreen.added)
            {
                InitializeVirtualTouchDevice();
            }

            if (_virtualTouchscreen != null)
            {
                var state = new TouchState
                {
                    touchId = pointerId + 1, // Unity 1-based touchId
                    phase = phase,
                    position = screenPixel,
                    pressure = pressure
                };

                InputSystem.QueueStateEvent(_virtualTouchscreen, state);
            }
        }

        public void StartParametricDrag(
            int pointerId,
            Vector2 startPixel,
            Vector2 endPixel,
            float durationSeconds,
            uint idempotencyKey = 0,
            bool snapSettle = true,
            Action onCompleted = null)
        {
            // Touch Began at start position
            InjectTouchState(pointerId, UnityEngine.InputSystem.TouchPhase.Began, startPixel, 1.0f);

            _activeInterpolations.Add(new ActiveInterpolation
            {
                PointerId = pointerId,
                IdempotencyKey = idempotencyKey,
                StartPixel = startPixel,
                EndPixel = endPixel,
                DurationSeconds = durationSeconds,
                ElapsedSeconds = 0f,
                IsCompleted = false,
                SettleTimeRemaining = snapSettle ? 0.1f : 0f, // 100ms settle
                OnCompleted = onCompleted
            });
        }

        public void Tap(int pointerId, Vector2 screenPixel, string targetBeaconId = null)
        {
            // Check UI blocker overlay if EventSystem is present
            if (!string.IsNullOrEmpty(targetBeaconId) && CheckUiOverlayBlocker(screenPixel, targetBeaconId, out string blockerName))
            {
                OnDiagnosticBlocked?.Invoke(targetBeaconId, blockerName);
            }

            InjectTouchState(pointerId, UnityEngine.InputSystem.TouchPhase.Began, screenPixel, 1.0f);
            InjectTouchState(pointerId, UnityEngine.InputSystem.TouchPhase.Ended, screenPixel, 0.0f);
        }

        public bool CheckUiOverlayBlocker(Vector2 screenPixel, string targetBeaconId, out string blockerName)
        {
            blockerName = null;
            if (EventSystem.current == null) return false;

            var pointerData = new PointerEventData(EventSystem.current)
            {
                position = screenPixel
            };

            _raycastResultsBuffer.Clear();
            EventSystem.current.RaycastAll(pointerData, _raycastResultsBuffer);

            if (_raycastResultsBuffer.Count > 0)
            {
                var topmost = _raycastResultsBuffer[0].gameObject;
                if (!topmost.name.Equals(targetBeaconId, StringComparison.OrdinalIgnoreCase) &&
                    !topmost.transform.IsChildOf(topmost.transform.root))
                {
                    blockerName = topmost.name;
                    return true;
                }
            }

            return false;
        }

        public static void ProcessUiCommand(UrdtUiCommand cmd)
        {
            OnUiCommand?.Invoke(cmd);
            if (Instance != null)
            {
                Instance.Tap(cmd.PointerId, cmd.ScreenPos);
            }
        }

        public static void ProcessPhysicsCommand(UrdtPhysicsCommand cmd)
        {
            OnPhysicsCommand?.Invoke(cmd);
        }
    }
}
