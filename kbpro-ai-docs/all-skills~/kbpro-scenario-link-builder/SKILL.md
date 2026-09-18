---
name: kbpro-scenario-link-builder
description: "Transitions, data links, ComparisonCondition. Triggers: create transition, add condition to node, ScenarioTransitionData, connect nodes."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
known_risks:
  - Accessing the output parameter of the leaving node inside its own transition condition (due to snapshot timing, it will not be flushed yet).
  - Leaving unconnected dangling connections on nodes.
  - Type mismatches between output source port and input target port.
---

# KBPro Scenario Link Builder

## Purpose

Apply this skill to construct, validate, or script connections between ScenarioGraph nodes — including execution transitions (`ScenarioTransitionData`), transition conditions (`IModuleTransitionCondition`), and data links (`ScenarioDataLink`).

## When To Use

* Writing a custom transition condition (e.g., checking Player level or specific system flag).
* Scripting graph assembly code that wires up nodes programmatically (like test harnesses).
* Setting up `ScenarioDataLink` structures in editor utilities.

## Do Not Use

* For creating visual nodes — use `kbpro-scenario-node-builder` instead.
* For caching module input values — use `kbpro-module-input-builder`.

## KBPro Transition and Link Rules

### 1. Custom Transition Condition
Transitions are chosen based on conditions. To write a custom condition, implement `IModuleTransitionCondition`.
Refer to [HasItemTransitionCondition.cs](file://examples/HasItemTransitionCondition.cs) for a complete template:
* Class must have `[Serializable]` and `[Preserve]` decoration.
* Implement `CanPass(Parameters context)` to safely evaluate transition logic against blackboard/global stats.

### 2. Output Snapshot Warning
> [!WARNING]
> A node's output snapshot is flushed **after** transition evaluation. Do not read the current node's `ModuleOutput` inside its outgoing transition condition — it will refer to the previous node's snapshot or will be empty. Instead, read from `context` (ScriptParameters) or global statistics.

### 3. Data Link Connections (`ScenarioDataLink`)
When defining a data link or exec transition between nodes programmatically (e.g., in factories or tests), refer to [LinkWiringExample.cs](file://examples/LinkWiringExample.cs) for a clean example of setup and instantiation:
* Specify correct `FromNodeId` and `ToNodeId`.
* Configure proper `LinkRefreshMode` to govern update frequencies.

## Workflow

1. Determine if you are creating an **Exec Transition** or a **Data Link**.
2. If Exec Transition with a custom rule, implement `IModuleTransitionCondition`.
3. Decorate your condition class as `[Serializable]` so it can be added to the Graph asset.
4. Set up visual drawers in the Editor using subclasses.

## Output Format

* Show the serialized custom condition class with comments.
* Clearly show programmatic wiring examples when working with tests or builders.

## Test Prompts

1. "Create a custom condition named ParameterGreaterCondition that compares a float parameter in context against a constant threshold."
2. "Show how to construct a ScenarioTransitionData programmatically between two nodes with a condition."
