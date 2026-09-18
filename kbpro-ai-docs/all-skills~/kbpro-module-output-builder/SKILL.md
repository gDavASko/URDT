---
name: kbpro-module-output-builder
description: "Custom ModuleOutput, output ports, Clone(). Triggers: create ModuleOutput, custom output port, inherit ModuleOutput, write output value."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
known_risks:
  - Modifying ModuleOutput properties inside transition conditions (outputs are flushed after transition evaluation).
  - Leaving reference lists inside ModuleOutput without deep-cloning in Clone() (this leaks runtime state into the snapshot store).
  - Forgetting to write outputs before calling base.OnComplete() or base.Dispose().
---

# KBPro Module Output Builder

## Purpose

Apply this skill to design, write, and validate custom `ModuleOutput` classes that carry computed runtime results from completed modules to downstream graph nodes.

## When To Use

* Writing a new gameplay module that produces outcomes (e.g. final duration, character selected, victory flags).
* Exposing module variables as graph output ports.
* Resolving state leaks in output snapshots by implementing deep copy.

## Do Not Use

* For creating input templates — use `kbpro-module-input-builder`.
* For long-lived session storage (e.g. currency, profile stats) — use global `LazySrv<IStatisticService>` instead.

## KBPro Module Output Rules

### 1. Basic Serializable Output Class
All outputs must inherit from `ModuleOutput`, be marked `[Serializable]`, and decorated with `[UnityEngine.Scripting.Preserve]` to prevent AOT stripping.
Refer to [GameRewardOutput.cs](file://examples/GameRewardOutput.cs) for a complete template:
* Class must have port decoration attributes on its serialized private fields.

### 2. Mandatory Deep Copy for Reference Types
> [!WARNING]
> If your class contains reference types (e.g. `List<T>`, `Dictionary<TKey,TValue>`, `Array`), you **MUST** override `Clone()` to provide a Deep Copy. Failing to do so shares reference instances across asset templates and active run contexts.
> Refer to [WaveSetupInput.cs](file://examples/WaveSetupInput.cs) inside examples for a deep cloning example.

### 3. Output Write and Completion Flow
Outputs must be populated in the module **before** raising completed events or invoking transition calls.
Refer to [RaceModule.cs](file://examples/RaceModule.cs) inside examples for a clean template.

---

## Workflow

1. Determine the outcomes subsequent graph nodes need to read from this module.
2. Create a class inheriting from `ModuleOutput`. Add `[Serializable]` and `[Preserve]`.
3. Add fields and decorate them with `[PortId]`, `[PortDisplayName]`, and `[PortDescription]`.
4. If reference types are present, override `Clone()` and provide a deep copy.
5. Link on the module class using `[ModuleIO]`.
6. Inside your module, write computed outcomes to `RuntimeContext.ModuleOutput` before finishing execution.

## Output Format

* Show the completed `ModuleOutput` class.
* Show the module writing values to the output just before completing.

## Test Prompts

1. "Create a custom ModuleOutput named QuizOutput that outputs the number of correct answers and a list of missed question IDs."
2. "Show a module that counts elapsed seconds and writes it to a DurationOutput before OnComplete is called."
