# Architecture of Autonomous URDT AI Reviewer for Unity Projects
*(Autonomous URDT QA Reviewer Architecture: Master Blueprint)*

---

## Document Context: Modular Documentation Suite

This document is the **Executive Master Blueprint** for the Autonomous URDT AI Reviewer. It defines the foundational vision, the 4-tier cognitive hierarchy, the Hexagonal architecture, and the system navigation hub.

All deep implementation details, wire protocols, CI/CD orchestration scripts, and contract schemas are maintained across specialized subsystem specifications:

```
                            SYSTEM DOCUMENTATION SUITE
   ┌──────────────────────────────────────────────────────────────────────────┐
   │            MASTER ARCHITECTURE BLUEPRINT (This Document)                 │
   │            Docs/URDT_Autonomous_Reviewer_Architecture_EN.md              │
   │  - Conceptual Vision, Postulates, Closed-Loop Self-Healing Paradigm      │
   │  - 4-Tier Cognitive Hierarchy (L1-L4) & Ports & Adapters Architecture    │
   │  - System Navigation Hub, Repository Layout & Phased Roadmap             │
   └────────────────────────────────────┬─────────────────────────────────────┘
                                        │
         ┌──────────────────────────────┼──────────────────────────────┐
         ▼                              ▼                              ▼
┌─────────────────────────────┐ ┌─────────────────────────────┐ ┌─────────────────────────────┐
│ 1. PREPARATION & ANALYSIS   │ │ 2. GAME & TESTING           │ │ 3. WIRE PROTOCOL v2         │
│ (MODE 1 SPECIFICATION)      │ │ (MODE 2 SPECIFICATION)      │ │ (TECHNICAL SPECIFICATION)   │
│ [Preparation_Analysis_EN]   │ │ [Game_Testing_EN]           │ │ [Wire_Protocol_Spec_EN]     │
│ - Pre-Flight Environment    │ │ - L1 Kinematics & Touch     │ │ - 4-Layer Data Model        │
│   Compatibility Audit       │ │ - 9 Motor Primitives        │ │ - 27-byte Binary Frame IPC  │
│ - Auto-Instrumentation (DoD)│ │ - L2 Tactics & Micro-SLM    │ │ - WebSocket JSON Envelopes  │
│ - Triad Control Discovery   │ │ - GDD Invariant Assertion   │ │ - MPSC Dual RingBuffer      │
│ - Runtime Graphify (G=V,E)  │ │ - Save-State Engine (L4)    │ │ - C# Command Structs        │
│ - 4-Tier Defect Detection   │ │ - Anomaly Resolution        │ │ - Zero-Alloc C# Hot Paths   │
└─────────────────────────────┘ └─────────────────────────────┘ └─────────────────────────────┘
                                        │
         ┌──────────────────────────────┴──────────────────────────────┐
         ▼                                                             ▼
┌─────────────────────────────────────────────┐ ┌─────────────────────────────────────────────┐
│ 4. CI/CD & HEADLESS ORCHESTRATION           │ │ 5. FAILURE EVIDENCE CONTRACT                │
│ [CI_CD_Orchestration_EN]                    │ │ [Failure_Evidence_Contract_EN]              │
│ - Headless Batchmode Launch & Xvfb          │ │ - Normative UrdtFailureEvidencePacket Schema│
│ - UrdtTestRunner.cs Implementation          │ │ - In-Memory Crash Dashcam (25 WebP frames)  │
│ - Parallel Isolation (Ephemeral Port 0)     │ │ - Closed-Loop AI Coder Auto-Repair (.harness│
│ - Mono Heap Recycling Protocol (exit 42)    │ │ - Interactive QA_Audit_Report.html Cards    │
│ - Post-Mortem Native Crash Watchdog         │ │ - Failure Taxonomy (Stuck/Crash/Deadlock)   │
└─────────────────────────────────────────────┘ └─────────────────────────────────────────────┘
```

- 🗺️ **[Preparation & Analysis Subsystem (Mode 1)](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)**
- 🎮 **[Game & Testing Subsystem (Mode 2)](URDT_Autonomous_Reviewer_Game_Testing_EN.md)**
- ⚡ **[Wire Protocol v2 Specification](URDT_Wire_Protocol_Specification_EN.md)**
- 🚀 **[CI/CD & Headless Orchestration Specification](URDT_CI_CD_Orchestration_EN.md)**
- 🛡️ **[Failure Evidence Contract & Self-Healing](URDT_Failure_Evidence_Contract_EN.md)**

---

## 1. Mission and Conceptual Vision

### 1.1. Core Goal of the System
Transform **URDT (Unity Remote Debugging Transport)** into an **Autonomous QA, Topology & GDD Reviewer** with an unconditional focus on the **Unity-First Core**.

The reviewer operates as an embodied external agent that:
1. **Connects to any Unity project integrated with the URDT driver contract** with automatic reconnection upon Play Mode restarts.
2. **Discovers application topology (Runtime Graphify)**, constructing an observed multigraph $\mathcal{G} = (V, E)$ of screens, dialogs, transitions, and interactable beacons within declared coverage budgets.
3. **Discovers control schemes autonomously (Triad Control Scheme Discovery)** via GDD documentation, semantic on-screen tokens, and empirical physical micro-probing.
4. **Interacts at up to 60 Hz control cadence** via human-modeled multi-touch biomanipulation (Flash & Hogan minimum-jerk kinematics, Fitts's law targeting, physiological micro-jitter).
5. **Resolves unexpected popups, stalls, and threats** via an on-device Micro-SLM (Qwen2.5-1.5B/0.5B) within 40–60 ms.
6. **Validates game behavior against GDD specifications** across three abstraction tiers (DoD checklists, contractual state invariants, operational test hypotheses).
7. **Safely tests risky branches via atomic Save-State & Rollback engine** (`IUrdtSaveable`).
8. **Closes the autonomous AI development loop** by outputting machine-readable failure evidence packets (`.harness/failure_evidence.json`) and standalone interactive reports (`Docs/QA_Audit_Report.html`).

### 1.2. Fundamental Postulate: Only URDT (No Raw Computer Vision in the Fast Control Loop)
The system **completely eliminates raw computer vision (Pixel Vision / ViT)** from the fast control loop:
- **Zero VRAM capture overhead** at 500 MB/s, zero visual noise, zero GPU video encoding latency (33–50 ms).
- **Zero pixel coordinate hallucinations**, inherent to large multimodal models (VLMs).
- Control is strictly grounded upon the **contractual semantic runtime graph**:
  - UI Beacons: `UrdtUiTarget`, `UrdtUiWindowTarget`, `UrdtUiSliderTarget`.
  - Gameplay Beacons: `Urdt2DModuleTarget`, `Urdt2DDraggableTarget`, `Urdt2DSlotTarget`, `UrdtHazardTarget`.
  - Telemetry and physics streams transmitted over low-latency Loopback IPC (< 0.5 ms).

### 1.3. Closed-Loop AI Game Engineering Paradigm

```
                     CLOSED-LOOP AI GAME DEVELOPMENT PIPELINE
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │ 1. AI DEVELOPER (AI Engineer / IDE):                                        │
  │    Generates C# mechanics code, scenes, UI windows & beacons per GDD spec   │
  └──────────────────────────────────────┬──────────────────────────────────────┘
                                         │ dotnet build / Unity MCP recompile (Exit 0)
                                         ▼
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │ 2. AUTOMATIC RUNTIME LAUNCH (Unity MCP editor_play / Unity CLI batch):      │
  │    Launches game scene & binds URDT Named Pipe & WebSocket instantly        │
  └──────────────────────────────────────┬──────────────────────────────────────┘
                                         │ Handshake Protocol (Scene Ready)
                                         ▼
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │ 3. AUTONOMOUS URDT REVIEWER AGENT (Testing & Reviewer Agent):               │
  │    - Clears level via natural kinematic multi-touch (L1: Flash-Hogan)       │
  │    - Resolves modal popups, stalls, and threats (L2: HTN + BT + SLM)       │
  │    - Constructs topological application multigraph (L3: Runtime Graphify)   │
  │    - Validates compliance with GDD invariants and contracts                 │
  └──────────────────────────────────────┬──────────────────────────────────────┘
                                         │
                 ┌───────────────────────┴───────────────────────┐
                 │                                               │
           [ FAILURE DETECTED ]                          [ DECLARED SCOPE SUCCESS ]
                 │                                               │
                 ▼                                               ▼
  ┌───────────────────────────────┐               ┌───────────────────────────────┐
  │ 4. FAILURE EVIDENCE PACKET:   │               │ 5. MECHANIC CERTIFIED!        │
  │    - Exact L1 input trace     │               │    - Declared invariants      │
  │    - Beacon tree snapshot     │               │      exercised                │
  │    - Violated invariant ID    │               │    - Critical defects: 0       │
  │    - 25-frame WebP Dashcam    │               │    - Report: QA_Audit_Report   │
  └──────────────┬────────────────┘               └───────────────────────────────┘
                 │
                 ▼ (Autonomous Feedback Loop)
  ┌─────────────────────────────────────────────────────────────────────────────┐
  │ AI Developer parses Failure Evidence Packet, identifies root cause, applies │
  │ fixes to C# code / physics params, and re-triggers steps 1–3 autonomously!   │
  └─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Two Operating Modes & Transition Contract

The system operates across two strictly decoupled operational phases:

```
                            TWO OPERATING MODES
   ┌──────────────────────────────────────────────────────────────────────────┐
   │ MODE 1: PREPARATION & ANALYSIS                                           │
   │ Detailed Spec: Docs/URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md  │
   │ Focus: Pre-flight audit, beacon scaffolding, triad control discovery,    │
   │        Runtime Graphify (G = (V,E)), layout/localization/asset audits.   │
   │ Artifact: application_map.json + Structural defect cards                 │
   └────────────────────────────────────┬─────────────────────────────────────┘
                                        │
                         [ Transition: READY_FOR_TESTING ]
                         [ Fallback:   DISCOVERY_REQUIRED ]
                                        │
                                        ▼
   ┌──────────────────────────────────────────────────────────────────────────┐
   │ MODE 2: GAME & TESTING                                                   │
   │ Detailed Spec: Docs/URDT_Autonomous_Reviewer_Game_Testing_EN.md          │
   │ Focus: Live gameplay execution, L1 multi-touch biomanipulation,          │
   │        L2 tactical dispatch & Micro-SLM, GDD invariant validation,       │
   │        L4 save-state rollbacks, failure evidence generation.             │
   │ Artifact: QA_Audit_Report.html + Failure Evidence Packets                │
   └──────────────────────────────────────────────────────────────────────────┘
```

- **Mode Transition Contract:** Game / Testing Mode must not silently execute in an unmapped state. If the current screen or mechanic is missing from the map, the runner emits `DISCOVERY_REQUIRED` and re-engages Mode 1.
- **Evidence Restriction:** Mode 1 interactions are classified strictly as exploratory evidence and are never reported as proof of gameplay invariant satisfaction.

---

## 3. Cognitive Agent Hierarchy (4-Tier Speed Pyramid)

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                 LAYER 4: SAVE-STATE & DETERMINISM ENGINE                    │
│   Frequency: On-demand (Prior to risky branches / On failure / Replays)     │
│   Mechanism: IUrdtSaveable + Coroutine/DOTween/UniTask purge + Atomicity    │
│   Input: Checkpoint creation commands / Rollback state request              │
│   Output: Adapter-dependent state restoration & determinism replay          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Checkpoint and snapshot control
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       LAYER 3: STRATEGIST-REVIEWER (LLM)                    │
│   Frequency: 3 – 10 seconds (Asynchronous / On demand / Phase transition)   │
│   Model: High-capacity reasoning model (Deep Reasoning / CoT)               │
│   Input: GDD text + Scene map + Topological graph + L2 summary              │
│   Output: Audit plan, GDD test hypotheses, topological bug reports          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Directives & GDD asserts / Macro-goals
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       LAYER 2: TACTICAL DISPATCHER                          │
│   Mode 1 (Standard): Reactive state tracker (C#/JS native code, 0.01 ms)    │
│   Mode 2 (Anomalies): Standalone Micro-SLM (Qwen2.5-1.5B / SmolLM2, 50 ms)  │
│   Architecture: Two-tier hybrid (HTN planner + Reactive Behavior Tree)      │
│   Input: Open window graph + Delta stream + L3 subtask + Dynamic threats    │
│   Output: Channel motor commands for L1 + Modal & stall resolution          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Input primitives (TAP / DRAG / SWIPE / STEER)
                                       ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                       LAYER 1: OPERATIONAL BIOMANIPULATOR                   │
│   Frequency: up to 16.6 ms control cadence (60 Hz target)                   │
│   Mechanism: Flash & Hogan Minimum-Jerk Model + Fitts's Law + DMP           │
│   Dispatching: Dual-Mode Input (New Input System / Legacy) + MainThread     │
│   Input: Primitive from L2 + Beacon coordinates + Physics drag deltas       │
│   Output: URDT commands (PointerDown, PressMove, Swipe, PointerUp)          │
└──────────────────────────────────────┬──────────────────────────────────────┘
                                       │ Synthetic input (Virtual Touch / Raycasts)
                                       ▼
                      [ UNITY ENGINE: PLAYGROUND / GAME ]
```

---

## 4. Unity-First Core Architecture (Hexagonal / Ports & Adapters)

The reviewer is decoupled from the engine SDK via standardized Wire Protocol v2. The core focus is 100% committed to Unity (`com.davasko.urdt`), with future platform adapters (Unreal Engine C++, Cocos Creator, Web) supported via the decoupled protocol:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                      URDT CORE AGENT (TypeScript / Node.js)                 │
│                                                                             │
│   ┌─────────────────────┐   ┌─────────────────────┐   ┌──────────────────┐  │
│   │ L3: GDD Orchestrator│   │ L2: Tactical State  │   │ L1: Multi-Touch  │  │
│   │ & Runtime Graphify  │──►│ (Threat & SLM Brain)│──►│ Ticker (16.6 ms) │  │
│   └─────────────────────┘   └─────────────────────┘   └────────┬─────────┘  │
└────────────────────────────────────────────────────────────────┼────────────┘
                                         ▲                       │
                 Rich Semantic Snapshot  │                       │ Multi-Channel Commands
                 (World + Screen + Events)                       │ (P0: UI, P1: Gas/Mine,
                                         │                       │  P2: Stick, P3: Evasion)
                                         │                       ▼
┌────────────────────────────────────────┴────────────────────────────────────┐
│                    URDT WIRE PROTOCOL v2 (WebSocket / IPC)                  │
│       Specification: Docs/URDT_Wire_Protocol_Specification_EN.md            │
└───────────────────┬────────────────────────────────────────┬────────────────┘
                    ▼                                        │ (Roadmap: Future)
         ┌─────────────────────┐                             ▼
         │ Unity Engine Client │                  ┌─────────────────────┐
         │ (com.davasko.urdt)  │                  │ Other Engines (UE)  │
         │ [100% Core Focus]   │                  │ [Future Adapters]   │
         └─────────────────────┘                  └─────────────────────┘
```

---

## 5. Lifecycle and Play Mode Resilience

- **Play Mode Stop/Restart Resilience:** When the developer halts or restarts Play Mode in Unity Editor (`editor_stop` $\to$ `editor_play`), `CoreAgent` transitions smoothly into an exponential reconnect loop (500 ms, 1000 ms, 2000 ms). As soon as Play Mode restarts, the session re-establishes connection and resumes execution automatically.
- **Heartbeat & Safety Watchdogs:** Heartbeat ping dispatched every 1000 ms; touch contacts held longer than 10 seconds without active hold conditions are automatically released.
- **Dual Execution Workflows:**
  - *Interactive In-Editor Mode (`--watch`):* Controlled via official Unity MCP server; interactive local web dashboard at `http://localhost:3000`.
  - *Autonomous Headless CI Pipeline (`--ci`):* Controlled via CLI batch execution; isolated parallel execution using ephemeral ports and unique Named Pipe GUIDs (see [CI/CD Specification](URDT_CI_CD_Orchestration_EN.md)).

---

## 6. Repository Boundary and Directory Structure

The architecture spans two logical deliverables:
1. **URDT Unity Package:** Runtime transport, input simulation, inspectors, and C# contracts.
2. **CoreAgent Companion Project:** External Node.js/TypeScript orchestration and reasoning runtime.

```
E:\Projects\URDT\
├── Packages\com.davasko.urdt\                 # Unity C# transport, beacons, and Multi-Touch
│   └── Runtime\
│       ├── Beacons\                           # Urdt2DDraggableTarget, UrdtHazardTarget, etc.
│       ├── Input\                             # UrdtInputInjector (Dual New/Legacy), FlashHoganInterpolator
│       ├── IPC\                               # UrdtNamedPipeServer, UrdtWebSocketServer, RingBuffer
│       ├── SaveState\                         # UrdtSaveStateTarget, IUrdtSaveable, IUrdtContainerAdapter
│       ├── Inspectors\                        # UrdtAudioInspector, UrdtLayoutInspector, UrdtCrashDashcam
│       └── Runner\                            # UrdtTestRunner (Headless CI runner)
├── Docs\
│   ├── URDT_Autonomous_Reviewer_Architecture_EN.md            # Master Architectural Blueprint (This document)
│   ├── URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md    # Mode 1: Preparation & Application Mapping
│   ├── URDT_Autonomous_Reviewer_Game_Testing_EN.md            # Mode 2: Live Gameplay Execution & Testing
│   ├── URDT_Wire_Protocol_Specification_EN.md                 # Technical Wire Protocol v2 Specification
│   ├── URDT_CI_CD_Orchestration_EN.md                         # Headless CI/CD, Batchmode & Isolation
│   ├── URDT_Failure_Evidence_Contract_EN.md                   # Defect Schema, Dashcam & Self-Healing
│   └── QA_Audit_Report.html                                   # Standalone interactive HTML report
└── CoreAgent\                                 # Independent engine-agnostic kernel (Node.js / TS)
    ├── package.json                           # @langchain/langgraph, node-llama-cpp, ws
    ├── tsconfig.json
    ├── models\
    │   ├── qwen2.5-1.5b-instruct-q4_k_m.gguf  # Primary tactical model for L2
    │   └── qwen2.5-0.5b-instruct-q4_k_m.gguf  # Lightweight model for constrained CI nodes
    └── src\
        ├── protocol\                          # Wire types, Pipe and WebSocket clients
        ├── save_state\                        # Save-state controller and sandbox manager
        ├── control_discovery\                 # GDD reader, screen analyzer, micro-probe engine
        ├── perception\                        # Explicit hazard tracker and ballistic detector
        ├── l1_kinematics\                     # Flash-Hogan, Fitts's law, multi-touch ticker
        ├── l2_tactics\                        # Tactical state graph, Micro-SLM arbiter
        ├── l3_gdd\                            # GDD parser bridge, LangGraph nodes
        └── index.ts                           # Main CLI entry point (--ci / --watch)
```

---

## 7. Phased Implementation Roadmap

| Phase | Focus Subsystem | Key Deliverable |
| :--- | :--- | :--- |
| **Phase 1: Wire Protocol v2 in Unity** | [Wire Protocol Specification](URDT_Wire_Protocol_Specification_EN.md) | `UrdtServerHost`, Named Pipe 27-byte frames, MPSC RingBuffer. |
| **Phase 2: CoreAgent Skeleton** | [Master Architecture](URDT_Autonomous_Reviewer_Architecture_EN.md) | Companion TS runtime, Handshake manager, Play Mode auto-reconnect. |
| **Phase 3: Mode 1 Preparation & Scaffolding**| [Preparation & Analysis](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md) | `UrdtAutoInstrumentation`, Triad Control Discovery, Runtime Graphify. |
| **Phase 4: L1 Biomanipulator & Multi-Touch** | [Game & Testing](URDT_Autonomous_Reviewer_Game_Testing_EN.md) | Multi-touch channels 0..3 $\to$ 1..10, 9 motor primitives, Flash-Hogan. |
| **Phase 5: L2 Tactician & Micro-SLM** | [Game & Testing](URDT_Autonomous_Reviewer_Game_Testing_EN.md) | Micro-SLM (Qwen2.5) via `node-llama-cpp`, HTN-over-BT, anti-looping. |
| **Phase 6: CI/CD & Self-Healing Loop** | [CI/CD](URDT_CI_CD_Orchestration_EN.md) & [Failure Contract](URDT_Failure_Evidence_Contract_EN.md) | Headless runner, port 0 isolation, crash dashcam, `QA_Audit_Report.html`. |

---

## 8. Security, Isolation, and Hardening

1. **Local Socket Authentication Tokens:** Session tokens (`GUID v4` or `-urdtToken`) must be negotiated during handshake; unauthorized connections are rejected with close code `4401`.
2. **Save-State Sandbox Isolation:** File write operations are strictly confined to `Application.persistentDataPath + "/URDT_Sandbox/"`.
3. **HTML Dashboard Sanitization:** All dynamic strings (scene names, button labels, stack traces) are sanitized via `escapeHtml()` to eliminate XSS vectors.
4. **Prompt Injection Hardening for Micro-SLM:** Runtime UI text is encapsulated in XML tags (`<screen_ui>` and `<beacon_text>`) with allowlist action schema enforcement and zero filesystem or tool authority.
