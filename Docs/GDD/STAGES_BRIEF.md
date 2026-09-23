# Brief: stages and fails for the 2D polygon mechanics

Goal: make every mechanic of the test polygon emulate real gameplay — **2–3 stages, each a bit harder, and
failures that reset progress**. The polygon is the test bed for an autonomous AI player (URDT L3); it will play
all mechanics in one continuous run (host button "Автопрогон всех тестов").

## Framework (already in `Scripts/Core/BaseMechanic2DModule.cs` — do not modify it)

- `public override int StageCount => 3;` (or 2) — declare the number of stages.
- `CurrentStage` (1-based) — read it in your `Initialize()` override to configure the board for that stage.
- Win of a stage: call what the mechanic already calls (`NotifyCompleted()`, `CompleteMechanic()` or
  `NotifyProgress(cur, max)` reaching max). On an intermediate stage the base shows the transition, bumps
  `CurrentStage` and calls `ResetMechanic()` (→ your `Initialize()`); `IsCompleted` becomes true only after the
  last stage. `SetProgress/NotifyProgress` take the **stage-local** 0..1 progress; the base maps it to overall.
- Fail: `FailStage("причина по-русски")` — the stage progress drops to 0 and the board is rebuilt on the same
  stage via `ResetMechanic()` after 1 s. Ignored while `IsInTransition` is true.
- `protected override string GetStageInstruction(int stage)` — per-stage instruction text shown to the player
  (Russian, player-facing, like the current `_instruction`). The host displays `Instruction`.
- `IsInTransition` — ignore gameplay input while true (guard your input handlers / Update logic).

## Rules for each mechanic

1. **Stage 1 ≈ the current mechanic** (same difficulty). Stages 2 and 3 are harder in ways natural to the
   mechanic: more items/targets, extra decoys/junk, tighter timing windows, faster movers, more precise
   placement, limited moves/time, an extra rule. Increase gradually. Every stage must remain winnable by a
   competent human with mouse/touch and by honest synthetic pointer input.
2. **Fails reset progress.** Add fail conditions that make sense for the mechanic and call `FailStage(...)`:
   wrong item into a receptacle, junk/decoy used, timing miss, crash/fall, hazard touched, time limit ran out,
   out of moves, overflow. Where the mechanic previously just rejected a wrong action silently or with a
   warning, decide: a *soft* error stays a warning (e.g. dropping an item into empty space → return to origin),
   a *real mistake* becomes `FailStage`. At least one real fail condition per mechanic. Fails must be visible
   (existing warning label or a short message) — the base also logs them.
3. **Idempotent Initialize.** `Initialize()` is called on start, on every stage change and on every fail. It
   must rebuild the board for `CurrentStage` without leaking objects: destroy what earlier stages spawned (keep
   track of spawned objects), reset positions/parents of authored objects, stop coroutines/timers, reset
   counters. No ghost objects (see the M28 ghost-plank fix: an object re-parented to the canvas root while
   dragged must never survive a reset).
4. **Beacons / testability.** The AI sees objects through URDT beacons created by
   `Urdt2DBeaconUtility.InstrumentHierarchy` (called in base `Initialize`). Create extra objects by cloning an
   existing authored object of the same kind (`Instantiate(template, template.parent)`), give them stable,
   distinct names following the existing pattern (`Item_Apple_3`, `Target_4`…) — never duplicate names. Expose
   new gameplay state as **public read-only properties** on the gameplay components (they appear to the AI as
   `game.*`). Input must stay on EventSystem handlers / Input System (no `UnityEngine.Input`, no `OnMouse*`).
5. **Code only.** Prefabs are generated/serialized; do not edit `.prefab`/`.unity`/`.asset` files. If you need
   parameters per stage, keep them in code (arrays indexed by stage). Keep the existing code style (Russian
   comments are fine, match the file).
6. **Do not run Unity or the Unity CLI** — several people edit in parallel; the orchestrator compiles once.
   Be careful with C# (Unity 6000.4, C# 9): check every identifier you use exists in the file or in UnityEngine /
   TMPro / UnityEngine.UI. No new packages.
7. **GDD section.** For each mechanic write `Docs/GDD/sections/<MechanicId>.md` in Russian, in the style of a
   real game design document (as a designer writes it for a team, not as a test spec): name, player fantasy /
   short pitch, goal, controls, core rules, objects, **stages table** (what changes per stage, win condition,
   limits), **fail conditions and consequences** (what resets), UI/feedback texts the player sees, edge cases.
   Do **not** mention beacons, test ids, URDT, code class names or implementation details — only what a designer
   would write. It must match what you implemented exactly. Use
   `Docs/2D_Mechanics_Reports/<MechanicId>.md` only as background.

## Report back (short)

Per mechanic: stages (one line each), fail conditions, new public properties, spawned-object handling, anything
you were unsure about or could not make winnable. List every file you changed.
