# Antipatterns in KBPro module development

This reference describes forbidden programming and design practices that violate the KBPro platform's architectural contract, leading to memory leaks, lifecycle bugs, or degraded performance.

---

## 1. Using Update() inside a LogicSystem

### ❌ WRONG
```csharp
public class CleaningFlowSystem : LogicSystem
{
    // Unity calls this method automatically if the class is a MonoBehaviour,
    // but LogicSystem is a pure C# class, so Update() will NOT work.
    private void Update()
    {
        // The logic will not execute
    }
}
```

### ❓ Why this is bad
`LogicSystem` does not inherit from `MonoBehaviour`. Unity never interacts with this class directly. Attempting to write `Update()` results in logic that silently doesn't run, with no compilation errors.

### ✅ CORRECT
Implement the `IUpdatable` interface from the `KBP.CORE` namespace. The `OnUpdate()` method will be automatically called by the module's centralized timer every frame.
```csharp
using KBP.CORE;

public class CleaningFlowSystem : LogicSystem, IUpdatable
{
    public void OnUpdate()
    {
        // Logic that runs every frame
    }
}
```

---

## 2. Calling ServiceLocator.GetService<T>() directly during initialization

### ❌ WRONG
```csharp
public class CleaningFlowSystem : LogicSystem
{
    // Getting services directly via the static locator in class fields is forbidden
    private ISoundSystem _sound = ServiceLocator.GetService<ISoundSystem>();
}
```

### ❓ Why this is bad
- Violates Dependency Injection principles.
- A static call at class construction time can happen before the `ServiceLocator` has been populated with services, causing a `NullReferenceException` at load time.
- Makes unit testing harder, since it tightly couples the code to the global container.

### ✅ CORRECT
Use the `LazySrv<T>` wrapper, which lazily resolves the dependency on the first access to the `.Value` property and guarantees initialization safety. Don't forget to call `.Dispose()` at the end of the lifecycle.
```csharp
using KBP.CORE;

public class CleaningFlowSystem : LogicSystem
{
    private LazySrv<ISoundSystem> _sound = new LazySrv<ISoundSystem>();

    private void PlayCleanSound()
    {
        _sound.Value.PlaySound("clean", isLoop: false, volume: 1f, MixerType.Sound);
    }

    public override void Dispose()
    {
        _sound.Dispose();
        base.Dispose();
    }
}
```

---

## 3. Violating the call order of lifecycle base methods

### ❌ WRONG
```csharp
public override void Initialize()
{
    // base.Initialize() triggers [InjectComponent] dependency injections.
    // Calling it at the start means systems will try to work
    // with an environment that isn't set up yet.
    base.Initialize();
    
    _flowSystem = GetSystem<CleaningFlowSystem>(); // Too late
}
```

### ❓ Why this is bad
The lifecycle order is strictly defined:
- `base.Initialize()` triggers reflection and injects `[InjectComponent]` components into systems. All `GetSystem<T>()` calls and subscriptions must happen **BEFORE** this call.
- `base.Startable()` activates the logic systems. All of the module's own startup actions must happen **AFTER** the systems are activated.

### ✅ CORRECT
```csharp
public override void Initialize()
{
    _flowSystem = GetSystem<CleaningFlowSystem>();
    _flowSystem.OnComplete(OnFlowComplete);

    base.Initialize(); // Strictly LAST
}
```

---

## 4. Using GameObject.Find and FindObjectOfType

### ❌ WRONG
```csharp
public class CleaningFlowSystem : LogicSystem
{
    public override void Initialize()
    {
        // Searching for scene objects by type or name is forbidden
        var spot = Object.FindObjectOfType<DirtySpotComponent>(); 
        base.Initialize();
    }
}
```

### ❓ Why this is bad
- Extremely inefficient (iterates the entire scene).
- May find objects from other loaded scenes or modules.
- Violates module isolation. A module must operate only on the components that live inside its own prefab.

### ✅ CORRECT
Link components via the `_componentProviders` array in the module inspector and inject them with the `[InjectComponent]` attribute in logic systems.
```csharp
public class CleaningFlowSystem : LogicSystem
{
    // Automatic injection of the component from the module prefab
    [InjectComponent] private DirtySpotComponent _spotComponent = null;

    public override void Initialize()
    {
        _spotComponent.ShowDirt();
        base.Initialize();
    }
}
```

---

## 5. Direct references between modules

### ❌ WRONG
```csharp
public class TrainCleaningModule : AbstractGameModule
{
    // Referencing other module classes directly is forbidden
    [SerializeField] private RunGameModule _runModule; 
}
```

### ❓ Why this is bad
Creates tight coupling between modules. Modules must be fully autonomous, interchangeable "black boxes". They should communicate only through `EventBus`, shared `ScenarioGraph` parameters, or a central manager.

### ✅ CORRECT
If you need access to the currently active module, do it through a manager, or use `EventBus` to pass global events.

---

## 6. Hardcoding strings for UI components and events

### ❌ WRONG
```csharp
// Using string literals for key identifiers is forbidden
EventBus<EventShowComponent>.Raise(new EventShowComponent("GameHUDWindow"));
```

### ❓ Why this is bad
A typo in the string literal will lead to a hard-to-find runtime bug that the compiler cannot detect.

### ✅ CORRECT
Use the centralized `Constants.cs` class, which is auto-generated based on the `SOConstantsContainer` settings.
```csharp
EventBus<EventShowComponent>.Raise(new EventShowComponent(Constants.UICOMPONENT.GAME));
```
