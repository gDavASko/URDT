---
name: kbpro-module-input-builder
description: "Custom ModuleInput, visual ports, IInputUpdateAware, Clone(). Triggers: create ModuleInput, custom input port, inherit ModuleInput, PortId."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
known_risks:
  - Using mutable reference fields (like Lists or Arrays) without overriding the default shallow Clone() method.
  - Renaming field variables that are already bound in serialized ScenarioGraph assets (this breaks links; use PortId to decouple).
  - Storing state inside the ModuleInput class during runtime (input must be read-only).
---

# KBPro Module Input Builder

## Purpose

Apply this skill to design, write, and validate custom `ModuleInput` classes that act as visual input data carriers inside ScenarioGraph nodes.

## When To Use

* Writing a new gameplay module that requires parameters (e.g., speed, duration, text IDs) passed from the graph.
* Adding configuration ports to existing nodes.
* Fixing shared-state list bugs on nodes by implementing deep copy.

## Do Not Use

* For creating output parameters — use `kbpro-module-output-builder`.
* For creating general Zenject/VContainer DI properties — use `kbpro-di-expert`.

## KBPro Module Input Rules

### 1. Basic Serializable Input Class
All inputs must inherit from `ModuleInput`, be marked `[Serializable]`, and decorated with `[UnityEngine.Scripting.Preserve]` so AOT compilation does not strip them.
Refer to [BattleInput.cs](file://examples/BattleInput.cs) for a complete template:
* Declare input variables using private `[SerializeField]`.
* Decorate fields with `[PortId]`, `[PortDisplayName]`, and `[PortDescription]`.

### 2. Mandatory Deep Copy for Reference Types
> [!WARNING]
> If your class contains reference types (e.g. `List<T>`, `Dictionary<TKey,TValue>`, `Array`), you **MUST** override `Clone()` to provide a Deep Copy.
> Failing to do so shares reference instances across templates.
> Refer to [WaveSetupInput.cs](file://examples/WaveSetupInput.cs) for a complete template.

### 3. Caching and `IInputUpdateAware`
If your module caches input fields locally in `Initialize()`, implement `IInputUpdateAware` so that `Resume` and live data updates sync back to those fields correctly.
Refer to [RaceModule.cs](file://examples/RaceModule.cs) inside examples for a clean template.

---

## Workflow

1. List the variables your module needs to receive from the graph.
2. Create a class inheriting from `ModuleInput`. Add `[Serializable]` and `[Preserve]`.
3. Add fields using private `[SerializeField]` (or public if appropriate).
4. Decorate each field with `[PortId]`, `[PortDisplayName]`, and `[PortDescription]`.
5. Check if reference types are present. If yes, override `Clone()` and write a deep copy.
6. Link on the module class using `[ModuleIO]`.

## Output Format

* Show the completed `ModuleInput` class.
* If a deep copy is required, show the overridden `Clone()` block clearly.

## Test Prompts

1. "Create a custom ModuleInput named DialogInput that has a string localization key and a float text speed."
2. "Create an input class that contains a list of custom reward structures, showing the required Clone() override."
