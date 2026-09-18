# Module orchestrator class development rules (Module Orchestrator Rules)

The module class (a subclass of `AbstractGameModule`) is the lifecycle orchestrator. It must not contain business logic — its job is to coordinate systems and pass data.

## 1. Lifecycle and asynchrony

Follow the strict order of framework `base` method calls:

### `Initialize()`
1. Get references to local C# systems via `GetSystem<T>()`.
2. Subscribe to the completion of key logic systems.
3. Read the scenario input parameters via `RuntimeContext.ModuleInput` (if Module I/O is used).
4. Call `base.Initialize()` **strictly at the very end**. This will trigger DI injections.

### `Startable()`
1. Call `base.Startable()` **strictly first**. This will trigger the `Start()` logic in all registered systems.
2. Open the module's local HUD interface via EventBus:
   `EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.GAME));`

### `OnComplete()`
1. Hide the module's HUD interface:
   `EventBus<EventHideComponent>.Raise(new EventHideComponent(Constants.UICOMPONENT.GAME));`
2. Write the output results to `RuntimeContext.ModuleOutput` (when using Module I/O), or save the results to `IStatisticService` (when following the legacy method).
3. Call `base.OnComplete()` **strictly last**.

### `Dispose()`
1. Unsubscribe from all global EventBus events.
2. Call `.Dispose()` on every created `LazySrv<T>` instance.
3. Call `base.Dispose()` **strictly last** (this will call `Dispose()` on all LogicSystems and destroy the module's GameObject).

## 2. ISuspendable support

The module orchestrator must implement the `ISuspendable` interface to correctly support pauses in scenarios (dialogues, tutorials, cutscenes):
- `Suspend(bool hide)`: Sets the `IsSuspended = true` flag and, if needed, hides the module's GameObject (`gameObject.SetActive(false)`).
- `Resume()`: Resets the `IsSuspended = false` flag and returns the GameObject to the screen.
