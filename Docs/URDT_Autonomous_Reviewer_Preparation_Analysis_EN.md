# Architecture of Autonomous URDT AI Reviewer: Preparation & Analysis Subsystem
*(Mode 1: Pre-Flight Inspection, Scaffolding, Topological Discovery & Health Audit)*

---

## Document Context and System Relationships

This document is the normative architectural specification for **Mode 1 (Preparation and Analysis)** of the URDT Autonomous Reviewer. It forms part of the three-pillar architectural documentation suite:

1. **[Core Architecture & System Foundations (Master Overview)](URDT_Autonomous_Reviewer_Architecture_EN.md)** — Core mission, speed pyramid, Hexagonal architecture, Wire Protocol v2, dual transports, process isolation, and CI/CD lifecycle.
2. **[Preparation & Analysis Subsystem (This Document)](URDT_Autonomous_Reviewer_Preparation_Analysis_EN.md)** — Project readiness audit, beacon auto-instrumentation, control scheme discovery, topological mapping (Runtime Graphify), layout auditing, and dead-end detection.
3. **[Game & Testing Subsystem](URDT_Autonomous_Reviewer_Game_Testing_EN.md)** — Live gameplay execution, multi-touch biomanipulation (L1), tactical dispatching (L2), Micro-SLM cognitive arbiter, GDD invariant validation (L3), and save-state rollback engine (L4).

---

## 1. Mission and Conceptual Scope of Preparation & Analysis Mode

### 1.1. Core Objectives
Before an autonomous agent can safely or productively execute gameplay mechanics, it must inspect, scaffold, and map the target Unity project. **Mode 1 (Preparation & Analysis)** operates as an intelligent surveyor, auditor, and cartographer that:

1. **Conducts Pre-Flight Compatibility Audits:** Verifies that the Unity project meets all foundational runtime prerequisites (assembly definitions, packages, driver bindings, render pipeline sanity, input subsystems).
2. **Instruments the Scene Hierarchy (Scaffolding):** Deploys non-invasive semantic beacon proxies onto standard UI elements and interactables via `UrdtAutoInstrumentation.cs`, enforcing the contractual Definition of Done (DoD) for bespoke mechanics.
3. **Discovers Control Schemes (Triad Discovery):** Derives how the game is controlled by cross-referencing GDD documentation, scanning on-screen visual affordances, and executing physical micro-probing pulses.
4. **Constructs the Observed Multi-Layer Topological Multigraph (Runtime Graphify):** Systematically explores contract-visible scenes, windows, screens, transitions, and spatial zones within a declared exploration budget, synthesizing $\mathcal{G} = (V, E)$.
5. **Executes Multi-Tier Structural & Asset Defect Audits:** Detects soft-locks, unhandled exceptions, text truncation, broken font glyphs, bounds overflows, CDN stalls, and orphan screens without requiring manual gameplay intervention.
6. **Produces the Versioned Application Map Artifact:** Exports `application_map.json` and renders an interactive topological map inside `Docs/QA_Audit_Report.html` as the verified baseline for Mode 2 testing.

```
                      PREPARATION & ANALYSIS PIPELINE (MODE 1)
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 1. PRE-FLIGHT COMPATIBILITY AUDIT:                                          │
   │    Verify URDT Driver, Input System, Graphics Raycaster, IPC Sockets        │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Environment Verified (Exit Code 0)
                                          ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 2. BEACON SCAFFOLDING & AUTO-INSTRUMENTATION:                               │
   │    Inject UrdtUiTarget onto Buttons/Sliders/Toggles; Validate Custom Beacons│
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Scaffolding Deployed
                                          ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 3. TRIAD CONTROL SCHEME DISCOVERY:                                          │
   │    Correlate GDD priors + on-screen tokens + active 100 ms micro-probing    │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │ Control Mapping Registered
                                          ▼
   ┌─────────────────────────────────────────────────────────────────────────────┐
   │ 4. TOPOLOGICAL APPLICATION MAPPING (RUNTIME GRAPHIFY):                      │
   │    Frontier exploration (K=3) -> Construct multigraph G = (V, E)           │
   │    Filter continuous coordinates; Mask dynamic digits (<NUM>)               │
   └──────────────────────────────────────┬──────────────────────────────────────┘
                                          │
                  ┌───────────────────────┴───────────────────────┐
                  │                                               │
            [ DEFECTS FOUND ]                           [ PREPARATION COMPLETE ]
                  │                                               │
                  ▼                                               ▼
   ┌───────────────────────────────┐               ┌───────────────────────────────┐
   │ STRUCTURAL DEFECT REPORT:     │               │ APPLICATION MAP READY:        │
   │ - Soft-Locks / Dead Ends      │               │ - application_map.json        │
   │ - Layout / Truncation Bugs    │               │ - Verified Navigation Graph   │
   │ - Asset Streaming Stalls      │               │ - Transition to Mode 2        │
   │ - Engine Exceptions           │               │   (READY_FOR_TESTING)         │
   └───────────────────────────────┘               └───────────────────────────────┘
```

### 1.2. Mode Transition Contract
- **Contract Guarantee:** Mode 2 (Game / Testing) must never execute against an unmapped or unverified state. If Mode 2 encounters an uncataloged screen or unrecognized mechanic during gameplay, it immediately emits `DISCOVERY_REQUIRED` and re-engages Mode 1.
- **Exploratory Status of Evidence:** All transitions and clicks executed in Mode 1 are classified strictly as *exploratory evidence*. Observations gathered during Mode 1 exploration must not be reported as validation of gameplay invariants.
- **Readiness Verdict:** Preparation Mode terminates with one of three formal statuses:
  - `READY_FOR_TESTING`: Complete reachable UI/mechanic graph mapped; zero critical soft-locks or blockers.
  - `DEFECTS_BLOCKING`: Critical blockers discovered (onboarding modal freeze, unhandled exception, missing close button) preventing transition to gameplay.
  - `INCOMPLETE_BUDGET_EXHAUSTED`: Frontier queue remaining when the allocated exploration time/step budget elapsed; partial map exported with explicit coverage gaps identified.

---

## 2. Environment Readiness & Tooling Compatibility Verification

Prior to launching any automated exploration, the `CoreAgent` orchestrator runs a comprehensive pre-flight verification matrix against the target Unity project:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                    PRE-FLIGHT COMPATIBILITY AUDIT MATRIX                     │
├──────────────────────────┬───────────────────────────────────────────────────┤
│ Verification Target      │ Inspection Method & Success Criteria              │
├──────────────────────────┼───────────────────────────────────────────────────┤
│ **URDT Package Binding** │ `com.davasko.urdt` present in `manifest.json`.     │
│                          │ Assembly reference `KBP.URDT` linked in asmdef.   │
├──────────────────────────┼───────────────────────────────────────────────────┤
│ **Input System Mode**    │ Inspect `PlayerSettings.activeInputHandler`:      │
│                          │ - New Input System: `Touchscreen.current` valid.  │
│                          │ - Legacy: `EventSystem.current` & raycasters open.│
├──────────────────────────┼───────────────────────────────────────────────────┤
│ **Rendering Pipeline**   │ Confirm rendering is ACTIVE (no `-nographics`).   │
│                          │ `GraphicRaycaster` operational on root Canvases.  │
│                          │ `CanvasScaler` resolves target reference aspect.  │
├──────────────────────────┼───────────────────────────────────────────────────┤
│ **IPC & Network Ports**  │ Windows Named Pipe / Unix Socket accessible.      │
│                          │ WebSocket loopback port bound (ephemeral or 9002). │
│                          │ Session token validated (`4401` rejection tested).│
├──────────────────────────┼───────────────────────────────────────────────────┤
│ **IL2CPP Preservation**  │ `link.xml` contains mandatory preserve rules for   │
│                          │ `KBP.URDT`, `UnityEngine.UI`, `InputSystem`.      │
└──────────────────────────┴───────────────────────────────────────────────────┘
```

### 2.1. The Graphical Rendering Requirement (Preventing the `-nographics` Dilemma)
In automated CI runners, executing Unity with the command-line flag `-nographics` causes immediate failure of the reviewer pipeline:
- In `-nographics` mode, Unity bypasses `Canvas.willRenderCanvases`, `CanvasScaler` fails to compute screen scaling matrices, and `GraphicRaycaster.Raycast` returns empty hit collections.
- The agent becomes completely blind to UI interactive elements.
- **Enforced Standard:** In headless environments (Linux Docker), Unity must be executed with virtual displays (`xvfb-run -s "-screen 0 1920x1080x24"`) and software Vulkan/OpenGL acceleration (Google SwiftShader / Mesa llvmpipe), ensuring complete UI layout computation.

---

## 3. Auto-Instrumentation & Beacon Scaffolding Tooling

The URDT system features a first-party scaffolding assistant: **`UrdtAutoInstrumentation.cs`**. It ensures that standard interfaces are automatically queryable without manual developer effort.

```
       PROJECT HIERARCHY
      ┌─────────────────┐
      │ Canvas / Screen │
      └────────┬────────┘
               │
       Auto-Scan Hierarchy
               ▼
 ┌──────────────────────────┐     Inject Dynamic
 │ UnityEngine.UI.Button    ├───────────────────► [ UrdtUiTarget: Button ]
 ├──────────────────────────┤     Proxy Beacons
 │ UnityEngine.UI.Slider    ├───────────────────► [ UrdtUiSliderTarget ]
 ├──────────────────────────┤
 │ UnityEngine.UI.Toggle    ├───────────────────► [ UrdtUiTarget: Toggle ]
 ├──────────────────────────┤
 │ TextMeshProUGUI / Text   ├───────────────────► [ Semantic Vocabulary Map ]
 ├──────────────────────────┤
 │ Collider2D + IDragHandler├───────────────────► [ Urdt2DDraggableProxy ]
 └──────────────────────────┘
```

### 3.1. Baseline UI Auto-Detection
Upon scene entry or window activation, `UrdtAutoInstrumentation.cs` traverses active `GameObject` hierarchies:
1. **Selectable Controls:** All components inheriting from `UnityEngine.UI.Selectable` (`Button`, `Toggle`, `Slider`, `Dropdown`, `InputField`) receive a lightweight `UrdtUiTarget` proxy beacon if an explicit beacon is not already attached.
2. **Text Semantic Grounding:** Text components (`TextMeshProUGUI` and standard `Text`) are extracted, cleaned of markup tags (`<color>`, `<b>`), normalized, and bound to their parent selectable container to provide human-readable labels for the L2/L3 reasoning engines.
3. **Scrollable Viewports:** `ScrollRect` containers are marked with `UrdtScrollAreaTarget` to support directional swiping and viewport clipping boundaries.

### 3.2. Definition of Done (DoD) Rule for Custom Gameplay Beacons
Auto-instrumentation is intentionally constrained to standard UI and baseline 2D physics interactions:
- **Contract Mandate:** Complex gameplay mechanics (custom physics dragging, trajectory slingshots, snapping receptacles, dynamic hazard zones, damage fields) cannot be inferred reliably from raw colliders alone.
- **DoD Requirement:** Every custom mechanic implemented by a human developer or an AI coding agent must explicitly declare contractual URDT beacons (`Urdt2DModuleTarget`, `Urdt2DSlotTarget`, `UrdtHazardTarget`, `UrdtWaypointTarget`, `UrdtSaveStateTarget`).
- **Linter Gate:** If an active scene contains mechanics scripts without matching URDT beacons, Mode 1 logs a warning in `QA_Audit_Report.html`: `MISSING_CUSTOM_BEACON_DECLARATION`.

---

## 4. Triad Control Scheme Discovery

Mode 1 autonomously determines how to interact with the game through a **Triad Source Model**, preventing fragile hardcoded coordinate assumptions:

```
        ┌─────────────────────────────────────────────────────────┐
        │ 1. GDD PRIOR (Reading Controls section in Design Doc)   │
        │    "Left virtual stick steers, tap right pedal for gas" │
        └────────────────────────────┬────────────────────────────┘
                                     │ Hypothesize H0
                                     ▼
        ┌─────────────────────────────────────────────────────────┐
        │ 2. SEMANTIC ON-SCREEN ANALYSIS (Text, icons, beacons)   │
        │    Locate UrdtUiStickTarget (Left), Button "GAS" (Right)│
        └────────────────────────────┬────────────────────────────┘
                                     │ Correlate with H0
                                     ▼
        ┌─────────────────────────────────────────────────────────┐
        │ 3. EMPIRICAL MICRO-PROBING (Micro-Probe Execution)      │
        │    Issue 100 ms touch pulse -> Measure physics delta:   │
        │    velocity.magnitude increased? -> MAPPING CONFIRMED!  │
        └────────────────────────────┬────────────────────────────┘
                                     │
                 ┌───────────────────┴───────────────────┐
                 │ Delta verified?                       │
                 ▼                                       ▼
        [ REGISTER IN MAP ]                   [ DEFECT ESCALATION ]
        Bind actions to schema:               "Control 'GAS' unresponsive:
        Throttle: pointerId 1                 Physics delta = 0 under 100 ms pulse.
        Steering: pointerId 0                 Violation of GDD Section 2.1"
```

1. **Step 1: GDD Prior:** Layer L3 extracts control specifications from local design docs (`Docs/Specs/*.md`) or the knowledge base (`KBPRO_AI_CHAT_WIKI_DIR`).
2. **Step 2: Semantic Correlation:** The analyzer scans active beacons and text strings to bind semantic intents (`STEER`, `THROTTLE`, `JUMP`, `FIRE`) to candidate UI targets (`UrdtUiStickTarget`, `btn_jump`, `icon_pedal`).
3. **Step 3: Empirical Micro-Probing:** Dispatches an isolated, low-intensity motor primitive (e.g. 100 ms micro-tap or minor stick deflection) and observes the telemetry stream for expected kinematic deltas ($\Delta v > 0$, state flag change).
   - If confirmed: Registered in `application_map.json` under `controlBindings`.
   - If zero effect observed after testing alternate candidates: Logged as a control discrepancy.

---

## 5. Topological Application Mapping (Runtime Graphify)

### 5.1. Multi-Layer Topological Multigraph $\mathcal{G} = (V, E)$
During autonomous exploration, Mode 1 dynamically synthesizes a directed multigraph representing the reachable application space:

```
                 MULTI-LAYER TOPOLOGICAL APPLICATION MAP (4 LAYERS)
 ┌─────────────────────────────────────────────────────────────────────────────┐
 │ LAYER 1: UI Navigation Graph (Screens, Menus, Modals, Dialogs)              │
 │ [MainMenu] ──(click:btn_play)──► [SelectLevel] ──(click:lvl_01)──► [GameUI] │
 ├─────────────────────────────────────────────────────────────────────────────┤
 │ LAYER 2: Data & Progression Graph (Economy, Score, Inventory, Currencies)   │
 │ {Score: 0, Ore: 0} ──(mine_node)──► {Score: 100, Ore: 5, LevelUnlocked: 2} │
 ├─────────────────────────────────────────────────────────────────────────────┤
 │ LAYER 3: Spatial World Graph (2D/3D Space, Rooms, Arenas, Zones)            │
 │ [SpawnPoint] ──(navigate:corridor_A)──► [BossArena] ──(exit)──► [SafeZone]  │
 ├─────────────────────────────────────────────────────────────────────────────┤
 │ LAYER 4: Element Affordance Graph (Beacons & permitted actions at timestamp)│
 │ [Urdt2DDraggable] ──(can_drag_to)──► [Urdt2DSlot] (Status: IsOccupied=false)│
 └─────────────────────────────────────────────────────────────────────────────┘
```

### 5.2. Formal Data Structures for Multigraph ($V$ and $E$)

#### Semantic State Node Structure ($V$):
```typescript
interface UrdtGraphNode {
  nodeId: string;              // 64-bit MurmurHash3 of normalized discrete state descriptor
  screenType: 'MAIN_MENU' | 'LEVEL_SELECT' | 'GAMEPLAY_ARENA' | 'MODAL_DIALOG' | 'INVENTORY' | 'VICTORY' | 'DEFEAT';
  sceneName: string;           // Composite active Unity scene string
  activeModalId: string | null;// Blocking modal window name (if present)
  
  stateDescriptor: {
    interactiveBeaconIds: string[]; // Alphabetically sorted array of active beacon IDs
    progressionPhase: string;       // "TUTORIAL_STEP_1", "WAVE_3", "BOSS_FIGHT"
    questFlagsHash: number;         // Bitmask of critical discrete gameplay flags
    normalizedTexts: string[];      // UI strings with dynamic numbers (\d+) replaced with <NUM>
  };
  
  firstDiscoveredTimestamp: number;
  visitCount: number;
  isTerminal: boolean;         // End-of-game screen / Victory / Defeat
}
```

#### Affordance Transition Edge Structure ($E$):
```typescript
interface UrdtGraphEdge {
  edgeId: string;
  fromNodeId: string;
  toNodeId: string;
  actionPrimitive: 'TAP' | 'DRAG' | 'SWIPE' | 'CONTINUOUS_STEER' | 'TIMEOUT_WAIT';
  targetBeaconId: string;
  
  kinematicParams: {
    screenNormalizedStart: [number, number];
    screenNormalizedEnd?: [number, number];
    durationMs: number;
  };
  
  transitionLatencyMs: number; // Latency between input completion and node transition
  traversalCount: number;      // Number of times transition executed successfully
  successRate: number;         // Transition success ratio (0.0 .. 1.0)
}
```

### 5.3. State Explosion Protection in Topological Graph
To prevent the graph from expanding into tens of thousands of redundant nodes due to fluctuating timers, currency counters, or looping animations, `computeNodeHash(state)` enforces rigorous normalization:

1. **Regex Digit Masking:** All numerical tokens in button labels and text fields are replaced via `/\b\d+([.,]\d+)?\b/g` with `<NUM>`. Values *"Gold: 154"* and *"Gold: 210"* collapse into a single semantic invariant `Gold: <NUM>`.
2. **Alphabetical Beacon Sorting:** The `interactiveBeaconIds` array is sorted alphabetically (`ids.sort()`), eliminating dependencies on arbitrary `GetComponentsInChildren` traversal order.
3. **ScrollRect Viewport Culling:** UI elements positioned outside the viewport mask (`!viewportRect.Contains(elementScreenPos)`) are excluded from the node descriptor until scrolled into view.
4. **Decoupling Continuous Coordinates:** Floating-point coordinates of world entities are strictly excluded from node hash $V$. A node encodes spatial context only when marked with a discrete `UrdtSpatialZone` component.
5. **Composite Multi-Scene Scope:** During additive scene loading (`LoadSceneMode.Additive` or Addressables), scene identifier `sceneName` is assembled as an alphabetically sorted composite string: `MainScene + [HUDOverlay, PauseDialog]`.

### 5.4. Genre-Adaptive Procedural Quantization
- **Match-3 & Tile Puzzles:** Individual gem coordinates and clone IDs (`gem_red_4182(Clone)`) are excluded from node hash $V$. The vertex descriptor captures only level objectives (`Target: Apples, Remaining: <NUM>`), valid move status (`hasValidMoves: boolean`), and phase flag (`GAMEPLAY_MATCH3`). All gem swap interactions map as compact loopback edges within a single `[Match3_ActiveBoard]` node.
- **Endless Runners:** Monotonically increasing $Z$ coordinates and procedural obstacle prefabs are decoupled from graph vertices. Vertex $V$ tracks a discrete finite-state automaton for lanes (**Lane-State**: `LANE_LEFT` | `LANE_CENTER` | `LANE_RIGHT`, `RUNNING` | `JUMPING` | `SLIDING`) and macro-biome zones, collapsing a multi-kilometer run into a clean transition matrix.
- **Roguelikes & Procedural Dungeons:** Graph vertices are generated strictly at room boundaries upon traversing doors or portals. Vertices are keyed by meta-archetype (`RoomArchetype: START | COMBAT | SHOP | TREASURE | BOSS`) and completion status (`RoomStatus: IN_COMBAT` $\to$ `ROOM_CLEARED`). In-room dynamic combat does not spawn extra nodes.

---

## 6. Comprehensive 4-Tier Defect Detection Algorithm

During exploration, Mode 1 executes continuous defect screening across four independent verification tiers:

```
┌──────────────────────────────────────────────────────────────────────────────┐
│                  4-TIER DEFECT DETECTION AUDIT ARCHITECTURE                  │
├──────────────────────────────────────────────────────────────────────────────┤
│ Tier 1: Frontier Exhaustion & Engine Health                                  │
│ - Probe each affordance K=3 times (center, offset, hold)                     │
│ - Flag Deadlock Nodes (no outbound edges; missing Close/Back buttons)        │
│ - Catch NullReferenceExceptions, Spine freezes, Native OS Crashes (SIGSEGV)  │
├──────────────────────────────────────────────────────────────────────────────┤
│ Tier 2: Strict GDD Funnel Compliance                                         │
│ - Verify mandatory onboarding sequences and tutorials                        │
│ - Flag Interface Routing Discrepancies (e.g. Shop button opens Settings)     │
├──────────────────────────────────────────────────────────────────────────────┤
│ Tier 3: Automated Layout & Localization Audit (UrdtLayoutInspector)          │
│ - Text Truncation: isTextOverflowing / isTextTruncated clipped text          │
│ - Bounds Overflow: RectTransform exceeds parent container boundaries        │
│ - Font Degeneration: enableAutoSizing shrinks text to fontSize < 10.0pt      │
│ - Missing Glyphs: Unresolved characters in font atlas (fallback '\uFFFD')    │
├──────────────────────────────────────────────────────────────────────────────┤
│ Tier 4: Async Asset Streaming & CDN Stall Monitor (UrdtAsyncLoadingInspector)│
│ - Intercept active Addressables handles & UnityWebRequest downloads          │
│ - Distinguish legitimate slow downloads from deadlocks via progress deltas   │
│ - Flag CDN Stalls (delta p = 0 for >= 15s; HTTP 404/500/503 errors)          │
└──────────────────────────────────────────────────────────────────────────────┘
```

### 6.1. Tier 1: Frontier Exhaustion & Engine Health Heuristics
- **Exploration Frontier:** A node remains in the active `Frontier Queue` as long as untested affordances remain.
- **Fixed Variation Depth ($K = 3$):** Each available on-screen beacon is probed exactly $K = 3$ times with physical micro-variations (center tap, 5px offset tap, 80 ms extended hold tap).
- **Soft-Lock Defect:** If all beacons on a screen have been probed $K$ times, screen state remains unchanged, no "Back / Close" buttons exist, and the idle watchdog timer (5000 ms) expires $\rightarrow$ flagged as `CRITICAL_SOFT_LOCK`.
- **Functional Crash Defect:** If an interaction triggers an unhandled `NullReferenceException`, missing asset reference, or EventSystem desynchronization in the Unity console $\rightarrow$ flagged as `FUNCTIONAL_CRASH`.
- **Native OS Crash:** If the engine process crashes at the OS level (`SIGSEGV`, `0xC0000005`, OOM) $\rightarrow$ the post-mortem watchdog parses `Player.log` and logs `NATIVE_ENGINE_CRASH`.

### 6.2. Tier 2: Strict GDD Funnel Verification
- Compares the observed transition graph against mandatory GDD flow matrices.
- Flags **Progression Blocker Defects** if mandatory tutorial steps or onboarding screens cannot be completed.
- Flags **Interface Routing Discrepancies** if an edge routes to an unexpected target window.

### 6.3. Tier 3: Automated Layout & Localization Audit (`UrdtLayoutInspector`)
Automated inspection of all active `TextMeshProUGUI` components upon screen entry or language switch:
1. **Text Truncation (`LAYOUT_TEXT_TRUNCATED`):** Evaluates `tmp.isTextOverflowing` and `tmp.isTextTruncated`. Detects clipped labels without scroll capability.
2. **Bounds Overflow (`LAYOUT_BOUNDS_OVERFLOW`):** Compares text `RectTransform` world-space screen corners against parent button bounding boxes.
3. **Auto-Size Degeneration (`LAYOUT_FONT_DEGRADATION`):** Detects unreadable micro-text where `enableAutoSizing` reduced resolved `fontSize < 10.0f`.
4. **Missing Glyphs (`LAYOUT_MISSING_GLYPHS`):** Intercepts fallback replacement characters (`\uFFFD` or missing square glyphs) via `tmp.textInfo.characterInfo`.

### 6.4. Tier 4: Async Asset Streaming & CDN Monitoring (`UrdtAsyncLoadingInspector`)
- **The False Soft-Lock Problem:** During chapter streaming or asset downloading, UI is blocked by an overlay (`CanvasGroup.blocksRaycasts = true`) while a progress bar fills. A naive inactivity timeout would falsely classify this as a soft-lock.
- **Hybrid Streaming Detector:**
  - Inspects active `AsyncOperationHandle` in Addressables and active `UnityWebRequest` instances.
  - Monitors progress dynamics on `Slider.value` or `Image.fillAmount`.
- **Soft-Lock Suppression:** As long as download progress advances ($\Delta p > 0$ within a 5000 ms sliding window), stagnation alarms are suppressed (`STAGNATION_SUPPRESSED_DOWNLOADING`, up to 120s max).
- **CDN Stall Identification (`ASSET_STREAMING_STALL`):** Flagged if progress freezes ($\Delta p = 0$ for $\ge 15\text{ seconds}$) or HTTP errors (`404/500/503`, `CRC Mismatch`) are detected.

---

## 7. Deliverables and Handoff to Game & Testing Mode

Upon concluding Mode 1 exploration, the reviewer produces three concrete deliverables:

1. **`application_map.json`:** A versioned, machine-readable JSON artifact containing:
   - Complete serialized multigraph $\mathcal{G} = (V, E)$.
   - Validated control scheme bindings (`controlBindings`).
   - Catalog of discovered interactive screens, modals, and gameplay zones.
   - Identified dead ends and coverage gaps.
2. **Interactive HTML Topological Map in `Docs/QA_Audit_Report.html`:**
   - Standalone Vis-Network Canvas visualization (zero external CDN dependencies).
   - Sanitized strings (`escapeHtml`), force-directed layout frozen after 100 iterations.
   - Color-coded: Green = nominal path, Blue = explored branches, **Pulsing Red** = detected defects with interactive inspection cards.
3. **Mode Transition Handoff (`READY_FOR_TESTING`):**
   - If zero blocking defects exist, the application map is passed directly to **Mode 2 ([Game & Testing](URDT_Autonomous_Reviewer_Game_Testing_EN.md))**, which uses the graph for autonomous navigation ($A^*$ pathfinding) and executes deep invariant verification across all reachable game mechanics.
