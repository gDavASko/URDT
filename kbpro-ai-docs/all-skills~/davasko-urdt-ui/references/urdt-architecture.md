# URDT Architecture & Runtime Engine

## System Overview

URDT (Unity Remote Debugging Transport) is an out-of-process, device-level debugging, inspection, and automated testing system for Unity.

```
┌─────────────────────────────────┐
│     AI Agent / External Tool     │
│   (Node.js / Python / CLI / CI)  │
└────────────────┬────────────────┘
                 │ WebSocket JSON-RPC (ws://127.0.0.1:7777)
                 ▼
┌─────────────────────────────────────────────────────────────┐
│                       Unity Editor                          │
│                                                             │
│  ┌───────────────────────┐       ┌───────────────────────┐  │
│  │   UrdtServerHost      │       │   MainThreadDispatcher│  │
│  │   (WebSocket Server)  │       │   (Frame Synchronization)│
│  └───────────┬───────────┘       └───────────┬───────────┘  │
│              │                               │              │
│  ┌───────────▼───────────┐       ┌───────────▼───────────┐  │
│  │   TestIdRegistry      │       │   InputSimulator      │  │
│  │   (Fast Handle Lookup)│       │   (Real InputSystem)  │  │
│  └───────────────────────┘       └───────────────────────┘  │
│                                                             │
│  ┌───────────────────────────────────────────────────────┐  │
│  │             Polymorphic Beacons (UrdtUiTarget)        │  │
│  │  [ButtonTarget] [ToggleTarget] [SliderTarget] ...     │  │
│  └───────────────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────────────┘
```

---

## Core Components

### 1. `UrdtServerHost`
- Runs a lightweight, non-blocking TCP/WebSocket server inside the Unity process.
- Starts automatically on scene startup when configured with `_startServerOnStart = true`.
- Authentication via pre-shared secret token (`urdt-test-poligon`).

### 2. `MainThreadDispatcher`
- WebSocket message callbacks arrive on background network threads.
- All Unity API interactions (reading Transform, invoking Raycasts, simulating input) MUST occur on the Unity Main Thread.
- `MainThreadDispatcher` queues incoming actions and pumps them during the `Update()` phase.

### 3. `InputSimulator` (Device-Level Honest Input)
- **CRITICAL INVARIANT**: URDT never triggers callbacks by directly calling `button.onClick.Invoke()`.
- Instead, `InputSimulator` creates synthetic InputSystem devices (Mouse, Touchscreen, Keyboard) and queues hardware events into the real `InputSystem` event queue (`InputState.Change`).
- EventSystem processes raycasting, press states, drags, and hover effects identically to a real physical user touching the screen or clicking the mouse.

### 4. `StateInspector`
- Inspects GameObjects and caches reflected `[TestInspectable]` properties.
- Computes exact screen space bounding rectangles (`ScreenRect`) and centers (`ScreenCenter`) using `RectTransformUtility.WorldToScreenPoint`.
- Works flawlessly across Canvas render modes (`ScreenSpaceOverlay`, `ScreenSpaceCamera`, `WorldSpace`).
