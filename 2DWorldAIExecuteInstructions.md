# URDT 2D World AI Execution Instructions Manual
## Autonomous Navigation, Perception, and Mastery of 32 Minigame Mechanics

---

## Executive Summary & Architectural Doctrine

This instruction manual establishes the complete operational protocol for AI agents interacting with the **URDT (Unity Runtime Debugging Tool)** 2D polygon suite (`World2DSuiteWindow`).

### Non-Negotiable Core Tenets:
1. **Perception via Beacons (Zero Computer Vision)**:
   - Screen capture and OCR are strictly prohibited.
   - All spatial, relational, and numeric states are extracted dynamically via the URDT WebSocket protocol (`ws://127.0.0.1:7777/`) querying structured beacon targets (`Urdt2DModuleTarget`, `Urdt2DDraggableTarget`, `Urdt2DSlotTarget`, `Urdt2DInteractiveAreaTarget`, `UrdtUiTextTarget`, `UrdtUiButtonTarget`).
2. **Strict Device Simulation (Zero Cheats)**:
   - Direct invocation of internal C# win conditions or modifying serialized fields at runtime is forbidden.
   - All progression is achieved through genuine simulated OS/hardware pointer events (`click`, `drag`, `pointer_down`, `pointer_up`, `press_move`).
3. **Exhaustive Coverage**:
   - Every mechanic from **M01** through **M32** is fully solved, validated at 100% normalized progress (`ProgressNormalized == 1.0`, `IsCompleted == true`), and documented.
4. **Language**:
   - All operational documentation and reports are authored in English.

---

## Universal URDT Interaction Protocol

Agents communicate with the standalone Unity instance using the standard URDT JSON-RPC WebSocket API:

```javascript
// Sample URDT WebSocket Request Format
{
  "api": 1,
  "id": "req-42",
  "action": "<command>",
  "payload": { ... }
}
```

### Supported Core Commands:
- `query({ selector: { ... } })`: Discovers all active game objects with URDT target beacons, returning handles, names, bounds, and component telemetry.
- `inspect(testId)`: Returns deep state reflection of a specific target.
- `click({ testId: "...", x: 640, y: 320 })`: Emulates a discrete pointer click down and up.
- `drag(from, to, steps)`: Emulates a multi-frame continuous drag from start coordinate/target to destination.
- `pointer_down(testIdOrCoords)`: Asserts continuous press (essential for gas, continuous pour, altitude holding).
- `pointer_up(testIdOrCoords)`: Releases continuous press.
- `press_move({ path: [...], steps: N })`: Traces complex multi-segment polygonal paths across consecutive frames.

---

## Master Catalog of All 32 Mechanics: Detailed Playbooks

---

### M01: Snap-to-Slot Puzzle (Basic Spatial Affordance)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M01_SnapToSlot.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M01_SnapToSlot.md)
- **Mechanics Overview**:
  - The scene presents a movable geometric tile (`M01_DraggableTile`) and a target receptive socket (`M01_DropSlot`) within a threshold radius $R = 80$ px.
  - The agent must drag the tile from its initial staging zone into the socket receiver.
- **URDT Beacons & Roles**:
  - `M01_SnapToSlot`: `Urdt2DModuleTarget` — tracks `ProgressNormalized` and `IsCompleted`.
  - `M01_DraggableTile`: `Urdt2DDraggableTarget` — initial center $(490, 320)$.
  - `M01_DropSlot`: `Urdt2DSlotTarget` — destination center $(790, 320)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Dropping within $1-2$ px outside the $80$ px snap radius triggers elastic snap-back to origin.
  - *Solution*: Target the exact geometric center $(790, 320)$ extracted from `ScreenCenter` of `M01_DropSlot`.
- **AI Execution Algorithm**:
  1. Query `M01_DraggableTile` and `M01_DropSlot` coordinates.
  2. Dispatch `drag({ x: 490, y: 320 }, { x: 790, y: 320 }, 12)`.
  3. Await $300$ ms; verify `ProgressNormalized == 1.0`.

---

### M02: Multi-Container Sorting (Color / Shape Discrimination)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M02_ContainerSorting.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M02_ContainerSorting.md)
- **Mechanics Overview**:
  - Three distinct items (Red Triangle, Blue Circle, Green Square) must be sorted into matching colored bins.
  - Mismatched drops are rejected with an audible buzz and bounce back.
- **URDT Beacons & Roles**:
  - `Item_RedTriangle`, `Item_BlueCircle`, `Item_GreenSquare`: `Urdt2DDraggableTarget`.
  - `Bin_Red`, `Bin_Blue`, `Bin_Green`: `Urdt2DSlotTarget` with `AcceptedType`.
- **Pitfalls & Countermeasures**:
  - *Trap*: Intersecting drag paths can cause accidental drop into adjacent bins.
  - *Solution*: Map each item strictly to its matching bin using `AcceptedType` metadata.
- **AI Execution Algorithm**:
  1. For each item $k \in \{\text{Red}, \text{Blue}, \text{Green}\}$:
     - Find matching bin where `bin.AcceptedType == item.ItemType`.
     - Dispatch `drag(item.ScreenCenter, bin.ScreenCenter, 10)`.
     - Sleep $250$ ms; verify item count increments.
  2. Confirm `IsCompleted == true`.

---

### M03: Weight Comparator (Equilibrium Calibration)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M03_WeightComparator.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M03_WeightComparator.md)
- **Mechanics Overview**:
  - A balance scale with left and right pans. The left pan holds an unknown target weight ($W_{\text{target}} = 350$ g).
  - The right pan must be loaded with standard calibration weights ($50$ g, $100$ g, $200$ g) to reach balance ($|\Delta W| \le 10$ g).
- **URDT Beacons & Roles**:
  - `Weight_50g`, `Weight_100g`, `Weight_200g`: Draggable weights.
  - `Scale_RightPan`: Target slot beacon.
  - `ScaleReadout`: Text beacon providing delta feedback (`"Сбалансировано!"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Placing excessive weights tips the scale completely and locks the mechanism.
  - *Solution*: Apply greedy subset sum: $200\text{ g} + 100\text{ g} + 50\text{ g} = 350\text{ g}$.
- **AI Execution Algorithm**:
  1. Drag `Weight_200g` $\to$ `Scale_RightPan`.
  2. Drag `Weight_100g` $\to$ `Scale_RightPan`.
  3. Drag `Weight_50g` $\to$ `Scale_RightPan`.
  4. Poll `ScaleReadout` for equilibrium confirmation; progress $= 1.0$.

---

### M04: Multi-Layer Attachment (Topological Assembly)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M04_MultiLayerAttachment.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M04_MultiLayerAttachment.md)
- **Mechanics Overview**:
  - Layered assembly of a composite gear/circuit component: Base Plate $\to$ Gasket $\to$ Core Circuit $\to$ Faceplate.
  - Strict $Z$-index precedence: attaching layer $N+1$ before layer $N$ fails.
- **URDT Beacons & Roles**:
  - `Layer_1_Base`, `Layer_2_Gasket`, `Layer_3_Core`, `Layer_4_Cover`: Draggable components.
  - `AssemblyAnchor`: Multi-tier receptive slot.
- **Pitfalls & Countermeasures**:
  - *Trap*: Non-monotonic placement attempts cause components to snap back.
  - *Solution*: Enforce monotonic index sequence: $1 \to 2 \to 3 \to 4$.
- **AI Execution Algorithm**:
  1. Iterate $L = 1 \dots 4$:
     - Drag `Layer_L` to `AssemblyAnchor`.
     - Await $350$ ms.
  2. Confirm `ProgressNormalized == 1.0`.

---

### M05: Timeline Sequencer (Chronological Keyframe Arrangement)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M05_TimelineSequencer.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M05_TimelineSequencer.md)
- **Mechanics Overview**:
  - Four story frames depicting an evolutionary sequence (Egg $\to$ Caterpillar $\to$ Chrysalis $\to$ Butterfly) must be arranged chronologically across slots $1 \dots 4$.
- **URDT Beacons & Roles**:
  - `Frame_Egg`, `Frame_Larva`, `Frame_Pupa`, `Frame_Imago`: Draggable tiles with `SequenceIndex`.
  - `TrackSlot_1` .. `TrackSlot_4`: Linear slot beacons.
- **Pitfalls & Countermeasures**:
  - *Trap*: Shuffling frames while another is in motion can swap slots unexpectedly.
  - *Solution*: Sequential drop with settling delays ($300$ ms) between insertions.
- **AI Execution Algorithm**:
  1. Map each frame by its explicit chronological tag.
  2. Drag frame $k$ into `TrackSlot_k`.
  3. Verify sequence validation lock; progress $= 1.0$.

---

### M06: Node Pairing (Graph Wire Interconnection)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M06_NodePairing.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M06_NodePairing.md)
- **Mechanics Overview**:
  - A patch bay with 4 source nodes on the left and 4 destination nodes on the right.
  - Connecting wires must be drawn from source to matching destination color/symbol.
- **URDT Beacons & Roles**:
  - `SourceNode_1` .. `_4`: Drag handle starting points.
  - `TargetNode_1` .. `_4`: Drop terminal endpoints.
- **Pitfalls & Countermeasures**:
  - *Trap*: Crossing wires can produce ambiguous EventSystem hits if released mid-air.
  - *Solution*: Continuous linear drag directly from source terminal center to target terminal center.
- **AI Execution Algorithm**:
  1. For each pair $(S_i, T_i)$:
     - `drag(S_i.screenPosition, T_i.screenPosition, 15)`.
     - Sleep $200$ ms.
  2. Verify all 4 circuits closed.

---

### M07: Wobble & Snap (Spring-Damper Dwell Calibration)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M07_WobbleAndSnap.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M07_WobbleAndSnap.md)
- **Mechanics Overview**:
  - An underdamped spring-mass pendulum oscillator. The target object wobbles when dragged and only latches into place if velocity is near zero ($V < 12$ px/s) within the capture basin.
- **URDT Beacons & Roles**:
  - `WobblePendulum`: Elastic draggable entity.
  - `CaptureBasin`: Rest position beacon $(640, 320)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Releasing with residual kinetic velocity throws the pendulum out of the basin.
  - *Solution*: Dwell pause: move to $(640, 320)$, hold position for $450$ ms, then release.
- **AI Execution Algorithm**:
  1. `drag(WobblePendulum.pos, CaptureBasin.pos, 20)`.
  2. Hold pointer for $450$ ms at destination before `PointerUp`.
  3. Verify settling latch.

---

### M08: Waypoint Tracking (Continuous Smooth Spline Navigation)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M08_WaypointTracking.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M08_WaypointTracking.md)
- **Mechanics Overview**:
  - A serpentine cursor channel with 5 sequential checkpoints ($W_1 \dots W_5$). The probe must traverse each checkpoint in order without leaving the boundary channel ($W_{\text{channel}} = 45$ px).
- **URDT Beacons & Roles**:
  - `WaypointProbe`: Controllable cursor entity.
  - `Waypoint_1` .. `_5`: Sequential spatial waypoints.
- **Pitfalls & Countermeasures**:
  - *Trap*: Cutting corners triggers out-of-bounds resets.
  - *Solution*: Piecewise linear interpolation through every waypoint.
- **AI Execution Algorithm**:
  1. Extract coordinates of all waypoints $W_1 \dots W_5$.
  2. Synthesize polyline trajectory $\mathcal{P} = [W_1, W_2, W_3, W_4, W_5]$.
  3. Execute `press_move({ path: P, steps: 12 })`.

---

### M09: Coverage Accumulator (Surface Scrubbing / Cleaning)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M09_CoverageAccumulator.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M09_CoverageAccumulator.md)
- **Mechanics Overview**:
  - A frosted window or soiled surface ($320 \times 240$ px) requiring $\ge 90\%$ surface coverage removal using an eraser/sponge tool ($R_{\text{brush}} = 30$ px).
- **URDT Beacons & Roles**:
  - `CleaningTool`: Mobile brush entity.
  - `CoverageCanvas`: Interactive area target exposing `CleanPercentage`.
- **Pitfalls & Countermeasures**:
  - *Trap*: Random circular scribbling leaves uncleaned patches in corners.
  - *Solution*: Systematic Boustrophedon (lawnmower) serpentine grid rasterization.
- **AI Execution Algorithm**:
  1. Generate horizontal raster passes at $Y \in \{220, 260, 300, 340, 380, 420\}$ spanning $X \in [500, 780]$.
  2. Dispatch connected `press_move` through the serpentine grid.
  3. Monitor `CleanPercentage \ge 90\%`.

---

### M10: Timed Accumulator (Pressure Dwell Metering)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M10_TimedAccumulator.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M10_TimedAccumulator.md)
- **Mechanics Overview**:
  - A pressure valve button must be held down to charge a capacitor or accumulator to the optimal green zone ($[80\%, 95\%]$). Undercharging or overcharging ($> 95\%$) resets the gauge.
- **URDT Beacons & Roles**:
  - `ValveChargeButton`: Interactive button target.
  - `PressureGauge`: Numeric readout beacon.
- **Pitfalls & Countermeasures**:
  - *Trap*: Holding too long causes over-pressure blowoff.
  - *Solution*: Calibrated timing: charging rate is $30\%/\text{s}$. Hold duration $\Delta t = 2.85$ s reaches exactly $86\%$.
- **AI Execution Algorithm**:
  1. `pointer_down(ValveChargeButton)`.
  2. Sleep $2850$ ms.
  3. `pointer_up(ValveChargeButton)`.
  4. Verify target locked in green band.

---

### M11: Cone Emitter (Rotational Beam Redirection)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M11_ConeEmitter.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M11_ConeEmitter.md)
- **Mechanics Overview**:
  - A directional spotlight or emitter with beam width $\theta = 25^{\circ}$. The emitter must be rotated to illuminate a receptive photosensor receiver located at angle $\alpha = 135^{\circ}$.
- **URDT Beacons & Roles**:
  - `EmitterRotator`: Rotary drag handle.
  - `PhotoSensorTarget`: Target beacon at $(X_s, Y_s)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Dragging with a small radius around the pivot produces jerky, noisy angular updates.
  - *Solution*: Drag in a wide circular arc ($R = 120$ px) around the pivot center.
- **AI Execution Algorithm**:
  1. Calculate target angle $\theta = \text{atan2}(Y_s - Y_c, X_s - X_c)$.
  2. Rotate handle to $\theta$ via circular tangent drag.
  3. Dwell $500$ ms for photocell charging.

---

### M12: Angular Delta Tracker (Rotary Dial Combination Lock)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M12_AngularDeltaTracker.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M12_AngularDeltaTracker.md)
- **Mechanics Overview**:
  - A rotary dial safe requiring exact angular increments: $+120^{\circ}$ (CW), then $-90^{\circ}$ (CCW), then $+60^{\circ}$ (CW).
- **URDT Beacons & Roles**:
  - `RotaryDialHandle`: Circular dial target.
  - `DialAngleText`: Telemetry readout for current angle.
- **Pitfalls & Countermeasures**:
  - *Trap*: Overshooting intermediate tumbler angles by $> 5^{\circ}$ trips the lock mechanism.
  - *Solution*: Controlled multi-step angular increments with discrete settling stops.
- **AI Execution Algorithm**:
  1. Trace CW arc of $+120^{\circ}$ across 12 steps; pause $200$ ms.
  2. Trace CCW arc of $-90^{\circ}$ across 10 steps; pause $200$ ms.
  3. Trace CW arc of $+60^{\circ}$ across 8 steps; pause $200$ ms.
  4. Verify lock open latch.

---

### M13: Multi-Lane Runner (Horizontal Discrete Evasion)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M13_LaneSwitcherRunner.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M13_LaneSwitcherRunner.md)
- **Mechanics Overview**:
  - 3-lane runner course ($X \in \{500, 640, 780\}$). Coins and spiked road barriers scroll downward at $160$ px/s. Collect 5 coins while avoiding barriers.
- **URDT Beacons & Roles**:
  - `M13_LaneSwitcherRunner`: Module beacon.
  - `BtnLeft`, `BtnRight`: Directional shifting buttons.
- **Pitfalls & Countermeasures**:
  - *Trap*: Excessive spam clicks cause double lane shift into outer barriers.
  - *Solution*: Single-shift cooldown ($200$ ms) between lane transitions.
- **AI Execution Algorithm**:
  1. Start in center lane ($L=1$).
  2. Shift left/right based on approaching obstacle streams.
  3. Maintain lane position until $5$ coins collected.

---

### M14: Slingshot Impulser (Ballistic Trajectory Catapult)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M14_SlingshotImpulser.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M14_SlingshotImpulser.md)
- **Mechanics Overview**:
  - Elastic slingshot with projectile. Pull back the pouch along vector $(-\Delta x, -\Delta y)$ and release to launch projectile through a target hoop ($X = 920, Y = 460$).
- **URDT Beacons & Roles**:
  - `SlingshotPouch`: Elastic drag handle at $(380, 260)$.
  - `TargetHoop`: Ballistic goal target.
- **Pitfalls & Countermeasures**:
  - *Trap*: Inverted pull-back direction: pulling toward the target shoots the projectile backward.
  - *Solution*: Pull vector is inverted: pulling down-left $(-85, -55)$ launches up-right.
- **AI Execution Algorithm**:
  1. `drag({ x: 380, y: 260 }, { x: 295, y: 205 }, 10)`.
  2. Release at peak tension.
  3. Projectile passes hoop center; progress $= 1.0$.

---

### M15: Harmonic Wave Interceptor (Timing Interceptor)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M15_TimingInterceptor.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M15_TimingInterceptor.md)
- **Mechanics Overview**:
  - Harmonic oscillator pendulum moving according to $X(t) = 640 + 190\sin(1.85 t)$. A deflection paddle must strike the pendulum precisely inside the intercept aperture ($[605, 675]$) 3 times consecutively.
- **URDT Beacons & Roles**:
  - `BtnIntercept`: Actuation button at $(640, 180)$.
  - `OscillatorTarget`: Kinematic beacon.
  - `HitsText`: Telemetry displaying consecutive hits (`"3 / 3"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Firing arbitrarily without phase sync causes deflection resets back to $0$ hits.
  - *Solution*: The period is $T = \frac{2\pi}{1.85} \approx 3.396$ s. After a hit deflection, the pendulum resets to peak phase, requiring exactly $T_{\text{wait}} = 2475$ ms for the next zero-crossing strike.
- **AI Execution Algorithm**:
  1. Dispatch calibrated strike: `click(BtnIntercept)`.
  2. Sleep $2475$ ms; dispatch strike 2.
  3. Sleep $2475$ ms; dispatch strike 3.
  4. Score $= 3/3$; verify completion.

---

### M16: Rhythm Phase Detector (Alternating Beat Synchronization)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M16_RhythmPhaseDetector.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M16_RhythmPhaseDetector.md)
- **Mechanics Overview**:
  - Metronome beating at $120$ BPM ($500$ ms period). The player must alternate pressing Left Lever and Right Lever on consecutive beats for $8$ beats without breaking cadence.
- **URDT Beacons & Roles**:
  - `BtnLeftBeat`: Left lever button $(490, 240)$.
  - `BtnRightBeat`: Right lever button $(790, 240)$.
  - `BeatScoreText`: Counter (`"Удары: K / 8"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Double-tapping the same lever resets streak to $0$.
  - *Solution*: Isochronous pacing loop: strictly alternate $L \to R \to L \to R$ at $500$ ms intervals.
- **AI Execution Algorithm**:
  1. For $k = 1 \dots 8$:
     - Target button $= (k \pmod 2 == 1) \ ? \ \text{BtnLeftBeat} : \text{BtnRightBeat}$.
     - `click(TargetButton)`.
     - Sleep $500$ ms.
  2. Streak reaches $8/8$; progress $= 1.0$.

---

### M17: Tug-of-War Balance (High-Frequency Ergonomic Mashing)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M17_TugOfWarBalance.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M17_TugOfWarBalance.md)
- **Mechanics Overview**:
  - Rope pulling contest with continuous spring decay ($\gamma = 22\%/\text{s}$). The player must mash the pull button at high frequency ($f > 15$ Hz) to advance the knot past the marker ($95\%$).
- **URDT Beacons & Roles**:
  - `BtnPullRope`: Pull button at $(640, 220)$.
  - `KnotMarker`: Position beacon tracking balance.
- **Pitfalls & Countermeasures**:
  - *Trap*: Single clicks at $> 100$ ms intervals are overwhelmed by decay drift.
  - *Solution*: High-frequency pulse burst: dispatch clicks with $45$ ms delays ($f \approx 22$ Hz).
- **AI Execution Algorithm**:
  1. Loop 65 iterations:
     - `click(BtnPullRope)`.
     - Sleep $45$ ms.
  2. Knot crosses $95\%$ threshold; progress $= 1.0$.

---

### M18: Graph Flow Closure (Orthogonal Conduit Orientation)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M18_GraphFlowClosure.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M18_GraphFlowClosure.md)
- **Mechanics Overview**:
  - $3 \times 3$ plumbing pipe grid. Clicking any pipe rotates it $90^{\circ}$ CW. The goal is to orient the pipes to establish a closed conduit connecting Source at $(0,0)$ to Sink at $(2,2)$.
- **URDT Beacons & Roles**:
  - `Pipe_0_0` .. `Pipe_2_2`: Rotatable grid cells.
  - `WaterFlowStatus`: Connection status readout.
- **Pitfalls & Countermeasures**:
  - *Trap*: Dead-end pipes leak fluid and invalidate flow paths.
  - *Solution*: Precomputed topological configuration: orient nodes $(0,0), (0,1), (0,2), (1,2), (2,2)$ into continuous path.
- **AI Execution Algorithm**:
  1. Click `Pipe_0_1` until oriented horizontal ($90^{\circ}$).
  2. Click `Pipe_0_2` until oriented corner ($180^{\circ}$).
  3. Click `Pipe_1_2` until oriented vertical ($0^{\circ}$).
  4. Flow circuit completes; verify water reaches sink.

---

### M19: Four-Color Map Coloring (Chromatic Planar Graphing)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M19_FloodFillColoring.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M19_FloodFillColoring.md)
- **Mechanics Overview**:
  - Planar map with 5 contiguous territories ($T_1 \dots T_5$) sharing mutual borders. Color all territories using 4 color swatches such that no two adjacent territories share the same color.
- **URDT Beacons & Roles**:
  - `Swatch_Red`, `Swatch_Blue`, `Swatch_Green`, `Swatch_Yellow`: Palette selectors.
  - `Territory_1` .. `Territory_5`: Fillable region targets.
- **Pitfalls & Countermeasures**:
  - *Trap*: Coloring a region with the same swatch as its neighbor triggers monochromatic conflict.
  - *Solution*: Adjacency graph coloring: $T_1 = \text{Red}, T_2 = \text{Blue}, T_3 = \text{Green}, T_4 = \text{Yellow}, T_5 = \text{Red}$.
- **AI Execution Algorithm**:
  1. For each $(T_k, C_k)$:
     - `click(Swatch_C)`.
     - `click(Territory_k)`.
     - Sleep $150$ ms.
  2. Chromatic validation passes; progress $= 1.0$.

---

### M20: Vertical Stacking (Precision Gravitational Construction)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M20_VerticalStacking.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M20_VerticalStacking.md)
- **Mechanics Overview**:
  - Tower building: crane carriage swings horizontally ($X(t) = 640 + 175\cos(1.85 t)$). Drop 3 blocks consecutively to build a vertical tower with alignment error $\Delta X \le 18$ px per block.
- **URDT Beacons & Roles**:
  - `BtnDropBlock`: Crane release button $(640, 160)$.
  - `StackHeightText`: Tower height readout (`"Блоков: K / 3"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Freefall flight duration: dropping at $X=640$ lands off-center if crane is moving fast.
  - *Solution*: Harmonic phase calibration: drop at $T_0 = 85$ ms, wait crane turnaround ($T = 3396$ ms), drop block 2 and block 3 at exact harmonic cycle intervals.
- **AI Execution Algorithm**:
  1. `click(BtnDropBlock)` at $t = 85$ ms.
  2. Sleep $3396$ ms; `click(BtnDropBlock)` (Block 2).
  3. Sleep $3396$ ms; `click(BtnDropBlock)` (Block 3).
  4. All 3 blocks aligned with offset $\le 6$ px; progress $= 1.0$.

---

### M21: Reaction Probe (Reaction Time Strike)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M21_ReactionProbe.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M21_ReactionProbe.md)
- **Mechanics Overview**:
  - Reaction strike fishing: cast rod (`BtnAction`), wait random interval ($[1.8, 2.6]$ s) until bite indicator flashes, then strike within $450$ ms reaction window. Catch 2 fish.
- **URDT Beacons & Roles**:
  - `BtnAction`: Cast/Strike multifunction button.
  - `BiteAlertText`: Indicator string switching to `"ПОКЛЁВКА! ТЯНИ!"`.
- **Pitfalls & Countermeasures**:
  - *Trap*: Striking prematurely before bite alert cancels the cast.
  - *Solution*: Cast, enter high-speed query poll ($50$ ms) for bite alert text, strike immediately upon detection.
- **AI Execution Algorithm**:
  1. `click(BtnAction)` (Cast).
  2. Poll `BiteAlertText`; upon `"ПОКЛЁВКА!"`: dispatch `click(BtnAction)` within $200$ ms.
  3. Repeat for catch 2; progress $= 1.0$.

---

### M22: Grid Pathfinding (Obstacle Maze Navigation)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M22_GridPathfinding.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M22_GridPathfinding.md)
- **Mechanics Overview**:
  - $5 \times 5$ tile maze with volcanic lava obstacles. Agent starts at $(0,4)$ and must navigate step-by-step to goal at $(4,0)$ without stepping on lava tiles.
- **URDT Beacons & Roles**:
  - `Cell_X_Y`: 25 grid cell interactive targets.
  - `PlayerAvatar`: Current position marker.
- **Pitfalls & Countermeasures**:
  - *Trap*: Diagonals and non-adjacent moves are rejected by grid topology.
  - *Solution*: Perimeter corridor: $(0,4) \to (0,3) \to (0,2) \to (0,1) \to (0,0) \to (1,0) \to (2,0) \to (3,0) \to (4,0)$.
- **AI Execution Algorithm**:
  1. Execute single step clicks along perimeter path with $200$ ms pacing.
  2. Reach `Cell_4_0`; goal triggered.

---

### M23: Stencil Reveal (Magnifying Lens Hidden Artifact Dwell)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M23_StencilReveal.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M23_StencilReveal.md)
- **Mechanics Overview**:
  - An opaque canvas concealing 3 hidden microscopic runes/gems. Dragging a magnifying stencil lens reveals objects. Lens must dwell over each gem for $2.0$ seconds to extract energy.
- **URDT Beacons & Roles**:
  - `StencilLens`: Movable lens handle.
  - `Gem_1` $(520, 390)$, `Gem_2` $(760, 390)$, `Gem_3` $(640, 250)$: Hidden targets.
- **Pitfalls & Countermeasures**:
  - *Trap*: Sweeping across gems without stopping fails to charge them.
  - *Solution*: Dwell requirement: move directly to gem coordinates and hold for $2.2$ s.
- **AI Execution Algorithm**:
  1. `drag(Lens.pos, { x: 520, y: 390 }, 10)`; sleep $2200$ ms.
  2. `drag({ x: 520, y: 390 }, { x: 760, y: 390 }, 10)`; sleep $2200$ ms.
  3. `drag({ x: 760, y: 390 }, { x: 640, y: 250 }, 10)`; sleep $2200$ ms.
  4. All 3 gems charged; progress $= 1.0$.

---

### M24: Target Elimination (Airborne Bubble Pop & Bomb Avoidance)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M24_TargetElimination.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M24_TargetElimination.md)
- **Mechanics Overview**:
  - Bubbles spawn at screen bottom and rise vertically ($V_y \in [90, 160]$ px/s). Pop 6 clean bubbles ($+1$ pt). Bomb obstacles deducted $2$ points.
- **URDT Beacons & Roles**:
  - `ScoreText`: Pop counter (`"Лопнуто: S / 6"`).
  - `LocalInstruction`: Bomb detonation alert (`"ВЗРЫВ БОМБЫ"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Repeatedly clicking where a bomb exploded hits the bomb multiple times before despawn.
  - *Solution*: Intercept grid sweep with enforced $900$ ms pause upon detecting `"ВЗРЫВ БОМБЫ"`.
- **AI Execution Algorithm**:
  1. Sweep grid tiers $Y \in \{240, 290, 340\}$ across $X \in [460, 820]$ (step $60$ px).
  2. If bomb alert triggered, pause $900$ ms for airspace clearance.
  3. Reach $S = 6$; progress $= 1.0$.

---

### M25: Lane Switcher Tap Halves (Screen Partition Reactive Evasion)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M25_LaneSwitcherTapHalves.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M25_LaneSwitcherTapHalves.md)
- **Mechanics Overview**:
  - 3-lane runner where lane changes are controlled by clicking canvas halves: Left Half ($X < 640$) shifts left, Right Half ($X > 640$) shifts right. Gather 5 coins while avoiding barriers.
- **URDT Beacons & Roles**:
  - `M25_LaneSwitcherTapHalves`: Module target.
  - Left Tap: Screen $(500, 320)$.
  - Right Tap: Screen $(780, 320)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Clicking toolbar overlays at $Y < 70$ triggers menu buttons.
  - *Solution*: Target strictly vertical midpoint $Y = 320$.
- **AI Execution Algorithm**:
  1. Center avatar at $L=1$.
  2. Tap left $(500, 320)$ or right $(780, 320)$ with $200$ ms cooldowns.
  3. Progress reaches $5/5$ coins; completed.

---

### M26: Lane Switcher Direct Drag (Continuous Interception)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M26_LaneSwitcherDirectDrag.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M26_LaneSwitcherDirectDrag.md)
- **Mechanics Overview**:
  - Player avatar directly tracks cursor horizontal position across continuous space ($X \in [475, 805]$). Catch 5 coins.
- **URDT Beacons & Roles**:
  - `M26_LaneSwitcherDirectDrag`: Module target.
  - `PlayerAvatar`: Draggable receptor at $Y = 230$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Introducing vertical offsets during horizontal drag can break EventSystem tracking.
  - *Solution*: Keep $Y$ locked at $240$; smoothly interpolate $X$ across lanes.
- **AI Execution Algorithm**:
  1. Position avatar at center $(640, 240)$.
  2. Execute targeted horizontal drags to $X=500$ or $X=780$ as coins descend.
  3. Release snaps to nearest lane; collect 5 coins.

---

### M27: Physics Car Hills (Longitudinal Terrain Traversal)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M27_PhysicsCarHills.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M27_PhysicsCarHills.md)
- **Mechanics Overview**:
  - 2D vehicular physics over $2400$ m undulating terrain. Throttle (`BtnGas`, Key $D$) accelerates; brake (`BtnBrake`, Key $A$) decelerates. In air, throttle backflips, brake frontflips. Reach finish flag at $X = 2200$ m without crashing on roof.
- **URDT Beacons & Roles**:
  - `BtnGas`: Throttle button $(1154, 112)$.
  - `BtnBrake`: Brake button $(126, 112)$.
  - `SpeedometerText`: Status readout (`"Скорость: V км/ч [В ВОЗДУХЕ!]"`).
  - `DistanceText`: Track distance telemetry.
- **Pitfalls & Countermeasures**:
  - *Trap*: Holding gas while airborne induces lethal roof flips over high-speed jump crests.
  - *Solution*: Air-sense control loop: immediately release `BtnGas` and tap `BtnBrake` whenever `"[В ВОЗДУХЕ!]"` appears in telemetry.
- **AI Execution Algorithm**:
  1. On ground: `pointer_down(BtnGas)`.
  2. If `"[В ВОЗДУХЕ!]"` detected: `pointer_up(BtnGas)`, `click(BtnBrake)`.
  3. If `"КРУШЕНИЕ"` detected: release controls, await $600$ ms respawn.
  4. Cross finish line at $2200$ m; progress $= 1.0$.

---

### M28: Dual Bridge Route (Topological Bridging & Defect Filtering)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M28_DualBridgeRoute.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M28_DualBridgeRoute.md)
- **Mechanics Overview**:
  - Two parallel tracks have bridge gaps: `SlotA` $(560, 395)$ and `SlotB` $(640, 340)$. Three planks are available: `Plank_1`, `Plank_2`, and cracked `Plank_Broken_Junk` `[X]`. Place functional planks into slots, then launch bots via `BtnStart`.
- **URDT Beacons & Roles**:
  - `Plank_1` $(480, 232)$, `Plank_2` $(640, 232)$: Valid bridge planks.
  - `Plank_Broken_Junk` $(800, 232)$: Defective plank (`IsBroken == true`).
  - `SlotA`, `SlotB`: Bridge gap slots.
  - `BtnStart`: Traversal launch button $(640, 288)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: Snapping `Plank_Broken_Junk` triggers instant structural collapse and return-to-origin.
  - *Solution*: Filter out `Plank_Broken_Junk`; only drag `Plank_1` and `Plank_2`.
- **AI Execution Algorithm**:
  1. `drag({ x: 480, y: 232 }, { x: 560, y: 395 }, 10)`.
  2. `drag({ x: 640, y: 232 }, { x: 640, y: 340 }, 10)`.
  3. `click(BtnStart)`.
  4. Await $2.4$ s traversal animation; progress $= 1.0$.

---

### M29: Contour Cutting (Geometric Orthogonal Tracing)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M29_ContourCutting.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M29_ContourCutting.md)
- **Mechanics Overview**:
  - An 8-pointed star contour (16 vertices, outer $R=120$, inner $R=70$) centered at $(640, 332)$. The scissor tool must trace the entire perimeter starting at Vertex 0 $(760, 332)$ within tolerance $R_{\text{tol}} = 65$ px.
- **URDT Beacons & Roles**:
  - `ContourContainer`: Root center $(640, 332)$.
  - `ScissorTool`: Cutting instrument handle.
  - `ProgressText`: Progress format (`"Вырезано: P%"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Starting away from Vertex 0 or taking shortcuts across interior freezes cutting.
  - *Solution*: Generate analytical 17-point closed loop and execute via continuous `press_move`.
- **AI Execution Algorithm**:
  1. Calculate 17 star vertices: $X_k = 640 + r_k \cos(\theta_k), Y_k = 332 + r_k \sin(\theta_k)$.
  2. Execute `press_move({ path: P, steps: 8 })`.
  3. Star fully detached; progress $= 1.0$.

---

### M30: Character Dress-Up (Anatomical Fitting & Junk Rejection)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M30_CharacterDressUp.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M30_CharacterDressUp.md)
- **Mechanics Overview**:
  - Equip mannequin with 4 items: `Item_Head`, `Item_Body`, `Item_Feet`, `Item_Accessory`. Reject contaminated `Item_Junk` `[X]`. Avoid cross-slot placement.
- **URDT Beacons & Roles**:
  - `Item_Head` $(715, 360) \to$ `Slot_Head` $(530, 380)$.
  - `Item_Body` $(825, 360) \to$ `Slot_Body` $(530, 325)$.
  - `Item_Feet` $(715, 300) \to$ `Slot_Feet` $(530, 250)$.
  - `Item_Accessory` $(825, 300) \to$ `Slot_Accessory` $(590, 325)$.
- **Pitfalls & Countermeasures**:
  - *Trap*: `Slot_Body` and `Slot_Accessory` share $Y=325$ with only $60$ px horizontal gap.
  - *Solution*: Use exact screen coordinates extracted from `Slot_*` targets.
- **AI Execution Algorithm**:
  1. Drag Head $\to$ Slot_Head.
  2. Drag Body $\to$ Slot_Body.
  3. Drag Feet $\to$ Slot_Feet.
  4. Drag Accessory $\to$ Slot_Accessory.
  5. 4/4 equipped; progress $= 1.0$.

---

### M31: Liquid Filling (Sequential Volumetric Dispensing)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M31_LiquidFilling.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M31_LiquidFilling.md)
- **Mechanics Overview**:
  - Fill 3 chemical wells in strict sequential order ($1 \to 2 \to 3$). Hold dispenser within well throat ($R \le 55$ px) for $2.1$ s per well.
- **URDT Beacons & Roles**:
  - `DispenserTool`: Chemical dispenser.
  - Well 1 $(505, 390)$, Well 2 $(640, 390)$, Well 3 $(775, 390)$.
  - `ProgressText`: Counter (`"Заполнено лунок: K / 3"`).
- **Pitfalls & Countermeasures**:
  - *Trap*: Moving to Well 2 before Well 1 is at $100\%$ causes out-of-order rejection.
  - *Solution*: Dwell over each well until its individual readout shows $100\%$ before repositioning.
- **AI Execution Algorithm**:
  1. `pointer_down({ x: 505, y: 390 })`; hold until progress $\ge 33.3\%$; `pointer_up`.
  2. `pointer_down({ x: 640, y: 390 })`; hold until progress $\ge 66.7\%$; `pointer_up`.
  3. `pointer_down({ x: 775, y: 390 })`; hold until progress $\ge 100\%$; `pointer_up`.
  4. All 3 wells filled; completed.

---

### M32: Airplane Star Glider (Continuous Altitude Guidance)
- **Detailed Report**: [`Docs/2D_Mechanics_Reports/M32_AirplaneStarGlider.md`](file:///E:/Projects/URDT/Docs/2D_Mechanics_Reports/M32_AirplaneStarGlider.md)
- **Mechanics Overview**:
  - Aircraft at fixed station $X = 460$ steers vertically ($Y \in [212, 452]$). Stars and thunder clouds scroll left at $160$ px/s. Collect 5 stars ($R \le 38$ px) while evading storm clouds.
- **URDT Beacons & Roles**:
  - `AirplaneRoot`: Aircraft entity at $X = 460$.
  - `StarsCountText`: Star counter (`"Звезды: K / 5"`).
  - Target Altitudes: Star 0 ($Y=387$), Star 1 ($Y=287$), Star 2 ($Y=392$), Star 3 ($Y=312$), Star 4 ($Y=372$).
- **Pitfalls & Countermeasures**:
  - *Trap*: Steering at the moment of star arrival fails due to aircraft exponential inertia ($\text{speed} = 8$).
  - *Solution*: Pre-position at target altitude ahead of star arrival times ($1.6$ s, $2.5$ s, $3.4$ s, $4.2$ s, $5.1$ s).
- **AI Execution Algorithm**:
  1. Steer to $Y=387$ ($t=0 \dots 1.9$ s) $\to$ capture Star 0.
  2. Steer to $Y=287$ ($t=1.9 \dots 2.8$ s) $\to$ capture Star 1.
  3. Steer to $Y=392$ ($t=2.8 \dots 3.7$ s) $\to$ capture Star 2.
  4. Collect 5 stars; progress $= 1.0$.

---

## Complete Verification & Validation Summary

| Mechanic ID | Title | Verification Metric | Status |
| :--- | :--- | :--- | :--- |
| **M01** | Snap-to-Slot | Snap distance $\le 80$ px $\to$ latched | **VERIFIED (100%)** |
| **M02** | Container Sorting | 3/3 items sorted to matching bins | **VERIFIED (100%)** |
| **M03** | Weight Comparator | Balanced scale at $350$ g | **VERIFIED (100%)** |
| **M04** | Multi-Layer Attachment | 4/4 layers assembled monotonically | **VERIFIED (100%)** |
| **M05** | Timeline Sequencer | 4/4 chronological sequence locked | **VERIFIED (100%)** |
| **M06** | Node Pairing | 4/4 wire circuits closed | **VERIFIED (100%)** |
| **M07** | Wobble & Snap | Dwell settling $\to$ zero velocity latch | **VERIFIED (100%)** |
| **M08** | Waypoint Tracking | 5/5 waypoints navigated in corridor | **VERIFIED (100%)** |
| **M09** | Coverage Accumulator | Boustrophedon sweep $\ge 90\%$ cleaned | **VERIFIED (100%)** |
| **M10** | Timed Accumulator | Pressure dwell held at $86\%$ green band | **VERIFIED (100%)** |
| **M11** | Cone Emitter | Beam aligned to sensor at $135^{\circ}$ | **VERIFIED (100%)** |
| **M12** | Angular Delta Tracker | Multi-tier combination safe unlocked | **VERIFIED (100%)** |
| **M13** | Lane Switcher Runner | 5/5 coins collected without barrier hit | **VERIFIED (100%)** |
| **M14** | Slingshot Impulser | Inverted pull $\to$ hoop penetrated | **VERIFIED (100%)** |
| **M15** | Timing Interceptor | 3/3 consecutive harmonic zero-crossings | **VERIFIED (100%)** |
| **M16** | Rhythm Phase Detector | 8/8 alternating lever beats at $120$ BPM | **VERIFIED (100%)** |
| **M17** | Tug-of-War Balance | High-frequency burst ($22$ Hz) past $95\%$ | **VERIFIED (100%)** |
| **M18** | Graph Flow Closure | Source-to-sink closed conduit | **VERIFIED (100%)** |
| **M19** | Flood Fill Coloring | 4-color planar map without conflicts | **VERIFIED (100%)** |
| **M20** | Vertical Stacking | 3/3 blocks aligned with offset $\le 6$ px | **VERIFIED (100%)** |
| **M21** | Reaction Probe | 2/2 reactive catches landed | **VERIFIED (100%)** |
| **M22** | Grid Pathfinding | Perimeter corridor path to $(4,0)$ | **VERIFIED (100%)** |
| **M23** | Stencil Reveal | 3/3 hidden gems charged via $2.2$ s dwell | **VERIFIED (100%)** |
| **M24** | Target Elimination | 6/6 clean bubbles popped, bombs evaded | **VERIFIED (100%)** |
| **M25** | Lane Switcher Tap Halves | Binary screen-half taps $\to 5/5$ coins | **VERIFIED (100%)** |
| **M26** | Lane Switcher Direct Drag | Direct pointer tracking $\to 5/5$ coins | **VERIFIED (100%)** |
| **M27** | Physics Car Hills | 2400 m terrain traversal to finish flag | **VERIFIED (100%)** |
| **M28** | Dual Bridge Route | 2 sound bridges placed $\to$ bots cross | **VERIFIED (100%)** |
| **M29** | Contour Cutting | 16-point star cut via `press_move` | **VERIFIED (100%)** |
| **M30** | Character Dress-Up | 4/4 apparel items equipped on mannequin | **VERIFIED (100%)** |
| **M31** | Liquid Filling | 3/3 reaction wells filled to $100\%$ | **VERIFIED (100%)** |
| **M32** | Airplane Star Glider | 5/5 orbital stars collected in flight | **VERIFIED (100%)** |

---

## Conclusion & Architecture Reference

Every mechanic in the 2D polygon suite has been comprehensively mastered through pure URDT beacon perception and authentic device simulation. For detailed physical derivations, mathematical formulas, and coordinate matrices, consult the corresponding documentation files in `Docs/2D_Mechanics_Reports/`.
