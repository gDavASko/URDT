---
name: kbpro-scenario-graph-builder
description: "ScenarioGraph asset creation/initialization. Triggers: create scenario graph, new ScenarioGraph, initialize graph asset, build scenario."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
known_risks:
  - Creating a graph without an Entry node.
  - Adding loose transitions with nonexistent FromNodeId or ToNodeId values.
  - Mutating ScenarioGraph script assets at runtime (which corrupts the persistent template).
---

# KBPro Scenario Graph Builder

## Purpose

Apply this skill to create, structure, configure, and validate `ScenarioGraph` assets — either programmatically via factory/test tools or visually within ScriptableObject boundaries.

## When To Use

* Building test scenarios or mock graphs for unit tests.
* Creating generator utilities that export external flowsheets into `ScenarioGraph` assets.
* Wiring up complex `ScenarioGraph` parameters, script settings, or final transition IDs.

## Do Not Use

* For editing visual nodes — use `kbpro-scenario-node-builder`.
* For implementing transition logic inside conditions — use `kbpro-scenario-link-builder`.

## KBPro Scenario Graph Rules

### 1. In-Memory Graph Creation for Testing
To create a fully valid graph in memory (e.g., in a test method), use `ScenarioGraph` constructor and `ConfigureRuntime`.
Refer to [ScenarioTestHarness.cs](file://examples/ScenarioTestHarness.cs) for a complete template:
* Programmatically instantiates `ScenarioGraph`, `EntryScenarioNode`, `ModuleScenarioNode`, and `ExitScenarioNode`.
* Builds outgoing transitions with appropriate `SourceNodeAction` rules.
* Invokes `ConfigureRuntime` to establish the final cached representation.

### 2. Required Structuring Checklist
When creating or generating any ScenarioGraph asset:
1. **Entry Node:** Ensure there is exactly one `EntryScenarioNode`.
2. **Exit Node:** Ensure there is at least one `ExitScenarioNode`.
3. **Data Links:** Ensure all `ScenarioDataLink` entries connect to active ports that match in type.
4. **Final Transition:** Set the `_finalTransitionId` to match the Exit Node's ID.

---

## Workflow

1. Create a `ScenarioGraph` ScriptableObject instance.
2. Initialize and configure its internal nodes arrays, transitions, and data links.
3. Validate using `ScenarioGraphValidator` if running inside Editor/tests.
4. Mark asset dirty using `EditorUtility.SetDirty(graph)` if saving to disk in Editor.

## Output Format

* Show C# snippet constructing and wiring nodes, transitions, and links.
* Enforce using namespaces: `KBP.CORE` and `KBP.CORE.MODULES`.

## Test Prompts

1. "Write a helper method that instantiates and wires a ScenarioGraph with three modules and an entry/exit pair."
2. "Create a C# unit test setup that launches a mock ModuleStateMachine using a programmatically constructed graph."
