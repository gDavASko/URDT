# Tutor API Reference

This reference is for AI agents implementing KBPro module tutorials. Keep it aligned with
`kbpro-tutorsystem` source code and the main rule: module tutorials are optional observers.

## Required Namespaces

```csharp
using System;
using System.Collections.Generic;
using DG.Tweening;      // PathType only when path tutors need it.
using KBP.CORE;         // Tutor events, tutor strategies, tutor objects, LogicSystem, EventBus.
using UnityEngine;
```

Do not add local `DOVirtual.DelayedCall` schedulers for standard tutor delay or idle repeat.
`TutorSystem` owns those timings through `TutorTimingParameters`.

Do not implement new attract-attention animation components when an existing `AbstractIdleEffect`
can be configured. For a jumping/pulsing tool hint, configure `JumpingIdleEffect` or another
existing idle effect on the relevant tutor object and call `IdleEffect.PlayIdleEffect(...)`.

## EventBus Contract

```csharp
EventBus<EventStartTutor>.Raise(new EventStartTutor
{
    Type = typeof(TutorDragToTargets),
    TutorParameters = parameters,
    TutorTimingParameters = customTiming // optional
});

EventBus<EventPauseTutor>.Raise(new EventPauseTutor());
EventBus<EventContinueTutor>.Raise(new EventContinueTutor());
EventBus<EventRestartTutor>.Raise(new EventRestartTutor());
EventBus<EventStopTutor>.Raise(new EventStopTutor());
```

Use these events from a module tutor `LogicSystem` only. Gameplay systems should publish gameplay
state and remain independent from the tutor integration.

## Timing

```csharp
var timing = new TutorTimingParameters
{
    TutorPlayingDelay = 8f,
    TutorPlayingInterval = 1.5f,
    IdleEffectDelay = 2f,
    IdleEffectInterval = 2f,
};
```

Prefer the global `TutorConfig.TimingParameters`. Pass custom timing only for a justified
stage-specific cadence. Do not create a separate module config only to duplicate these standard
timing fields.

## Strategy Selection

| Strategy | Parameters | Tutor objects | Use |
| --- | --- | --- | --- |
| `TutorClick` | `TutorClickParameters` | `BaseTutorObject` | Click or tap target. |
| `TutorDragToTargets` | `TutorDragParameters` | `DragToTargetsTutorObject` | Drag object to one of its target transforms. |
| `TutorDragByPath` | `TutorDragByPathParameter` | `DragByPathTutorObject` | Drag along a configured `TutorPath`. |
| `TutorDragByBazierPath` | `TutorDragByBezierPathParameter` | `DragByBezierPathTutorObject` | Bezier spline motion. |
| `TutorDragRect` | `TutorDragRectParameters` | `DragRectTutorObject` | Rectangular drag gesture. |
| `TutorMovePointer` | `TutorMovePointerParameters` | none | Move finger from one transform to another. |
| `TutorTracking` | `TutorTrackingParameters` | `BaseTutorObject` | Keep finger tracking a moving target. |
| `TutorEmpty` | `EmptyTutorParameters` | none | Callback-only custom flow. |

Always verify callback names in the current parameter type before coding. Stop callback names are
not fully uniform across all parameter classes.

## Drag To Targets Example

```csharp
private void StartTransferTutor()
{
    var parameters = new TutorDragParameters
    {
        TutorObjects = new List<DragToTargetsTutorObject> { _tutorObject }
    };

    EventBus<EventStartTutor>.Raise(new EventStartTutor
    {
        Type = typeof(TutorDragToTargets),
        TutorParameters = parameters
    });
}
```

`DragToTargetsTutorObject` should be configured in the prefab with the real drag transform and real
target transforms. Do not create placeholder target visuals in code.

## Drag By Path Example

```csharp
private void StartPathTutor(Vector3 start, Vector3 end)
{
    var path = new TutorPath { Duration = 1.5f };
    path.SetPath(new[] { start, end });

    _pathTutorObject.SetPaths(new[] { path });
    _pathTutorObject.DragTransform.position = path.PositionPath[0];

    var parameters = new TutorDragByPathParameter
    {
        TutorObjects = new List<DragByPathTutorObject> { _pathTutorObject },
        IgnoreDraggablePosition = true,
        PathType = PathType.Linear,
        OnBeforeStartPlayingTutor = ShowGhostView,
        OnEndPlayingTutor = HideGhostView
    };

    EventBus<EventStartTutor>.Raise(new EventStartTutor
    {
        Type = typeof(TutorDragByPath),
        TutorParameters = parameters
    });
}
```

The tutor hand is created and animated by `TutorFinger` inside `TutorSystem`; do not create a hand
manually.

## Live Anchor Paths For Moving Tools

Never cache a tool's world position before its intro animation has finished. Instruments are often
moved from an offscreen paw position into their gameplay position, so an early-cached coordinate
makes the tutorial start offscreen.

- Create a start-anchor GameObject as a child of the real tool/view hierarchy.
- Create target, sweep, and optional middle anchors as children of the relevant target-area object.
- Keep references to those `Transform` anchors in the stage GameComponent.
- In `OnBeforeStartPlayingTutor`, read the anchors' current world positions, create a fresh
  `TutorPath`, call `SetPaths`, and place the ghost at the current start anchor.
- `TutorDragByPath` enables only `DragTransform.gameObject`. Keep every parent of `DragTransform`
  active; only the `DragTransform` object itself should start inactive.
- For Spine ghosts, initialize the cloned skeleton and apply the approved translucent alpha before
  raising `EventStartTutor`.

Apply this again after `EventContinueTutor`: every playback resolves current anchors instead of
reusing coordinates captured before the player or intro animation moved the tool.

### Visual-Only Ghost Contract

The ghost is not another gameplay object. Build a fresh hierarchy containing only:

- Transform hierarchy required to preserve the visual layout;
- SpriteRenderer/SpriteMask components, or mesh renderer/filter data;
- the required Spine visual component and renderer data.

Never clone the complete instrument prefab and merely disable its behaviours. The resulting ghost
must not contain colliders, draggable/input logic, gameplay MonoBehaviours, module components,
particles, audio, physics, or service references.

### Default Bounded Path Shape

For a normal tool-to-work-area hint, resolve live start and target positions immediately before
playback and generate a linear path:

`start -> 33% -> 66% -> target -> short sweep near target -> target`

Use `PathType.Linear`. The sweep anchor must be a small child offset of the target-area object.
Do not introduce an arbitrary elevated middle point and do not use Catmull-Rom smoothing for this
shape: spline overshoot can send the tutor outside the viewport.

`TutorDragByPath.TutorObjects` is the pool used by both the idle-effect loop and the ghost-hand
drag demonstration. For a single fixed target, pass a static one-element list. For a multi-target
tutorial where idle must play on all targets but the guided drag must demonstrate only the valid
one(s), use the dynamic-list pattern below instead of a second parallel loop.

## Multi-Target Dynamic TutorObjects

Some tutorials must play the idle/attract effect on every uncompleted target during the idle phase,
yet demonstrate the drag on only the currently valid target(s) (for example, a diagnosis screen
where all tools should attract attention but only the one tool valid for the current step should be
dragged). Do not run two separate loops for this. Pass one mutable `TutorObjects` list and mutate
the SAME list instance inside the parameter callbacks, driven by a "which target is valid now"
query on a gameplay system:

- `TutorDragByPath.TutorPlay` invokes `OnBeforeStartPlayingTutor` BEFORE it picks the random drag
  object (and before the empty-list guard), invokes `OnStartPlayingTutor` after the object is
  picked, and invokes `OnEndPlayingTutor` after the drag path completes.
- The built-in idle loop plays the idle effect on every object currently in `TutorObjects`, so a
  full list makes all uncompleted targets attract attention during idle.
- `EventPauseTutor` / `EventContinueTutor` keep the current tutor and replay the loop against the
  SAME `TutorObjects` list reference, so mutating that one list in callbacks survives pause/continue.

Flow:

1. On init, cache every target's `DragByPathTutorObject` and hold a reference to the gameplay
   system that answers which target(s) are valid now.
2. On gameplay start, fill the list with all uncompleted targets, subscribe the callbacks, and
   raise `EventStartTutor` with `TutorDragByPath`.
3. In `OnBeforeStartPlayingTutor` (after idle, before the drag pick): clear the list and add only
   the currently valid target(s) -> the ghost hand drags only those.
4. In `OnEndPlayingTutor` (after the drag): clear and refill with all uncompleted targets -> the
   next idle phase covers all remaining targets again.
5. On target grab raise `EventPauseTutor`; on release, refill with all uncompleted targets first,
   then raise `EventContinueTutor`, so the resumed idle phase covers all targets.
6. Raise `EventStopTutor` once the taught (valid) target is completed (and in `Dispose`).

For a single fixed target this degenerates to a static one-element list with no callback mutation:
prefer that simpler form and only reach for the dynamic list when idle must cover many targets.

```csharp
public sealed class ExampleMultiTargetDragTutorSystem : LogicSystem
{
    [InjectComponent] private ExampleMultiTargetTutorComponent _component = null;

    private ExampleFlowSystem _flowSystem;
    private ExampleTargetsSystem _targetsSystem; // answers which target is valid now

    private readonly List<DragByPathTutorObject> _tutorObjects = new();
    private TutorDragByPathParameter _tutorParameters;

    [InjectSystems]
    public void Construct(ExampleFlowSystem flowSystem, ExampleTargetsSystem targetsSystem)
    {
        _flowSystem = flowSystem;
        _targetsSystem = targetsSystem;
    }

    public override void Initialize()
    {
        _flowSystem.OnGameplayStarted += StartTutor;
        _targetsSystem.OnTargetGrabbed += PauseTutor;
        _targetsSystem.OnTargetReleased += ContinueTutor;
        _targetsSystem.OnTargetCompleted += HandleTargetCompleted;
        base.Initialize();
    }

    private void StartTutor()
    {
        RefillWithUncompletedTargets(); // idle phase: all uncompleted targets attract attention

        _tutorParameters = new TutorDragByPathParameter
        {
            TutorObjects = _tutorObjects, // one instance, mutated in callbacks
            OnBeforeStartPlayingTutor = NarrowToValidTargets,
            OnEndPlayingTutor = RefillWithUncompletedTargets
        };

        EventBus<EventStartTutor>.Raise(new EventStartTutor
        {
            Type = typeof(TutorDragByPath),
            TutorParameters = _tutorParameters
        });
    }

    // Idle phase: every uncompleted target attracts attention.
    private void RefillWithUncompletedTargets()
    {
        _tutorObjects.Clear();
        foreach (var target in _targetsSystem.Targets)
        {
            if (!target.IsCompleted)
            {
                _tutorObjects.Add(target.TutorObject);
            }
        }
    }

    // Fires after idle, before the ghost hand picks a drag object.
    // Guided drag must demonstrate only the currently valid target(s).
    private void NarrowToValidTargets()
    {
        _tutorObjects.Clear();
        foreach (var target in _targetsSystem.Targets)
        {
            if (!target.IsCompleted && _targetsSystem.IsValidNow(target))
            {
                _tutorObjects.Add(target.TutorObject);
            }
        }
    }

    private void PauseTutor(ExampleTarget target)
    {
        EventBus<EventPauseTutor>.Raise(new EventPauseTutor());
    }

    private void ContinueTutor(ExampleTarget target)
    {
        RefillWithUncompletedTargets(); // guarantee all targets before resuming idle
        EventBus<EventContinueTutor>.Raise(new EventContinueTutor());
    }

    private void HandleTargetCompleted(ExampleTarget target)
    {
        if (_targetsSystem.IsTaughtTarget(target)) // the valid target this tutorial teaches
        {
            EventBus<EventStopTutor>.Raise(new EventStopTutor());
        }
    }

    public override void Dispose()
    {
        EventBus<EventStopTutor>.Raise(new EventStopTutor());
        _flowSystem.OnGameplayStarted -= StartTutor;
        _targetsSystem.OnTargetGrabbed -= PauseTutor;
        _targetsSystem.OnTargetReleased -= ContinueTutor;
        _targetsSystem.OnTargetCompleted -= HandleTargetCompleted;
        _tutorParameters = null;
        base.Dispose();
    }
}
```

This is composition of existing APIs, not a reason to modify `kbpro-tutorsystem` or to add a custom
idle-animation class.

## Correct Module Pattern

```csharp
public sealed class ExampleTransferTutorSystem : LogicSystem
{
    [InjectComponent] private ExampleTutorComponent _component = null;

    private ExampleFlowSystem _flowSystem;
    private DragLogicSystem _dragLogicSystem;
    private DragToTargetsTutorObject _tutorObject;

    [InjectSystems]
    public void Construct(ExampleFlowSystem flowSystem, DragLogicSystem dragLogicSystem)
    {
        _flowSystem = flowSystem;
        _dragLogicSystem = dragLogicSystem;
    }

    public override void Initialize()
    {
        _tutorObject = _component.TutorObject;
        _flowSystem.OnToolAvailable += StartTutor;
        _dragLogicSystem.OnDragStartAction += PauseTutor;
        _dragLogicSystem.OnDragItemFree += HandleDragFinished;
        base.Initialize();
    }

    private void StartTutor()
    {
        var parameters = new TutorDragParameters
        {
            TutorObjects = new List<DragToTargetsTutorObject> { _tutorObject }
        };

        EventBus<EventStartTutor>.Raise(new EventStartTutor
        {
            Type = typeof(TutorDragToTargets),
            TutorParameters = parameters
        });
    }

    private void PauseTutor(DraggableItemBase item)
    {
        EventBus<EventPauseTutor>.Raise(new EventPauseTutor());
    }

    private void HandleDragFinished(DragEndParameters parameters)
    {
        if(parameters.IsSuccess)
        {
            EventBus<EventStopTutor>.Raise(new EventStopTutor());
            return;
        }

        EventBus<EventContinueTutor>.Raise(new EventContinueTutor());
    }

    public override void Dispose()
    {
        EventBus<EventStopTutor>.Raise(new EventStopTutor());
        _flowSystem.OnToolAvailable -= StartTutor;
        _dragLogicSystem.OnDragStartAction -= PauseTutor;
        _dragLogicSystem.OnDragItemFree -= HandleDragFinished;
        base.Dispose();
    }
}
```

The flow system does not inject `ExampleTransferTutorSystem`. It only exposes gameplay-domain
events. Removing the tutor system from `_systems` must not break the gameplay stage.

## Prefab And Component Guidance

- GameComponents hold serialized references only: tutor object, ghost view, start point, target
  anchors, and optional stage-specific visual references.
- Tutor objects are configured in the prefab and reused by strategy parameters.
- Idle effects are configured on tutor objects and played through the central TutorSystem loop.
- For module-specific attention pulses outside the central loop, still call the configured
  `BaseTutorObject.IdleEffect.PlayIdleEffect(...)`; never duplicate the effect animation in a new
  component if an existing `AbstractIdleEffect` fits.
- Register the tutor `LogicSystem` in module `_systems` when the tutorial should run.
- Prefer Inspector or existing prefab patterns for wiring serialized references.

## Anti-Patterns

- Gameplay flow injects a concrete tutor system and calls `StartTutorial`, `StopTutorial`,
  `OnInstrumentTaken`, or `OnInstrumentReturned`.
- Tutor code is required for the gameplay step to complete.
- Module tutor system owns delay, repeat, or idle loops with local tweens.
- A multi-target drag tutorial runs a second parallel attract loop, or passes all targets without
  narrowing to the valid target(s) inside `OnBeforeStartPlayingTutor`, so the ghost hand drags a
  random target.
- Module tutor system duplicates `TutorState` with local boolean state machines.
- Separate config is created only for standard tutor timing fields.
- Tutor hand is created manually instead of letting `TutorSystem` own `TutorFinger`.
- `EventStartTutor` has no matching `EventStopTutor` on success, stage completion, and `Dispose`.
- Custom `JumpingIdleEffect` clones, group-jump components, or local DOTween attention animations
  are added while `JumpingIdleEffect` / `AbstractIdleEffect` would cover the visual behavior.
- Core files under `Assets/KBPro/kbpro-tutorsystem/**` are changed to satisfy a single module's
  tutorial requirement without explicit approval for a platform-level change.
