# KBPro TutorSystem Cheatsheet

Use this cheatsheet before implementing or reviewing module tutorial logic.

## System Boundary

`TutorSystem` owns:

- `TutorFinger` lifecycle and animations;
- strategy creation through `TutorComponentAccessor`;
- `EventStartTutor`, `EventPauseTutor`, `EventContinueTutor`, `EventRestartTutor`, `EventStopTutor`;
- timing through `TutorTimingParameters`;
- idle effect playback;
- pause blockers for settings and parent-control UI;
- cancellation through `CancellationTokenSource`.

Module tutor `LogicSystem` owns only:

- observing gameplay/input state;
- selecting tutor objects and targets;
- building the correct `ITutorParameters`;
- raising TutorSystem EventBus commands;
- for multi-target drag: swapping the tutor-object list inside `OnBeforeStartPlayingTutor` (narrow to the valid target(s)) and `OnEndPlayingTutor` (refill all uncompleted targets), mutating one list instance;
- unsubscribing and stopping in `Dispose`.

## Main Events

| Event | Use from module tutor system |
| --- | --- |
| `EventStartTutor` | Start or replace the current tutor. Requires `Type` and `TutorParameters`; optional `TutorTimingParameters`. |
| `EventPauseTutor` | Player starts performing the taught action; the tutor should disappear but remain resumable. |
| `EventContinueTutor` | Player releases/aborts without success; resume the current tutor. Resume replays against the same `TutorObjects` list instance, so for multi-target tutorials refill that list with all uncompleted targets before raising this event. |
| `EventRestartTutor` | Interrupt and immediately continue the current tutor. Rare in module code. |
| `EventStopTutor` | Taught action succeeded, stage ended, or system is disposing. |

## Strategy Map

| Strategy | Parameters | Objects | Typical use |
| --- | --- | --- | --- |
| `TutorClick` | `TutorClickParameters` | `BaseTutorObject` | Click or tap target. |
| `TutorDragToTargets` | `TutorDragParameters` | `DragToTargetsTutorObject` | Drag object to one of its configured target transforms. |
| `TutorDragByPath` | `TutorDragByPathParameter` | `DragByPathTutorObject` | Drag object/finger along a configured path. |
| `TutorDragByBazierPath` | `TutorDragByBezierPathParameter` | `DragByBezierPathTutorObject` | Bezier spline motion. |
| `TutorDragRect` | `TutorDragRectParameters` | `DragRectTutorObject` | Rectangular drag gesture. |
| `TutorMovePointer` | `TutorMovePointerParameters` | none | Move the hand from A to B without dragging an object. |
| `TutorTracking` | `TutorTrackingParameters` | `BaseTutorObject` | Keep the hand following a moving object. |
| `TutorEmpty` | `EmptyTutorParameters` | none | Callback-only custom flow. |

## Timing

`TutorTimingParameters`:

- `TutorPlayingDelay`: first playback delay;
- `TutorPlayingInterval`: interval between main tutor playbacks;
- `IdleEffectDelay`: delay before idle effect starts during a waiting window;
- `IdleEffectInterval`: interval between idle effect repeats.

Prefer the global `TutorConfig.TimingParameters`. Pass custom timing only for a justified stage-specific cadence.

## Correct Observer Pattern

```csharp
public override void Initialize()
{
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
```

## Anti-Patterns

- Gameplay flow injects and calls a concrete tutor system.
- Tutor code is required for the game step to complete.
- Local `DOVirtual.DelayedCall` or custom tweens duplicate standard TutorSystem delays.
- Local boolean state machine duplicates `Play`, `Pause`, `Continue`, `Stop`.
- Manual idle/attention loop duplicates `PlayIdleEffectLoop`.
- A second parallel attract loop is built for a multi-target drag tutorial instead of mutating one `TutorObjects` list in `OnBeforeStartPlayingTutor` / `OnEndPlayingTutor`.
- Separate config is created only for standard 2s/8s tutor timings.
- Tutor hand is created manually instead of using `TutorFinger`.
- `EventStartTutor` lacks stop paths.

## Dispose Checklist

- Raise `EventStopTutor`.
- Unsubscribe from flow, drag, component, and EventBus events.
- Clear local references if needed.
- Call `base.Dispose()` last.
