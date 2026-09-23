# URDT Failure Evidence Contract & Self-Healing Specification
*(Machine-Readable Defect Schema, In-Memory Crash Dashcam & Closed-Loop AI Repair)*

---

## Document Context and System Relationships

This document is the authoritative engineering specification for the **URDT Failure Evidence Contract** and autonomous self-healing data structures. It forms part of the modular documentation suite:

1. **[Core Architecture & Master Overview](URDT_Autonomous_Reviewer_Architecture_EN.md)** — Architectural vision, speed pyramid, Hexagonal core, and master subsystem map.
2. **[Preparation & Analysis Subsystem (Mode 1)](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)** — Pre-flight audit, scaffolding, and topological mapping.
3. **[Game & Testing Subsystem (Mode 2)](URDT_Autonomous_Reviewer_Game_Testing_EN.md)** — Live gameplay execution, multi-touch kinematics, and tactical dispatching.
4. **[Wire Protocol v2 Specification](URDT_Wire_Protocol_Specification_EN.md)** — Low-level networking, binary frame layouts, JSON envelopes, and C# structs.
5. **[CI/CD & Headless Orchestration](URDT_CI_CD_Orchestration_EN.md)** — Headless execution, port isolation, and crash watchdogs.
6. **[Failure Evidence Contract (This Document)](URDT_Failure_Evidence_Contract_EN.md)** — Schema for defect packages, crash dashcam, and self-healing.

---

## 1. Strategic Role in Closed-Loop AI Game Engineering

The primary bottleneck of autonomous game engineering by AI coding agents is the absence of actionable, deterministic feedback from the runtime environment. When generated C# code fails in execution, a standard stack trace is rarely sufficient: it lacks physical coordinates, input history, beacon states, and visual context.

The **`UrdtFailureEvidencePacket`** closes this gap by transforming runtime anomalies into rich, structured diagnostic packets:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│ 1. AI DEVELOPER (AI Engineer / IDE):                                        │
│    Generates C# mechanics code, scenes, UI windows & beacons per GDD spec   │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ dotnet build / Unity MCP recompile (Exit 0)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 2. AUTONOMOUS URDT REVIEWER RUNNER:                                         │
│    Executes L3 GDD invariants via L1 Flash-Hogan kinematic biomanipulator    │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Anomaly Detected: Invariant / Crash / Stuck
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│ 3. FAILURE EVIDENCE SYNTHESIS:                                              │
│    - Freeze rolling in-memory crash dashcam (25 WebP frames)                │
│    - Capture active beacon state tree snapshot                              │
│    - Compile chronological L1 input history                                 │
│    - Synthesize recommended AI fix hint                                     │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Dual Export Protocol
                   ┌───────────────────┴───────────────────┐
                   ▼                                       ▼
┌─────────────────────────────────────┐ ┌─────────────────────────────────────┐
│ MACHINE LOOP (.harness/failure.json)│ │ HUMAN LOOP (QA_Audit_Report.html)   │
│ Ingested by AI Coder / cli-judge.js │ │ Interactive animated report for     │
│ Autonomous C# code patch formulated │ │ human developers & QA leads         │
└──────────────────┬──────────────────┘ └─────────────────────────────────────┘
                   │
                   ▼ (Autonomous Repair Execution)
┌─────────────────────────────────────────────────────────────────────────────┐
│ AI Coder modifies C# scripts, triggers compilation, and re-executes tests! │
└─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Failure & Stagnation Taxonomy

Every failure packet is categorized under a normative taxonomy:

| Stagnation / Failure Type | Trigger Condition | Primary Diagnostic Payload |
| :--- | :--- | :--- |
| **`MICRO_STUCK`** | Physical resistance encountered during dragging; velocity stalled for $> 0.5\text{ s}$. | Drag delta, contact normal, collider ID. |
| **`MACRO_STUCK`** | Screen state unchanged for $> 3.0\text{ s}$ during active gameplay with no blocking animations. | Invariant ID, active modal name, beacon tree. |
| **`CRASH_EXCEPTION`** | Unhandled `NullReferenceException`, missing asset reference in Unity console. | C# stack trace, faulting script name, line number. |
| **`DEADLOCK_TOPOLOGY`**| Screen lacks outbound edges, missing Close/Back buttons; cyclic re-planning exhausted. | Graph node ID, visited node history $\mathcal{P}$. |
| **`LAYOUT_DEFECT`** | Text clipped, bounds overflow, font auto-sized $< 10\text{ pt}$, missing glyph `\uFFFD`. | RectTransform world corners, TMP component path. |
| **`ASSET_STREAMING_STALL`**| Addressables or UnityWebRequest download progress $\Delta p = 0$ for $\ge 15\text{ s}$ or HTTP error. | Stalled URL, HTTP status code, download percentage. |
| **`NATIVE_ENGINE_CRASH`**| OS-level process crash (`SIGSEGV`, `0xC0000005`, native OOM). | Native stack trace from `Player.log`, faulting binary. |

---

## 3. Normative JSON Schema (`UrdtFailureEvidencePacket`)

```json
{
  "$schema": "http://json-schema.org/draft-07/schema#",
  "title": "UrdtFailureEvidencePacket",
  "type": "object",
  "required": [
    "incidentId",
    "timestamp",
    "failingPhase",
    "gddInvariantViolated",
    "stagnationType",
    "lastKnownWorldRevision",
    "activeScene",
    "beaconStateSnapshot",
    "actionHistoryBeforeFailure",
    "diagnostics"
  ],
  "properties": {
    "incidentId": { "type": "string" },
    "timestamp": { "type": "number" },
    "failingPhase": { "type": "string" },
    "gddInvariantViolated": { "type": "string" },
    "stagnationType": {
      "type": "string",
      "enum": [
        "MICRO_STUCK",
        "MACRO_STUCK",
        "CRASH_EXCEPTION",
        "DEADLOCK_TOPOLOGY",
        "LAYOUT_DEFECT",
        "ASSET_STREAMING_STALL",
        "NATIVE_ENGINE_CRASH"
      ]
    },
    "lastKnownWorldRevision": { "type": "number" },
    "activeScene": { "type": "string" },
    "beaconStateSnapshot": { "type": "array" },
    "actionHistoryBeforeFailure": { "type": "array" },
    "diagnostics": {
      "type": "object",
      "properties": {
        "engineConsoleErrors": { "type": "array", "items": { "type": "string" } },
        "activeModalDialog": { "type": ["string", "null"] },
        "recommendedAiFix": { "type": "string" },
        "crashDashcamBase64": {
          "type": "string",
          "description": "25-frame animated WebP captured by in-memory ring-buffer"
        },
        "assetStreamingInfo": {
          "type": ["object", "null"],
          "properties": {
            "stalledUrl": { "type": "string" },
            "lastReportedProgress": { "type": "number" },
            "stalledDurationSec": { "type": "number" },
            "httpStatusCode": { "type": ["number", "null"] }
          }
        },
        "nativeCrashDump": {
          "type": ["object", "null"],
          "properties": {
            "exitCodeHex": { "type": "string" },
            "signalName": { "type": ["string", "null"] },
            "faultingModule": { "type": "string" },
            "nativeStackTrace": { "type": "array", "items": { "type": "string" } },
            "logTailSnippet": { "type": "string" }
          }
        }
      }
    }
  }
}
```

---

## 4. Concrete Failure Evidence Packet Example

```json
{
  "incidentId": "INC_20260922_M01_042",
  "timestamp": 1726912345678,
  "failingPhase": "M01_SnapToSlot",
  "gddInvariantViolated": "INV-01_SNAP_SUCCESS",
  "stagnationType": "CRASH_EXCEPTION",
  "lastKnownWorldRevision": 42,
  "activeScene": "URDT_2D_TestPolygon",
  "beaconStateSnapshot": [
    {
      "id": "item_cube_blue",
      "layer": "PHYSICS_2D",
      "screenPixel": { "x": 450.0, "y": 310.0 },
      "worldPos": { "x": -1.2, "y": 0.4, "z": 0.0 },
      "velocity": [0.0, 0.0, 0.0],
      "flags": { "isInteractable": true, "isSnapped": false }
    },
    {
      "id": "slot_01_blue",
      "layer": "PHYSICS_2D",
      "screenPixel": { "x": 450.0, "y": 310.0 },
      "flags": { "isOccupied": false }
    }
  ],
  "actionHistoryBeforeFailure": [
    {
      "commandId": "cmd_001",
      "action": "DRAG",
      "pointerId": 0,
      "targetBeaconId": "item_cube_blue",
      "durationMs": 350,
      "timestamp": 1726912345200
    }
  ],
  "diagnostics": {
    "engineConsoleErrors": [
      "NullReferenceException: Object reference not set to an instance of an object",
      "  at SnapZoneController.OnTriggerEnter2D (UnityEngine.Collider2D other) [0x00012] in <Assets/Scripts/SnapZoneController.cs:42>"
    ],
    "activeModalDialog": null,
    "recommendedAiFix": "In SnapZoneController.cs line 42, verify that 'slotData' is assigned before dereferencing slotData.acceptedCategory.",
    "crashDashcamBase64": "data:image/webp;base64,UklGRmYAAABXRUJQVlA4IFoAAAAwAQCdASoZAA8APw0G...",
    "assetStreamingInfo": null,
    "nativeCrashDump": null
  }
}
```

---

## 5. In-Memory Crash Dashcam (`UrdtCrashDashcam.cs`)

To provide visual context without imposing the severe disk and performance penalties of continuous video capture in CI:

```
                  [ Rolling 5-Second History Buffer in RAM ]
   ┌────────┬────────┬────────┬────────┬────────┬────────┬────────┬────────┐
   │ Frame 1│ Frame 2│ Frame 3│ Frame 4│ ...... │Frame 23│Frame 24│Frame 25│
   └────────┴────────┴────────┴────────┴────────┴────────┴────────┴────────┘
     200 ms   400 ms   600 ms   800 ms            4.6 s    4.8 s    5.0 s
```

- **Ring Buffer Parameters:** 25 lightweight JPEG frames (640x360 resolution, 60% quality, ~60 KB per frame).
- **Sampling Frequency:** 5 frames per second ($T = 200\text{ ms}$), maintaining a continuous 5-second window.
- **Memory Footprint:** Merely **~1.5 MB total RAM**, avoiding memory bloat during multi-hour test runs.
- **Zero Render Stalls:** Frames are retrieved via asynchronous GPU memory transfers (`AsyncGPUReadback.Request`), preventing main render thread micro-stutters.
- **Freezing on Anomaly:** The instant a crash, soft-lock, or invariant violation occurs, the ring buffer freezes, compiles into an animated WebP/GIF using an embedded zero-alloc encoder, and attaches as `crashDashcamBase64` in the defect card.

---

## 6. Dual Export Protocol

The generated failure evidence is published through two independent channels:

### 6.1. Machine Loop (`.harness/failure_evidence.json`)
- Written directly to repository root `.harness/failure_evidence.json`.
- Serves as the standardized entry contract for autonomous AI coding agents (OpenAI Codex, Google Antigravity, Harness Protocol `cli-judge.js`).
- Allows coding agents to immediately parse:
  - Exact failing C# file and line number.
  - Invariant ID violated.
  - Physical beacon states at the moment of failure.
- Coding agents modify the offending C# script, recompile via Unity MCP, and re-trigger test execution, completing the closed-loop self-healing cycle.

### 6.2. Human Loop (`Docs/QA_Audit_Report.html`)
- Embedded into the standalone interactive HTML audit report.
- Features:
  - Visual animated playback of the 25-frame dashcam.
  - Clickable chronological action timeline.
  - Exact reproduction steps with beacon state diffs.
  - Highlighted pulsing red node on the Vis-Network application multigraph.
