# Architecture of Autonomous URDT AI Reviewer: Game & Testing Subsystem
*(Mode 2: Live Gameplay Execution, Multi-Touch Kinematics, Tactics & GDD Invariant Assertion)*

---

## Document Context and System Relationships

This document is the normative architectural specification for **Mode 2 (Game & Testing)** of the URDT Autonomous Reviewer. It forms part of the three-pillar architectural documentation suite:

1. **[Core Architecture & System Foundations (Master Overview)](URDT_Autonomous_Reviewer_Architecture_EN.md)** — Core mission, speed pyramid, Hexagonal architecture, Wire Protocol v2, dual transports, process isolation, and CI/CD lifecycle.
2. **[Preparation & Analysis Subsystem](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)** — Project readiness audit, beacon auto-instrumentation, control scheme discovery, topological mapping (Runtime Graphify), layout auditing, and dead-end detection.
3. **[Game & Testing Subsystem (This Document)](URDT_Autonomous_Reviewer_Game_Testing_EN.md)** — Live gameplay execution, multi-touch biomanipulation (L1), tactical dispatching (L2), Micro-SLM cognitive arbiter, GDD invariant validation (L3), and save-state rollback engine (L4).

---

## 1. Mission and Conceptual Scope of Game & Testing Mode

### 1.1. Core Objectives
Once a target Unity project has been verified and mapped by Mode 1, **Mode 2 (Game & Testing)** assumes active, honest control of gameplay mechanics to validate that runtime behavior satisfies all declared design specifications.

Mode 2 operates as an embodied, highly skilled autonomous player that:
1. **Navigates to Target Mechanics:** Leverages the verified application multigraph $\mathcal{G} = (V, E)$ constructed in Mode 1 to perform autonomous pathfinding ($A^*$ / BFS) from initial menus to specific gameplay arenas.
2. **Executes Natural Multi-Touch Kinematics (Layer L1):** Drives gameplay via up to 4 concurrent multi-touch channels at 60 Hz cadence using human motor models (Flash & Hogan minimum-jerk interpolation and Fitts's law targeting).
3. **Arbitrates Tactical Intent and Evades Dynamic Threats (Layer L2):** Maintains tactical continuity via an HTN-over-BT hybrid executive, resolving dynamic hazards (incoming projectiles, falling hazards) and unpredicted popups via an on-device Micro-SLM (Qwen2.5-1.5B/0.5B).
4. **Asserts Contractual Invariants Against GDD Specifications (Layer L3):** Formulates and tests Hoare-triple invariants ($Precondition \xrightarrow{Action} ExpectedState$) in both nominal ("happy path") and deliberate edge-case / exploit scenarios.
5. **Manages Counterfactual State Exploration (Layer L4):** Safely stress-tests risky, destructive, or irreversible gameplay branches via an atomic 4-phase save-state and rollback engine (`IUrdtSaveable`).
6. **Closes the Autonomous Development Loop:** Upon detecting an invariant violation, soft-lock, or crash, generates a comprehensive `UrdtFailureEvidencePacket` complete with a 25-frame animated WebP dashcam capture, enabling external AI coding agents to autonomously diagnose and repair C# source code.

```
                         GAME & TESTING PIPELINE (MODE 2)
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 1. INGESTION OF APPLICATION MAP & GDD INVARIANTS:                           │
   │    Load application_map.json (Mode 1) + Decompose GDD rules into INV-xx     │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Test Suite Initialized
                                          ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 2. AUTONOMOUS GRAPH NAVIGATION (L3 -> L2):                                  │
   │    Execute A* pathfinding over multigraph G to reach target mechanic arena  │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Arena Active
                                          ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 3. LIVE KINEMATIC EXECUTION (L1 Flash-Hogan + L2 Tactics):                  │
   │    - Multi-touch control: Throttle hold + stick steering + action taps      │
   │    - Reactive threat evasion (Behavior Tree tick 50 ms)                     │
   │    - Cognitive Arbiter (Micro-SLM) resolves unpredicted popups/stalls       │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │
                  ┌───────────────────────┴───────────────────────┐
                  │                                               │
            [ INVARIANT VIOLATED / CRASH ]              [ INVARIANTS SATISFIED ]
                  │                                               │
                  ▼                                               ▼
   ┌───────────────────────────────┐               ┌───────────────────────────────┐
   │ 4. FAILURE EVIDENCE PACKET:   │               │ 5. MECHANIC CERTIFIED:        │
   │ - Full L1 input trace         │               │ - Invariants exercised: 100%  │
   │ - Beacon state snapshot       │               │ - Critical defects: 0         │
   │ - 25-frame WebP Dashcam       │               │ - Append evidence to report   │
   │ - Export: failure_evidence.json│              │ - Proceed to next mechanic    │
   └──────────────┬────────────────┘               └───────────────────────────────┘
                  │
                  ▼ (Closed-Loop Feedback)
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ AI Coder parses failure evidence, modifies C# code, and re-triggers Mode 2  │
   └─────────────────────────────────────────────────────────────────────────────┘
```

---

## 2. Layer L1: Multi-Channel Kinematic Biomanipulator

### 2.1. Multi-Touch Channel Architecture (`pointerId: 0..3` $\to$ Unity `touchId: 1..10`)
On modern mobile devices and touch displays, interactions require concurrent multi-finger dexterity.
Layer L1 manages a **Multi-Channel Touch Manager**, maintaining up to 4 concurrent logical channels:

```
                       ┌───────────────────────────────────────┐
                       │  L1 Multi-Channel Touch Manager (0..3)│
                       └───────────────────┬───────────────────┘
                                           │
         ┌───────────────────┬─────────────┴─────┬───────────────────┐
         ▼                   ▼                   ▼                   ▼
   [ Channel 0 ]       [ Channel 1 ]       [ Channel 2 ]       [ Channel 3 ]
   (Primary Finger)    (Secondary Finger)  (Tertiary Finger)   (Quaternary Finger)
   - Virtual Joystick  - Throttle (Gas)    - Fire / Attack     - Camera Orbit
   - Part Dragging     - Shield Block      - Resource Harvest  - Pinch-to-Zoom
```

- **Identifier Contract:**
  - `pointerId`: Logical URDT channel in the range `0..3`.
  - `touchId`: Non-zero virtual device identifier in Unity Input System in the range `1..10`.
  - Normative mapping: `touchId = pointerId + 1`. Channels `5..10` are reserved for project adapters.

### 2.2. Low-Level Command Packet Structure (`L1CommandPacket`)
```typescript
interface L1CommandPacket {
  commandId: string;
  pointerId: number; // Logical URDT channel 0..3; mapped to Unity touchId 1..4
  primitive: 'TAP' | 'DRAG' | 'SWIPE' | 'SLICE' | 'HOLD_EVENT_CONDITIONED' | 'CHARGE_AND_RELEASE' | 'CONTINUOUS_STEER_STREAM' | 'PINCH_ZOOM' | 'QTE_TIMED_TAP';
  
  targetBeaconId?: string;
  kinematicParams: {
    startScreenPixel?: { x: number; y: number };
    endScreenPixel?: { x: number; y: number };
    screenNormalized?: { x: number; y: number; isNormalized: boolean };
    waypoints?: Array<{ x: number; y: number }>;
    
    // For continuous and conditional primitives:
    holdCondition?: {
      targetEvent?: string;      // "OnResourceMined", "OnLapCompleted", "OnFuelEmpty"
      predicateKey?: string;     // "chargeNormalized >= 1.0"
      maxTimeoutMs: number;
    };
    
    steerVector?: { x: number; y: number }; // Joystick deflection [-1.0 .. +1.0]
    speedProfile?: 'minimum_jerk' | 'linear' | 'ballistic';
    durationMs?: number;                    // Gesture duration
    qteWindowMs?: { min: number; max: number }; // Allowed time window for QTE
  };
}
```

### 2.3. Extended Catalog of 9 Motor Primitives (L1)

| Primitive | Purpose | Key Parameters | Gameplay Example Scenarios |
| :--- | :--- | :--- | :--- |
| **`TAP`** | Single click / tap | `pos, holdMs=50, jitter=1.5px` | UI buttons, primary fire, dialog confirmation |
| **`DRAG`** | Smooth kinematic dragging | `startPos, endPos, speedProfile, durationMs` | 2D/3D puzzles, inventory moving, snap-to-slot |
| **`SWIPE`** | Ballistic flick / swipe | `startPos, direction, velocity, durationMs` | Endless runner lane changes, grenade tosses |
| **`SLICE`** | Continuous path slicing | `waypoints[], totalTimeMs` | Cutting mechanics, blade swings, rune drawing |
| **`HOLD_EVENT_CONDITIONED`**| Sustained hold until event | `pos, targetEvent, predicateKey, maxTimeoutMs` | Resource harvesting, vehicle gas pedal, shield block |
| **`CHARGE_AND_RELEASE`** | Variable charge / pullback | `pos, chargePredicate, maxMs` | Slingshots, bow draws, charged heavy attacks |
| **`CONTINUOUS_STEER_STREAM`**| Continuous joystick steer | `stickId, steerVector: [-1..1]` | Vehicle driving, twin-stick shooter movement |
| **`PINCH_ZOOM`** | Dual-finger pinch gesture | `center, deltaDistance, durationMs` | Strategic map zoom, camera scale modulation |
| **`QTE_TIMED_TAP`** | Precision timing execution | `pos, qteWindowMs: [t_min, t_max]` | Rhythm games, parry mechanics, green-zone lockpicking |

### 2.4. L1 Ticker Internal Loop (16.6 ms on `process.hrtime`) and Micro-Jitter
Inside Node.js, the kinematics ticker runs on `process.hrtime.bigint()` targeting a strict 16.6 ms cycle:

```
Every 16.6 ms:
  FOR EACH active channel (pointerId 0..3):
    1. If primitive == 'DRAG' or 'SWIPE' (Live Streaming Mode):
       - Calculate tau = (now - startTime) / durationMs.
       - Flash-Hogan displacement: S(tau) = 10*tau^3 - 15*tau^4 + 6*tau^5.
       - Inject physiological micro-tremor (Perlin Noise / Gaussian Jitter, sigma = 0.8px):
         P_final = P_base + Jitter(t).
       - send("press_move", { pointerId, x: P_final.x, y: P_final.y })
    2. If primitive == 'HOLD_EVENT_CONDITIONED' (mining / throttle hold):
       - Check condition: target event received (e.g. OnResourceMined) or timeout?
       - If YES:
           send("pointer_up", { pointerId, x, y })
           release channel.
       - If NO: retain finger on button with subpixel physiological micro-breathing.
    3. If primitive == 'CONTINUOUS_STEER_STREAM' (vehicle steering stick):
       - Update stick deflection vector based on physical target trajectory.
       - send("press_move", { pointerId, x: stickCenter.x + dx, y: stickCenter.y + dy })
    4. Closed-Loop Physical Feedback:
       - Continuously evaluate physical resistance heuristics.
       - If object is stuck/obstructed: L1 interrupts movement and notifies L2: RESISTANCE_DETECTED.
```

### 2.5. Hybrid Execution Scheme: In-Engine Flash-Hogan vs Live Streaming
1. **In-Engine Batch Execution (`DRAG`, `SWIPE`, `SLICE`):**
   - High-level parametric gesture descriptions are sent to Unity, where `UrdtInputInjector.cs` evaluates the Flash-Hogan polynomial natively at the full display framerate (60–120 Hz), eliminating network jitter artifacts.
2. **Behavior of L1 During Micro-SLM Inference (40–150 ms):**
   - When L2 dispatches a query to the Micro-SLM, it specifies an operational fast-loop policy (`on_deliberation_policy`):
     - `SAFE_COAST`: Smoothly completes the active kinematic gesture and enters a neutral hold posture, preventing character runaway.
     - `SAFE_HOLD`: Retains the current virtual stick deflection or throttle hold.
     - `SAFE_RELEASE`: Immediately releases all pointers to prevent unintended clicks.
     - `PAUSE_TIMESCALE`: Controlled world freeze (`urdt_pause_world`) via `UrdtTimeScaleController` (permitted only in deterministic test fixtures, not in realtime CI).
     - `ASYNC_BUFFERING`: Maintains safe neutral posture while gameplay simulation continues.

### 2.6. Controlled World Pause Tool During Reasoning (`UrdtTimeScaleController`)
When complex multi-step reasoning is required from L3 or the Micro-SLM, the agent can safely pause gameplay physics and animations via the `IUrdtPausable` contract:
1. **Handling Unscaled Time:** When paused, `UrdtTimeScaleController` records virtual offset `unscaledPauseOffset`. Game timers reading unscaled time receive the adjusted value via `UrdtTime.UnscaledTime`.
2. **Tween Suppression (DOTween / LeanTween):** Active tweens are paused via `DOTween.PauseAll()` (or selectively via `DOTween.Pause(UrdtTarget)`).
3. **UniTask Timers:** Delay tokens receive `UrdtEvents.OnUrdtPauseStateChanged(isPaused: true)`.
4. **Network Thread Isolation:** URDT communication threads (Named Pipe / WebSocket) operate on independent monotonic system threads (`process.hrtime` / `Stopwatch`), remaining fully responsive during engine world freezes.

### 2.7. Dual-Mode Synthetic Input Injection Mechanism (`UrdtInputInjector.cs`)
1. **New Input System Mode (Primary Standard):**
   - Resolves or instantiates an authentic virtual touchscreen:
     ```csharp
     var touchDevice = InputSystem.GetDevice<Touchscreen>() ?? InputSystem.AddDevice<Touchscreen>("UrdtVirtualTouchscreen");
     ```
   - Injects low-level state events using physical screen pixels:
     ```csharp
     InputSystem.QueueStateEvent(touchDevice, new TouchState {
         touchId = pointerId + 1,
         phase = currentPhase, // Began, Moved, Stationary, Ended
         position = physicalPixelPos,
         pressure = 1.0f
     });
     ```
   - Seamlessly activates `Touchscreen.current`, `ActionMaps`, `OnScreenStick`, and `EnhancedTouch`.
2. **Legacy / UGUI Fallback Mode:**
   - Dispatches `PointerEventData` via `ExecuteEvents.ExecuteHierarchy` for Canvas UI.
   - Dispatches synthetic `Physics.Raycast` / `Physics2D.Raycast` for 2D/3D colliders.
3. **Stationary Hold Emulation Throttling:** Throttles `TouchPhase.Stationary` events to 10 Hz during sustained holds to prevent event queue saturation.
4. **Physics Step Synchronization (`FixedUpdate`):** Synchronizes physical displacements with `Time.fixedDeltaTime` (50 Hz), eliminating collision tunneling.
5. **Raycast Blocker Detection (`BLOCKED_BY_UI_OVERLAY`):** Pre-queries `EventSystem.current.RaycastAll`. If the topmost graphic blocker does not match the target beacon, dispatches diagnostic event `BLOCKED_BY_UI_OVERLAY`.
6. **Stationary Settle for Snap-to-Slot ($T_{\text{settle}} = 100\text{ ms}$):** Holds drag position for 5 physics ticks over the slot receptacle, allowing physical damping to bleed off kinetic energy before issuing `pointer_up`.
7. **Sub-Frame Precision In-Engine QTE Trigger:** Evaluates declarative conditions (`SCHEDULE_PREDICATE_TAP`) frame-by-frame inside C# `Update()` / `FixedUpdate()`, executing zero-latency parries and QTE taps on the exact required frame.
8. **Floating Dynamic Joysticks:** Supports `isFloating: true` with dynamic anchor positioning upon `TouchPhase.Began` inside `activationAreaRect`.

---

## 3. Layer L2: Tactical Dispatcher & Cognitive Arbiter

### 3.1. Tactical State Data Model (`L2TacticalState`)
```typescript
interface L2TacticalState {
  currentMissionGoal: string;             // Macro-goal from L3 ("Complete 3-lap race")
  activeSubGoal: string;                  // Sub-task ("Hold throttle and navigate turn #2")
  
  activeTouchChannels: Map<number, {
    pointerId: number;
    currentPrimitive: string;
    targetElementId: string;
    startedAtTimestamp: number;
  }>;
  
  detectedHazards: Array<{
    hazardId: string;
    position: { x: number; y: number; z?: number };
    timeToImpactMs: number;
    dangerRadius: number;
    threatLevel: 'LETHAL' | 'MODERATE' | 'INFO';
  }>;
  
  activeModalWindow: string | null;       // Active popup name or null
  trackedInteractiveElements: Map<string, ElementSnapshot>;
  expectedTransitions: Map<string, string>;
  
  stuckCounter: number;                   // 0..3 attempts
  subGoalStartedAt: number;
  subGoalTimeoutMs: number;
  lastStateChangeTimestamp: number;
  
  recentActionHistory: Array<{
    stateHash: string;
    targetId: string;
    outcomeDelta: number;
  }>;
}
```

### 3.2. Dual-Loop Tactical Architecture
Layer L2 operates through two strictly differentiated computational paths:

```
                        [ INPUT FROM L3: Sub-Goal Objective ]
                                      │
                                      ▼
                        [ RECEIVE URDT STATE SNAPSHOT ]
                                      │
                      Is there a blocking modal window?
                     ┌────────────────┴────────────────┐
                 YES │                                 │ NO
                     ▼                                 ▼
          Window known in graph?             State progressed
          ┌──────────┴──────────┐            since last action?
      YES │                   NO│            ┌─────────┴─────────┐
          ▼                     ▼        YES │                 NO│
    [ AUTOPILOT ]      [ COGNITIVE ARBITER ] ▼                   ▼
    Click known        Query Micro-SLM       Reset stuck       stuckCounter++
    button from graph  (Qwen-1.5B, 50 ms)    counter           stuckCounter > 3?
    (0.01 ms overhead)          │            Select primitive  ┌──────┴──────┐
          │                     │            for L1 goal   YES │           NO│
          │                     │            (TAP / DRAG)      ▼             ▼
          └──────────┬──────────┘                  │     [ ESCALATE TO L3 ] Retry with
                     │                             │     Generate Failure   alternative
                     ▼                             ▼     Evidence Packet    trajectory
             [ DISPATCH L1 PRIMITIVE VIA WEBSOCKET ]
```

1. **Autopilot Fast Loop (0.01 ms Overhead):** In nominal situations (target slot empty, item ready, path known), L2 translates HTN sub-tasks directly into L1 motor primitives without invoking neural networks.
2. **Cognitive Arbiter Loop (40–60 ms Latency):** Invoked exclusively upon encountering anomalies: unknown modal popups, unexpected projectiles, or stagnation detector triggers.

### 3.3. Two-Tier Tactical Executive (HTN-over-BT)
```
 ┌───────────────────────────────────────────────────────────────────────────┐
 │               UPPER LEVEL: HTN PLAN GENERATOR (Macro-Plan)                │
 │   Decomposes GDD specs & scene beacons into ordered subtasks:             │
 │   [NavigateToShop] ──► [SelectTab:Weapons] ──► [TapItem:Sword] ──► [Buy] │
 └─────────────────────────────────────┬─────────────────────────────────────┘
                                       │ Active plan step (SubGoal)
                                       ▼
 ┌───────────────────────────────────────────────────────────────────────────┐
 │             LOWER LEVEL: REACTION BEHAVIOR TREE (50 ms Ticker)            │
 │   Execution Priority Selector:                                            │
 │   ├─ Priority 1 (Critical): Threat / Damage detected ──► Evade / Block    │
 │   ├─ Priority 2 (Interrupt): Modal window popup ──► Dismiss / Respond     │
 │   └─ Priority 3 (Default): Execute current HTN step via L1 primitive      │
 └─────────────────────────────────────┬─────────────────────────────────────┘
                                       │ On step failure / obstruction
                                       ▼
                        [ FEEDBACK: REPLAN_REQUIRED ]
                  (HTN dynamically replans around the obstacle)
```

- **Loop-Break Invariant:** If 2 consecutive plans cycle through identical nodes without progress ($\Delta \text{Progress} = 0$), or re-planning attempts exceed 3 for a single sub-goal, L2 aborts and raises a `DEADLOCK_TOPOLOGY` defect.

### 3.4. Dynamic Semantic Check-Up (`UrdtStagnationPolicy`)
- Stagnation thresholds are configured dynamically per genre context:
  - *Arcade / Runner:* $\text{velocity} < 0.1\text{ m/s}$ for $0.5\text{ s} \implies$ Stagnation.
  - *Puzzle / Match-3:* Up to $15\text{ s}$ of inaction is considered normal deliberation.
  - *UI / Dialog:* Stagnation declared upon zero state delta following an active click.
- **Continuous Noise Filtering:** Micro-oscillations from idle animations ($\le 0.001\text{ m}$) and particles are filtered out of state tree comparison hashes ($\Delta \text{Tree}$).

### 3.5. 5-Layer Composite Modal & Delay Policy
Every 1000 ms, L2 evaluates screen state across 5 filters to eliminate false-positive soft-locks:
1. **Layer 1: GDD Contract:** Enforces declared mandatory durations (e.g. 15-second reward video).
2. **Layer 2: Contractual Beacons (`UrdtModalTarget`):** Inspects `minDisplayDurationMs` and `isCloseAllowed`.
3. **Layer 3: Active Scene Engine State:** Pauses stagnation timers while `VideoPlayer.isPlaying` or non-looping animation sequences with `locksInteraction == true` are running.
4. **Layer 4: Countdown Regex Scanner:** Detects decreasing numerical strings `/\b(0?[0-9]|1[0-5])\s*(s|sec)?\b/i` (5..4..3..2..1) as `LEGITIMATE_COUNTDOWN`.
5. **Layer 5: 4.0s Grace Period:** Grants every newly spawned dialog an unconditional 4.0s window before evaluating soft-lock conditions.

### 3.6. Triad World & Threat Perception
1. **GDD Specifications:** Ingests expected threat spawn rules (e.g. *"Meteors spawn every 5s"*).
2. **Explicit Engine Beacons (`UrdtHazardTarget`):** Provides precise danger radii and `timeToImpactMs`.
3. **Dynamic Physics Detector:** Scans colliders within 10 meters. If an entity carries a `Hazard/Damage` tag or its velocity vector $\vec{v}$ predicts impact with the player in $< 1.0\text{ s}$, it is flagged as a lethal threat, triggering an immediate BT evasive maneuver.

### 3.7. Micro-SLM Cognitive Arbiter Implementation
Powered by `qwen2.5-1.5b-instruct-q4_k_m.gguf` via `node-llama-cpp`:
```text
SYSTEM: You are a tactical game control module. Your objective is to return gameplay to its goal.
GOAL: "Place blue cube into slot"
CURRENT SCREEN:
- Active window: "SpecialOfferModal" (Blocks screen: true)
- On-screen buttons:
  * id: "btn_buy_now", text: "Buy for $0.99"
  * id: "btn_close", text: "X" (top-right corner)

Respond strictly in JSON:
{
  "diagnosis": "Special offer popup blocks progression",
  "action": "TAP",
  "targetId": "btn_close"
}
```

- **3-Echelon Hallucination Defense:**
  1. *GBNF Grammar:* Constrains token sampling strictly to valid on-screen button IDs.
  2. *Zod Validation:* Validates JSON schema adherence with one bounded retry.
  3. *Deterministic Fallback:* Defaults to prioritized dismissal (`btn_close`, `Cancel`, `Back`) upon model timeout.
- **3-Stage Anti-Looping Protection:** Action history ring buffer + prompt exclusion (`forbidden_targets`) + exploratory micro-jitter.
- **Natural Game Over & Restart Loop:** Upon `OnPlayerDied`, L2 scans the UI hierarchy for `Retry` / `Restart` buttons and restores the testing loop authentically.

---

## 4. Layer L3: GDD Compliance & Invariant Verification in Runtime

### 4.1. Three-Level GDD Compliance Model
Requirements from local specifications (`Docs/Specs/*.md`) or the central knowledge base (`KBPRO_AI_CHAT_WIKI_DIR`) are decomposed into three tiers:
1. **Macro-Level (DoD Checklist):** Major milestone acceptance criteria (e.g. *"Complete 3 laps within 60s"*).
2. **Meso-Level (Contractual Invariants):** Formal state invariants:
   - `INV-01 (Happy Path)`: Dragging cube to matching slot $\implies$ `isOccupied == true` and `progress += 1`.
   - `INV-02 (Exploit / Negative)`: Dragging defective junk part $\implies$ slot rejects item, `isOccupied == false`.
3. **Micro-Level (Operational Hypotheses on the Fly):** Dynamically formulated assertions executed immediately prior to entering a mechanic.

### 4.2. LangGraph Execution Engine in Mode 2
- **`ExperimentPlannerNode`:** Synthesizes the test execution matrix: balances nominal playthroughs with deliberate exploit testing (dragging items out of bounds, spamming inputs, rapid toggling).
- **`AuditReportNode`:** Aggregates invariant test traces into `Docs/QA_Audit_Report.html`.

---

## 5. Layer L4: Save-State & Rollback Engine

To enable destructive or irreversible game path testing without permanently corrupting save files:

```
    [ Game state prior to test branch ]
                   │
                   ▼ (URDT: create_save_state)
    [ Serialized snapshot: "checkpoint_before_shop" ]
                   │
                   ▼
    [ Agent executes arbitrary exploratory actions ]
    (Purchasing shop items, spending gold, deleting profile,
     stress-testing save corruption / reset logic)
                   │
                   ▼
    Test concluded / Save state mutated?
    (URDT: rollback_save_state("checkpoint_before_shop"))
                   │
                   ▼
    [ Game world restored to initial state via 4-phase protocol ]
```

### 5.1. The `IUrdtSaveable` Contract
```csharp
public interface IUrdtSaveable
{
    string SaveStateKey { get; }
    byte[] CaptureState();
    void RestoreState(byte[] stateData);
    void ResetToDefault();
}
```

### 5.2. Four-Phase Clean Rollback Protocol
1. **Phase 1: Runtime Force-Purge:**
   - Kills active coroutines across all `IUrdtCoroutinesHolder` instances.
   - Force-kills tweens without triggering completion callbacks: `DOTween.KillAll(complete: false)`.
   - Cancels UniTask tokens via `CancellationTokenSource`.
   - Unloads dynamically loaded Addressables additive scenes.
2. **Phase 2: Atomic Disk Data Restoration:**
   - Overwrites data within isolated sandbox directory `Application.persistentDataPath + "/URDT_Sandbox/"` via a write-ahead swap buffer.
   - Resets and flushes `PlayerPrefs.Save()`.
3. **Phase 3: Memory Restoration for Singletons & DI Containers:**
   - Invokes `RestoreState()` on all registered `IUrdtSaveable` entities.
   - Reconstructs child DI scopes (Zenject / VContainer sub-containers).
4. **Phase 4: Adapter-Dependent Synchronization or Soft Scene Reload:**
   - Localized rollback completed in sub-50ms benchmark; fallback to `SceneManager.LoadSceneAsync` upon deep graph corruption.

---

## 6. Closed-Loop AI Game Engineering & Failure Evidence

When an invariant is violated, an unhandled exception occurs, or stagnation triggers `stuckCounter >= 3`:

### 6.1. In-Memory Crash Dashcam (`UrdtCrashDashcam.cs`)
- Instead of continuous heavy video recording, a C# RAM ring buffer records 25 lightweight JPEG frames (640x360 resolution, 60% quality, ~60 KB/frame) at 5 FPS ($T = 200\text{ ms}$), maintaining a rolling 5-second history.
- RAM footprint is merely **~1.5 MB**, with frames captured via non-blocking `AsyncGPUReadback`.
- Upon failure, the buffer compiles into an animated WebP and embeds directly as `data:image/webp;base64,...` into the failure evidence packet.

### 6.2. Failure Evidence Packet Schema (`UrdtFailureEvidencePacket`)
The failure packet is exported through a **Dual Export Protocol**:
1. **Machine Loop (`.harness/failure_evidence.json`):** Ingested by external AI coding agents (OpenAI Codex, Google Antigravity, `cli-judge.js`) to autonomously identify root causes in C# scripts and apply gated patches.
2. **Human Loop (`Docs/QA_Audit_Report.html`):** Interactive defect cards with animated dashcam playback, full beacon telemetry, and chronological action traces.

```json
{
  "incidentId": "INC_20260922_001",
  "timestamp": 1726912345678,
  "failingPhase": "M01_SnapToSlot",
  "gddInvariantViolated": "INV-01_SNAP_SUCCESS",
  "stagnationType": "MICRO_STUCK",
  "lastKnownWorldRevision": 42,
  "activeScene": "URDT_2D_TestPolygon",
  "beaconStateSnapshot": [
    {
      "id": "item_cube_blue",
      "screenPixel": { "x": 450.0, "y": 310.0 },
      "velocity": [0.0, 0.0, 0.0],
      "flags": { "isInteractable": true, "isSnapped": false }
    }
  ],
  "actionHistoryBeforeFailure": [
    { "action": "DRAG", "targetId": "item_cube_blue", "durationMs": 350 }
  ],
  "diagnostics": {
    "engineConsoleErrors": ["NullReferenceException: SnapZoneController.OnTriggerEnter2D"],
    "activeModalDialog": null,
    "crashDashcamBase64": "data:image/webp;base64,UklGR...",
    "recommendedAiFix": "Check SnapZoneController.cs line 42 for unassigned Collider2D reference."
  }
}
```
