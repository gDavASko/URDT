---
name: kbpro-tutor-builder
description: "Tutorial observers via TutorSystem EventBus. Triggers: tutorial, tutor, hint, ghost hand, guided drag, idle hint, attract attention."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
required_reading:
  - references/tutor-system-cheatsheet.md
  - references/tutor-api.md
known_risks:
  - Gameplay flow depends on a concrete tutor system. Tutor systems must be optional observers.
  - A module reimplements TutorSystem delay, repeat, idle-effect, pause, or stop behavior with local tweens and flags.
  - Attract-attention animation is reimplemented with a custom tween component instead of using an existing `AbstractIdleEffect` such as `JumpingIdleEffect`.
  - A multi-target drag tutorial passes all targets in the `TutorObjects` list for the idle phase but fails to narrow the list to the currently valid target inside `OnBeforeStartPlayingTutor`, so the ghost hand drags a random target.
  - Core `kbpro-tutorsystem` files are modified to satisfy a module-specific tutorial behavior instead of composing existing APIs in the module.
  - "`EventStartTutor` has no reliable `EventStopTutor` path on success, stage completion, and `Dispose`."
  - A tutor system lives in a GameComponent instead of a LogicSystem.
  - Callback or parameter names are guessed instead of checked against the current `kbpro-tutorsystem` source.
  - Tutor objects or target objects are placeholders rather than real scene anchors or approved silhouettes.
  - A guided path caches world positions before the tool's intro animation moves it on screen, so the tutor hand starts offscreen or detached from the visible tool.
  - A ghost silhouette's child is enabled while one of its parent objects remains inactive, so the hand moves without a visible tool.
  - A ghost silhouette duplicates the whole gameplay prefab and carries colliders, drag logic, particles, audio, module components, or other non-visual behaviour.
  - A path uses a high or arbitrary middle control point, so Catmull-Rom overshoot sends the ghost outside the visible gameplay area.
---

# KBPro Tutor Builder

## Purpose

Build module-level tutorial integrations that observe gameplay and send commands to the global
`TutorSystem`. This skill covers guided drag/click hints, idle hints, and attract-attention behavior
through the built-in tutor strategies and EventBus commands.

## Core Principle

A tutorial system is an optional observer, not part of the gameplay dependency chain.

Gameplay flow systems may publish domain events such as `OnStageReady`, `OnToolAvailable`, or
`OnTargetChanged`. A tutor `LogicSystem` subscribes to those signals and to input/drag events, then
raises `EventStartTutor`, `EventPauseTutor`, `EventContinueTutor`, or `EventStopTutor`.

If the tutor system is removed from a module, gameplay must still work.

## Mandatory Workflow

1. Read `references/tutor-system-cheatsheet.md` and `references/tutor-api.md`.
2. Check the current `kbpro-tutorsystem` source for the exact strategy, parameter, and callback names.
3. Find a recent working project example of the same tutor type.
4. Keep gameplay flow independent from the concrete tutor system.
5. Implement the tutor integration as a `LogicSystem` that subscribes in `Initialize` and unsubscribes in `Dispose`.
6. Use `EventStartTutor` with the correct strategy and `ITutorParameters`.
7. Use `EventPauseTutor` when the player starts doing the taught action.
8. Use `EventContinueTutor` when the player aborts or releases without success.
9. Use `EventStopTutor` on success, stage completion, and `Dispose`.
10. Let `TutorSystem` own standard delays, repeats, idle effects, cancellation, and pause blockers.
11. For attract-attention animation, prefer configured `AbstractIdleEffect` components already present in `kbpro-tutorsystem` (`JumpingIdleEffect`, `PulsingIdleEffect`, etc.). Do not write a new DOTween animation component unless no existing idle effect matches and the reason is documented.
12. Choose the tutor-object shape by target count. Single fixed target: pass a static one-element `TutorObjects` list, no callback mutation. Many targets where idle/attract must play on all uncompleted targets but the guided drag must demonstrate only the currently valid target(s): do not split it into two parallel loops. Pass one mutable `TutorObjects` list that starts with all uncompleted targets (idle plays on all of them), narrow it to the valid target(s) inside `OnBeforeStartPlayingTutor`, and refill it with all uncompleted targets inside `OnEndPlayingTutor` (and before every `EventContinueTutor`). See `references/tutor-api.md` -> "Multi-Target Dynamic TutorObjects".
13. Build guided paths from live `Transform` anchors, not cached world-space coordinates. Put the start anchor under the real tool/view that can move during intro animations, and put target/sweep anchors under the relevant target-area object. Refresh the path inside `OnBeforeStartPlayingTutor` so every playback uses current anchor positions.
14. Validate the entire ghost hierarchy. `TutorDragByPath` activates only `DragTransform.gameObject`; every parent of `DragTransform` must already be active, while `DragTransform` itself starts inactive. Verify that the ghost renderer/Spine skeleton is initialized and visible.
15. A ghost is a visual-only clone. Copy only the required SpriteRenderer set, mesh render data, or Spine visual component and its transform hierarchy. Never retain colliders, draggable logic, gameplay MonoBehaviours, particles, audio, module components, or a nested copy of the complete tool prefab.
16. Default guided paths must be predictable and bounded: live start, straight-line interpolation toward the live target, target, then an optional short sweep near the target. Prefer `PathType.Linear`; do not add arbitrary elevated control points or use smoothing that can overshoot beyond the play area.

## Do

- Use `TutorDragToTargets` for normal "drag this object to target" hints.
- Use `TutorDragByPath` for path demonstrations.
- Use `TutorClick` for click/tap hints.
- Use `TutorTracking` when the target moves and the finger must follow it.
- Use `BaseTutorObject.IdleEffect.PlayIdleEffect(...)` / `StopIdleEffect()` for attract-attention pulses when a configured idle effect exists.
- Configure `BaseTutorObject` / `DragToTargetsTutorObject` / `DragByPathTutorObject` in the prefab.
- Create explicit path-anchor GameObjects inside the real moving tool and target-area hierarchies; resolve their positions immediately before playback.
- Construct ghost objects from visual components only and validate that every non-Transform component belongs to the approved visual set.
- Build the runtime path as start -> linear approach -> target -> short local sweep -> target.
- Use `TutorTimingParameters` on `EventStartTutor` only when the default `TutorConfig` timing is not enough.
- Keep GameComponents as reference holders only.
- Stop the tutor in `Dispose` before `base.Dispose()`.

## Do Not

- Do not inject a concrete tutor system into gameplay flow so the flow can call `StartTutorial`,
  `OnInstrumentTaken`, `OnInstrumentReturned`, or `StopTutorial`.
- Do not build a local scheduler with `DOVirtual.DelayedCall` for ordinary tutor delay or idle repeat.
- Do not split "attention tutor" and "drag tutor" manually when `TutorSystem` idle-effect timing covers it.
- Do not add a separate ScriptableObject config only for standard tutor timings.
- Do not duplicate `TutorSystem` state with `_active`, `_interacting`, `_isIdlePlaying`, or similar flags unless
  a real gameplay-specific edge case is documented.
- Do not create or animate the tutor hand manually; `TutorFinger` is owned by `TutorSystem`.
- Do not create custom "group jumping", "attention shake", or local DOTween idle-effect components when existing `AbstractIdleEffect` implementations can be configured on tutor objects.
- Do not modify files under `Assets/KBPro/kbpro-tutorsystem/**` for a module-specific tutorial. If a platform gap is suspected, stop and report the gap separately.
- Do not build a second, parallel idle/attract loop for a multi-target drag tutorial. Passing all uncompleted targets into `TutorDragByPath` is correct for the idle phase; narrow the guided drag phase to the currently valid target(s) inside `OnBeforeStartPlayingTutor` instead of running a separate module-driven attention loop.
- Do not pass all targets and then forget to narrow the list inside `OnBeforeStartPlayingTutor` — the ghost hand would drag a random target.
- Do not serialize an initial world-space start point for a tool that is moved on screen by a paw, popup, zoom, tween, or intro animation.
- Do not deactivate a parent of `DragTransform`; enabling only the child will not make the ghost silhouette visible.
- Do not instantiate the complete gameplay prefab as a ghost, even if its unwanted components are disabled afterward.
- Do not use arbitrary world-space arc points or Catmull-Rom smoothing for a basic tool-to-area hint.

## Reference Example Shape

The preferred shape is the current `CariesAnesthesiaTransferTutorialSystem` pattern:

- `Construct(flowSystem, dragLogicSystem)`.
- `Initialize` caches the tutor object and subscribes to flow/drag events.
- Flow exposes a gameplay-domain event such as `OnStartAnesthesiaLogic`.
- Tutor system starts `TutorDragToTargets` via `EventStartTutor`.
- Drag start raises `EventPauseTutor`.
- Failed drag raises `EventContinueTutor`.
- Successful drag raises `EventStopTutor`.
- `Dispose` raises `EventStopTutor`, unsubscribes, then calls `base.Dispose()`.

That shape covers a single fixed target (static one-element `TutorObjects` list). For a multi-target drag tutorial where idle/attract must cover all uncompleted targets but the guided drag must demonstrate only the currently valid target(s), use the dynamic-list variant: one mutable `TutorObjects` list, narrowed in `OnBeforeStartPlayingTutor` and refilled in `OnEndPlayingTutor`. Full example in `references/tutor-api.md` -> "Multi-Target Dynamic TutorObjects".

## Review Checklist

- [ ] Removing the tutor `LogicSystem` from `_systems` does not break gameplay.
- [ ] Gameplay flow does not hold a concrete tutor system dependency.
- [ ] The tutor system observes gameplay/input events and only sends TutorSystem commands.
- [ ] Standard timing and idle behavior are delegated to `TutorSystem`.
- [ ] Attract-attention animation uses an existing configured `AbstractIdleEffect` (`JumpingIdleEffect` when a jump is needed), not a custom tween clone.
- [ ] Multi-target tutorials narrow the guided drag to the currently valid target(s) inside `OnBeforeStartPlayingTutor`, and the idle phase / `OnEndPlayingTutor` / pre-continue refill restore all uncompleted targets; there is no second parallel attract loop. Single-target tutorials use a static one-element list.
- [ ] No module task changes `Assets/KBPro/kbpro-tutorsystem/**` unless the user explicitly approved a platform-level change.
- [ ] All subscriptions are removed in `Dispose`.
- [ ] `EventStopTutor` is raised on success and in `Dispose`.
- [ ] Strategy parameter types and callback names compile against current source.
- [ ] Prefab tutor objects are real configured anchors, not invented placeholders.
- [ ] Path start comes from a live anchor under the post-animation tool hierarchy and is refreshed in `OnBeforeStartPlayingTutor`.
- [ ] Target/sweep anchors live under the relevant target-area hierarchy rather than being unexplained world coordinates.
- [ ] The ghost silhouette is visible: all parents are active, `DragTransform` starts inactive, and its renderer/Spine data is valid.
- [ ] The ghost contains only Transform plus required sprite/mesh/Spine visual components; it contains no collider, particle system, audio, draggable, gameplay, or module component.
- [ ] The path is bounded and readable: live start, near-straight approach, live target, and only a small target-area sweep; it cannot overshoot the viewport.
