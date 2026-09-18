# ScenarioGraph Integration

This reference describes the rules and mechanisms for wiring game modules into the visual scenario graph (**ScenarioGraph**).

---

## 1. ScenarioGraph overview

**ScenarioGraph** is a ScriptableObject representing a directed graph whose nodes are scenario steps (nodes), and whose edges are transitions and data flows.

The main node for launching gameplay is **ModuleScenarioNode**. It is responsible for the lifecycle of a specific game module, its initialization, passing input parameters, and collecting output data on completion.

---

## 2. ModuleScenarioNode structure

Each `ModuleScenarioNode` node in the graph contains the following key fields:

1. **ModuleRef (`AssetReference`)**:
   - A direct reference to the game module's Addressable prefab (e.g. `TrainCleaningModule.prefab`).
   - This is the preferred way to specify the module.
2. **Input (`ModuleInput`)**:
   - A reference to a ScriptableObject or serialized class of input parameters (ports).
   - Set via the Picker in the node inspector.
3. **Output (`ModuleOutput`)**:
   - A reference to the class of output parameters filled in by the module on completion.

Example declaration in `ModuleScenarioNode.cs` code:
```csharp
public class ModuleScenarioNode : ScenarioGraphNode, IDataSourceNode
{
    [SerializeField] private AssetReference _moduleRef;
    [SerializeReference, SubclassSelector] private ModuleInput _input;
    [SerializeReference, SubclassSelector] private ModuleOutput _output;

    public ModuleInput Input => _input;
    public ModuleOutput Output => _output;
}
```

---

## 3. Data Flow

ScenarioGraph supports dynamic data transfer between nodes via **ScenarioDataLink**.

### Linking output and input ports (Data Links)
- The output port of a preceding node (e.g. the selected tool or difficulty) can be linked to the input port of `ModuleScenarioNode`.
- At runtime, before activating the module, `ModuleStateMachine` automatically resolves all data links:
  1. Extracts values from the output snapshots (`OutputSnapshots`) of preceding nodes.
  2. Writes them into the input properties of the current module's `ModuleInput`.
  3. Passes the filled `ModuleInput` to the module's `Run(ModuleInput input)` method.

### IDataSourceNode interface implementation
The module node implements the `IDataSourceNode` interface, allowing other graph nodes to read its results:
```csharp
public object ResolvePort(string portId, Type expectedType, IDataSourceContext ctx)
{
    IReadOnlyDictionary<string, object> snapshot = ctx?.OutputSnapshots?.Get(NodeId);
    if (snapshot != null && snapshot.TryGetValue(portId, out object value))
        return value;
    return null;
}
```

---

## 4. Transition Conditions

The transition from one node to another happens when `OnComplete()` is called on the active module. In the graph this is configured using transition edges (`ScenarioTransitionData`) containing conditions (`IModuleTransitionCondition`).

### Condition types
1. **AlwaysModuleTransitionCondition**:
   - The transition always fires immediately after the node finishes working.
2. **ComparisonCondition**:
   - Compares a value from the module's output or a scenario variable against a constant (e.g. `RemainingSpots == 0` or `Score > 80`).
3. **ParameterEqualsTransitionCondition**:
   - Checks that a string parameter equals a specific value.

---

## 5. Lifecycle in the graph (Suspend & Background)

Game modules running in ScenarioGraph can be controlled by external systems (e.g. a global pause or a dialogue system). For this, the module's `AbstractGameModule` orchestrator can implement the interfaces:

- **ISuspendable**:
  - Allows temporarily pausing the module's work (stopping timers, physics, animations) and resuming it.
  ```csharp
  public interface ISuspendable
  {
      void Suspend();
      void Resume();
  }
  ```
- **IBackgroundRunnable**:
  - Allows the module to keep performing background computations (e.g. loading resources or background logic) even when it is not the active visual node on screen.
