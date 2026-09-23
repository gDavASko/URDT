# URDT Wire Protocol v2: Technical Specification
*(Dual Transport, 4-Layer State Model, Reactive Event Bus & C# Memory Layout)*

---

## Document Context and System Relationships

This document is the authoritative engineering specification for the **URDT Wire Protocol v2** used by the Autonomous URDT AI Reviewer. It forms part of the modular documentation suite:

1. **[Core Architecture & Master Overview](URDT_Autonomous_Reviewer_Architecture_EN.md)** — Architectural vision, speed pyramid, Hexagonal core, and master subsystem map.
2. **[Preparation & Analysis Subsystem (Mode 1)](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)** — Pre-flight audit, scaffolding, and topological mapping.
3. **[Game & Testing Subsystem (Mode 2)](URDT_Autonomous_Reviewer_Game_Testing_EN.md)** — Live gameplay execution, multi-touch kinematics, and tactical dispatching.
4. **[Wire Protocol v2 Specification (This Document)](URDT_Wire_Protocol_Specification_EN.md)** — Low-level networking, binary frame layouts, JSON envelopes, and C# structs.
5. **[CI/CD & Headless Orchestration](URDT_CI_CD_Orchestration_EN.md)** — Headless execution, port isolation, and crash watchdogs.
6. **[Failure Evidence Contract](URDT_Failure_Evidence_Contract_EN.md)** — Schema for defect packages, crash dashcam, and self-healing.

---

## 1. Protocol Architecture & Dual Transport Model

The URDT Wire Protocol v2 provides bidirectional, ultra-low-latency data exchange between the Unity Engine runtime (`com.davasko.urdt`) and the external `CoreAgent` (Node.js / TypeScript):

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      URDT CORE AGENT (TypeScript / Node.js)                 │
│                                                                             │
│   ┌───────────────────────────┐             ┌───────────────────────────┐   │
│   │ Fast Kinematic Ticker     │             │ Strategic / Tactical Loop │   │
│   │ (16.6 ms Loop / hrtime)   │             │ (LangGraph / Micro-SLM)   │   │
│   └─────────────┬─────────────┘             └─────────────┬─────────────┘   │
└─────────────────┼─────────────────────────────────────────┼─────────────────┘
                  │ 27-byte Binary Frames                   │ JSON Envelopes
                  │ (< 0.05 ms IPC Target)                  │ (TCP_NODELAY)
                  ▼                                         ▼
┌───────────────────────────────────┐     ┌───────────────────────────────────┐
│     HIGH-SPEED IPC CHANNEL        │     │     FULL STATE & EVENT BUS        │
│  - Windows: Named Pipe            │     │  - WebSocket Server               │
│    \\.\pipe\urdt_fast_ticker      │     │    ws://127.0.0.1:9002            │
│  - Linux/macOS: Unix Socket       │     │  - State Snapshots (1 Hz / push)  │
│    /tmp/urdt_fast_ticker.sock     │     │  - Asynchronous Beacon Events     │
└─────────────────┬─────────────────┘     └─────────────────┬─────────────────┘
                  │                                         │
                  └────────────────────┬────────────────────┘
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         UNITY ENGINE RUNTIME (C#)                           │
│   ┌─────────────────────────────────────────────────────────────────────┐   │
│   │                     UrdtMainThreadDispatcher.cs                     │   │
│   │  - MpscRingBuffer<UrdtPhysicsCommand> (Drained in FixedUpdate, 50Hz)│   │
│   │  - MpscRingBuffer<UrdtUiCommand>      (Drained in Update, 60Hz)     │   │
│   └─────────────────────────────────────────────────────────────────────┘   │
│                                      │                                      │
│                  ┌───────────────────┴───────────────────┐                  │
│                  ▼                                       ▼                  │
│        [ UrdtInputInjector ]                   [ Beacons / Inspectors ]     │
│        (Touchscreen.current)                   (UI / Physics / Audio)       │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Four-Layer State Snapshot Model

Wire Protocol v2 structures engine telemetry across four discrete abstraction layers:

### 2.1. Spatial Layer (Dual Coordinates)
- `worldPos`: 3D Cartesian coordinates $[X, Y, Z]$ in meters (for spatial pathfinding, physics raycasting, and obstacle avoidance).
- `screenNormalized`: Normalized viewport coordinates $[u, v] \in [0.0 .. 1.0]$ (resolution- and aspect-ratio-independent).
- `screenPixels`: Physical on-screen display pixels $[X_{px}, Y_{px}]$ (for Fitts's law targeting and hitbox intersection).
- `frustumStatus`: Frustum culling status relative to the active camera (`InView`, `Occluded`, `BehindCamera`).

### 2.2. Physical and Kinematic Layer
- `velocity`: Linear and angular velocity vectors $[v_x, v_y, v_z]$.
- `mass`: RigidBody mass in kilograms.
- `isKinematic`: Boolean flag indicating non-simulated transform manipulation.
- `currentContacts[]`: Array of colliders currently in contact with the entity.
- `forces`: Active impulses, environmental drag coefficients, and applied spring tensions.

### 2.3. Event and Dynamic Hazard Layer
- Event Queue: Rolling buffer of recent engine callbacks (`OnCollisionEnter`, `OnTriggerStay`, `OnSnapped`, `OnResourceMined`).
- `activeHazards[]`: Detected environmental dangers (falling meteorites, incoming ballistic missiles, collapsing terrain) with calculated `timeToImpactMs` and blast radii.

### 2.4. UI and Control Context Layer
- `activeModalWindow`: Identifier of top-level blocking popup, or `null`.
- `blocksRaycasts`: Global raycast interception flag (`CanvasGroup.blocksRaycasts`).
- `stickState`: Virtual joystick state (`UrdtUiStickTarget`: deflection $[-1.0 .. +1.0]$, center, radius, dead zone).
- `activeTouches`: Active physical pointer channels currently registered in the engine.

---

## 3. Mechanic State Tree (`MechanicStateSnapshot`)

Root gameplay beacons (`Urdt2DModuleTarget` / `UrdtMechanicTarget`) emit structured hierarchical state snapshots:

```json
{
  "mechanicId": "M01_SnapToSlot",
  "timestamp": 1726912345678,
  "lifecycle": {
    "phase": "RUNNING",                // "INIT" | "RUNNING" | "ANIMATING" | "PAUSED" | "COMPLETED" | "FAILED"
    "isBusy": false,                   // true if cutscene/animation is playing and input is blocked
    "busyReason": null,                // "ChestOpeningAnimation", "VictoryBanner", "DialogTransition"
    "timeInCurrentPhaseMs": 1450
  },
  "progress": {
    "current": 2,
    "target": 3,
    "normalized": 0.667,
    "score": 250
  },
  "entities": {
    "draggables": [
      {
        "id": "item_cube_blue",
        "category": "blue",
        "state": "SNAPPED",            // "IDLE" | "DRAGGING" | "SNAPPED" | "RETURNING"
        "isJunk": false,
        "isInteractable": false,
        "currentSlotId": "slot_01",
        "screenNormalized": [0.35, 0.50]
      }
    ],
    "slots": [
      { "id": "slot_01", "acceptedCategory": "blue", "isOccupied": true, "occupantId": "item_cube_blue" },
      { "id": "slot_02", "acceptedCategory": "red", "isOccupied": false, "occupantId": null }
    ],
    "dynamicWorld": {
      "playerHp": 100,
      "resourceVeinHp": 45.0,
      "isMiningActive": true
    }
  },
  "animationState": {
    "spine": { "activeTrack": "idle", "isPlaying": true, "isLooping": true, "skin": "default" },
    "animator": { "currentState": "Idle", "normalizedTime": 0.42, "isInTransition": false }
  }
}
```

---

## 4. Complete Reactive Event Bus Catalog

The reactive event bus dispatches asynchronous events (`type: "event"`) immediately upon occurrence:

### 4.1. UI Control & Window Events
- **Buttons & Selectables:** `ui_pointer_enter`, `ui_pointer_exit`, `ui_clicked`, `ui_double_clicked`, `ui_long_pressed` (`holdDurationMs > 500`), `ui_selection_gained`, `ui_selection_lost`.
- **Toggles & Radio Groups:** `ui_toggle_changed` (`isOn`, `groupId`), `ui_radio_selected`.
- **Sliders & Progress Bars:** `ui_slider_drag_start`, `ui_slider_value_changed` (`rawValue`, `normalized`), `ui_slider_limit_reached` (`"MIN"` / `"MAX"`).
- **Dropdowns:** `ui_dropdown_opened`, `ui_dropdown_closed`, `ui_dropdown_item_highlighted`, `ui_dropdown_selected` (`index`, `value`, `label`).
- **Input Fields (TMP):** `ui_input_focus_gained`, `ui_input_focus_lost`, `ui_input_text_changed` (`newText`, `charCount`), `ui_input_submitted`, `ui_input_cancelled`, `ui_input_validation_failed`.
- **Scroll Views (ScrollRect):** `ui_scroll_started`, `ui_scroll_delta` (`normalizedPos`), `ui_scroll_ended`, `ui_scroll_edge_reached` (`"TOP"` / `"BOTTOM"` / `"LEFT"` / `"RIGHT"`), `ui_scroll_elastic_bounce`.
- **Windows & CanvasGroups:** `ui_window_opening`, `ui_window_opened`, `ui_window_closing`, `ui_window_closed`, `ui_backdrop_clicked`, `ui_raycast_block_changed`.
- **Tabs & Accordions:** `ui_tab_changed` (`oldTab`, `newTab`), `ui_accordion_toggled` (`sectionId`, `isExpanded`).

### 4.2. Physics, Trigger & Spatial Events
- **Triggers (2D / 3D):** `physics_trigger_enter`, `physics_trigger_stay`, `physics_trigger_exit` (`targetId`, `otherColliderId`, `otherTag`, `otherLayer`, `durationInsideMs`).
- **Collisions (2D / 3D):** `physics_collision_enter`, `physics_collision_exit` (`targetId`, `otherColliderId`, `relativeVelocity`, `contactPoint`, `normal`, `impulseMagnitude`).
- **Rigidbodies & Joints:** `physics_body_sleep`, `physics_body_wakeup`, `physics_velocity_threshold_exceeded`, `physics_joint_break` (`breakForce`).
- **Frustum Sensors:** `spatial_became_visible`, `spatial_became_invisible`, `spatial_proximity_enter/exit`.

### 4.3. Animation and Audio Events
- **Unity Animator (Mecanim):** `animator_state_entered`, `animator_state_exited`, `animator_transition_started`, `animator_transition_completed`, `animator_param_changed`, `animator_cue_triggered`.
- **Spine 2D Skeletal Animation:** `spine_animation_started`, `spine_animation_completed`, `spine_animation_interrupted`, `spine_user_event` (`eventName`, `intParam`, `floatParam`, `stringParam`, `audioClipName`).
- **Audio Inspector Events (`UrdtAudioInspector`):** `audio_clip_played` (`sourceId`, `clipName`, `mixerGroup`, `volume`, `pitch`, `timestampMs`) — runtime interception of `AudioSource.Play()`, `PlayOneShot()`, and FMOD Event Instances in Headless CI.

---

## 5. Thread Safety & Synchronization (`UrdtMainThreadDispatcher.cs`)

Because network I/O in Unity runs on background worker threads, direct invocation of engine APIs triggers fatal `UnityException: Can only be called from the main thread`.

`UrdtMainThreadDispatcher.cs` solves this with an **independent dual lock-free MPSC RingBuffer**:

```csharp
namespace URDT.Runtime.IPC
{
    public sealed class UrdtMainThreadDispatcher : MonoBehaviour
    {
        private readonly MpscRingBuffer<UrdtUiCommand> _uiQueue = new(256);
        private readonly MpscRingBuffer<UrdtPhysicsCommand> _physicsQueue = new(256);

        // UI commands drained strictly at the start of MonoBehaviour.Update()
        private void Update()
        {
            while (_uiQueue.TryDequeue(out var cmd))
            {
                UrdtInputInjector.ProcessUiCommand(cmd);
            }
        }

        // Physics inputs drained strictly at the start of MonoBehaviour.FixedUpdate()
        private void FixedUpdate()
        {
            while (_physicsQueue.TryDequeue(out var cmd))
            {
                UrdtInputInjector.ProcessPhysicsCommand(cmd);
            }
        }
    }
}
```

- **Overflow Policy:** Under command bursts, a `DiscardOldestWithWarning` policy is enforced, logging a telemetry warning without crashing.
- **Timing Synchronization:** Physics commands are quantized to `Time.fixedDeltaTime` (50 Hz / 20 ms). Every command carries a monotonic `worldRevision` and `idempotencyKey` preventing duplicate executions during multi-step `FixedUpdate` cycles.

---

## 6. Optimization Engine & Zero-Allocation Standards

### 6.1. Inspector Tracking Bitmask (`UrdtTrackingMask`)
```csharp
[Flags]
public enum UrdtTrackingMask
{
    None                = 0,
    PointerInput        = 1 << 0,
    DragAndDrop         = 1 << 1,
    SlotDocking         = 1 << 2,
    ValueChanges        = 1 << 3,
    ScrollEvents        = 1 << 4,
    WindowLifecycle     = 1 << 5,
    Triggers            = 1 << 6,
    Collisions          = 1 << 7,
    FrustumVisibility   = 1 << 8,
    AnimatorEvents      = 1 << 9,
    SpineEvents         = 1 << 10,
    AudioEvents         = 1 << 11,
    All                 = ~0
}
```

### 6.2. Zero-Allocation Path in Hot Paths
1. **Non-Allocating Physics:** Banned `Collision.contacts`. Beacons use static buffers `Physics.GetContacts(ContactPoint[] buffer)` (16 elements). If count $\ge 16$, contacts are drained using `ArrayPool<ContactPoint>.Shared`.
2. **Static Relay Delegates:** Dynamic lambda closures (`button.onClick.AddListener(() => ...)`) are strictly prohibited. Buttons route to static `UrdtEventRelay.OnButtonClicked(int beaconId)`.
3. **Buffer Pooling:** State serialization rents buffers via `ArraySegment<byte>` from `ArrayPool<byte>.Shared`.
4. **Spatial Overlap Throttling:** `Physics.OverlapSphere` queries are throttled to 12 Hz (at most once every 5 frames).

---

## 7. Handshake Protocol & Heartbeat

### 7.1. Handshake Exchange
Upon connection, client and server negotiate protocol versions:

```json
// Request from CoreAgent:
{
  "cmd": "handshake",
  "clientVersion": "2.0.0",
  "clientType": "URDT_CORE_AGENT",
  "sessionToken": "a8f2c31e-4509-42b7-810a-b28669c5e3d1"
}

// Response from Unity Server:
{
  "status": "connected",
  "protocolVersion": "2.0.0",
  "engine": "Unity",
  "engineVersion": "2022.3.x",
  "viewport": { "width": 1920, "height": 1080, "dpi": 96 },
  "activeScene": "URDT_2D_TestPolygon",
  "worldRevision": 1
}
```

- **Heartbeat:** Ping packet `{ "cmd": "ping", "t": timestamp }` every 1000 ms. If unacknowledged for $> 3000\text{ ms}$, soft reconnect is initiated.

---

## 8. Binary Wire Frame Specification (Named Pipe, 27 Bytes)

Transmitted at 50 Hz cadence with zero heap allocations (Little-Endian):

```
┌──────────────┬────────┬─────────────────────────┬───────────────────────────────┐
│ Byte Offset  │ Type   │ Field Name              │ Description                   │
├──────────────┼────────┼─────────────────────────┼───────────────────────────────┤
│ [Byte 0]     │ uint8  │ cmdType                 │ 0x01=STEER, 0x02=RESISTANCE   │
│ [Byte 1]     │ uint8  │ touchId                 │ 1..10 (Unity Touch ID)        │
│ [Bytes 2..5] │ uint32 │ sequenceNumber          │ Monotonic sequence counter    │
│ [Bytes 6..9] │ float  │ screenX                 │ Physical screen pixel X       │
│ [Bytes 10..13│ float  │ screenY                 │ Physical screen pixel Y       │
│ [Bytes 14..17│ float  │ pressure                │ Touch pressure (0.0 .. 1.0)   │
│ [Bytes 18..21│ uint32 │ targetTimestampMs       │ Quantization target timestamp │
│ [Bytes 22..25│ uint32 │ idempotencyKey          │ Hash for duplicate filtering  │
│ [Byte 26]    │ uint8  │ crc8                    │ Cyclic redundancy check       │
└──────────────┴────────┴─────────────────────────┴───────────────────────────────┘
TOTAL: Exactly 27 bytes per frame.
```

---

## 9. WebSocket JSON Envelopes

### 9.1. Command Envelope (CoreAgent $\to$ Unity)
```json
{
  "$schema": "urdt/command_v2.json",
  "idempotencyKey": "cmd_9f1a2b_1719283921",
  "worldRevision": 42,
  "action": "DRAG",
  "pointerId": 1,
  "params": {
    "startPos": { "x": 960.0, "y": 540.0 },
    "endPos": { "x": 1200.0, "y": 800.0 },
    "durationMs": 350,
    "speedProfile": "FLASH_HOGAN"
  },
  "timeoutMs": 1500
}
```

### 9.2. State Envelope (Unity $\to$ CoreAgent)
```json
{
  "$schema": "urdt/state_v2.json",
  "timestamp": 1719283921045,
  "worldRevision": 42,
  "sceneName": "MainGameScene",
  "activeModalId": null,
  "beacons": [
    {
      "id": "draggable_wood_block",
      "layer": "PHYSICS_2D",
      "screenPixel": { "x": 960.0, "y": 540.0 },
      "worldPos": { "x": 0.0, "y": 1.5, "z": 0.0 },
      "sizeScreen": { "x": 120.0, "y": 120.0 },
      "flags": { "isInteractable": true, "isSnapped": false }
    }
  ],
  "hazards": [],
  "eventsQueue": ["OnLevelStarted"]
}
```

---

## 10. C# Command Struct Memory Layout

```csharp
using System.Runtime.InteropServices;
using UnityEngine;

namespace URDT.Runtime.IPC
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct UrdtRawCommand
    {
        public byte CmdType;          // 1 byte
        public byte PointerId;        // 1 byte
        public uint SequenceNumber;   // 4 bytes
        public float ScreenX;         // 4 bytes
        public float ScreenY;         // 4 bytes
        public float Pressure;        // 4 bytes
        public uint TargetTimestampMs;// 4 bytes
        public uint IdempotencyKey;   // 4 bytes
        public byte Crc8;             // 1 byte
    } // Exact size: 27 bytes

    public struct UrdtUiCommand
    {
        public uint IdempotencyKey;
        public int PointerId;
        public Vector2 ScreenPos;
        public byte ActionType; // Tap, Swipe, Release
    }

    public struct UrdtPhysicsCommand
    {
        public uint IdempotencyKey;
        public int PointerId;
        public Vector2 WorldTargetPos;
        public float DragForce;
    }
}
```
