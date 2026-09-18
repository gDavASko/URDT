# Logic system development rules (LogicSystem Rules)

Systems (`LogicSystem`) contain business logic, calculations, and game state (State). They are pure C# classes and do not inherit from `MonoBehaviour`.

## 1. SRP (Single Responsibility Principle)

Each system solves strictly one isolated task:
- `DrillPhysicsSystem` — calculates drilling physics and distances.
- `WashSoundsSystem` — plays washing sounds.
- Do not combine unrelated logic (input, sounds, counters) into one massive class.

## 2. System parameters and `[SerializeReference]`

Since systems are serialized polymorphically, you can declare configurable parameters (`[SerializeField] private float _minTriggerDistance`) directly inside systems. They will be configurable on the module prefab in the inspector.

## 3. Dependency Injection (DI) in systems

- **Linking to components:** Use the `[InjectComponent]` attribute to inject the needed prefab components. The framework will fill in these fields before calling `Initialize()`.
- **Global services:** For access to sounds, timers, and pooling, use `LazySrv<T>` (e.g. `private readonly LazySrv<ISoundSystem> _sound = new();`).
- **Links between systems:** If a system needs a reference to another system in the same module, inject it via the orchestrator's `Construct(...)` initialization method.

## 4. Per-frame updates (No MonoBehaviour Update)

Writing an `Update()` method in systems is forbidden. If a system needs a per-frame tick, it must implement the `IUpdatable` interface (or `IFixedUpdatable` / `ILateUpdatable`). The module will automatically subscribe the system to `TimerService` updates.

## 5. Pause support (ISuspendable)

If the module must support suspension (e.g. during dialogues), systems must implement the `ISuspendable` interface and correctly pause or resume timers, UniTasks, and Spine animations in the `Suspend()` / `Resume()` methods.

## 6. Sounds and Pooling

- **Sounds:** Played via `ISoundSystem` (LazySrv) using identifiers from `Constants.SOUNDS`.
- **Pooling:** Frequently spawned objects (splashes, sparks, droplets) are spawned via `PoolService` (`_pools.Value.Spawn(Constants.POOLS.DUST, position)`).

## 7. Memory Safety (Memory cleanup)

In a system's `Dispose()` method, it is mandatory to:
1. **Unsubscribe from all component events** (`_spotComponent.OnClicked -= OnSpotClicked`) subscribed to in `Initialize()`.
2. **Release all global services** (`_sound.Dispose()`, `_pools.Dispose()`).
3. Call `base.Dispose()` at the very end.
  *Skipping unsubscriptions leads to hard memory leaks in Unity!*
