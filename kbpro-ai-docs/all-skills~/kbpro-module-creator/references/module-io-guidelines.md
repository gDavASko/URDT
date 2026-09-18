# Data contract design rules (Module I/O Guidelines)

To pass data between ScenarioGraph nodes in the modern KBPro V2 framework, input (`ModuleInput`) and output (`ModuleOutput`) DTO classes are used.

## 1. General requirements for data classes

1. **Serializability:** Classes must be marked with the `[System.Serializable]` attribute.
2. **Linker protection (Stripping):** For mobile platforms, the `[UnityEngine.Scripting.Preserve]` attribute must be added so the Unity Linker doesn't strip the class during compilation.
3. **Inheritance:**
   - The input class must inherit from `ModuleInput`.
   - The output class must inherit from `ModuleOutput`.
4. **Port description:** Every public serialized field must be marked with the `[PortDescription("Clear description in Russian")]` attribute. This description is shown in the ScenarioGraph editor.

## 2. ModuleInput specification

The input class contains the parameters for launching the mini-game. For example:
- The identifier of the tooth being treated (`ToothId`).
- The type of tool being used (`ToolType`).
- Rotation speed or sensitivity.

*Note:* Default values for fields can be set on the input class.

## 3. ModuleOutput specification

The output class contains the results of completing the mini-game. For example:
- A flag indicating successful completion (`IsSuccess`).
- Time spent completing it (`TimeSpent`).
- The number of points or stars earned.

## 4. Code placement

Usually the `ModuleInput` and `ModuleOutput` classes are placed in the same C# file as the module's main class (or in separate `[ModuleName]Input.cs` / `[ModuleName]Output.cs` files in the same module scripts folder).
An example of the C# code is provided in the examples file `examples/module-code-examples.md`.
