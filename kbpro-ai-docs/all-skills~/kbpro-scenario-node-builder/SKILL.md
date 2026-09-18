---
name: kbpro-scenario-node-builder
description: "ScenarioGraphNode, custom actions, routing. Triggers: create scenario node, custom graph node, inherit ScenarioGraphNode, add node type."
status: candidate
owner: KBPro
license: project-internal
allowed_tools:
- filesystem-read
- filesystem-write
- rg
known_risks:
- Assigning execution transitions directly to IDataOnlyNode types.
- Forgetting [System.Serializable] on node classes.
- Adding gameplay logic to nodes instead of delegating to modules.

---

# KBPro Scenario Node Builder

## Purpose

Apply this skill to create or extend `ScenarioGraphNode` classes (e.g., custom actions, routing selectors) that are placed and configured within the ScenarioGraph Node Editor.

## When To Use

- Writing a custom node type that executes a specific non-module editor command.
- Extending routing nodes (like random selectors, conditions).
- Creating data-only nodes implementing `IDataOnlyNode`.

## Do Not Use

- For creating a visual component layout — that is covered by `kbpro-module-components-builder`.
- For implementing actual gameplay logic — use `kbpro-node-module-builder` instead. Nodes should be visual wrappers, leaving logic to decoupled modules.

## KBPro Scenario Node Rules & Patterns

### 1. Basic Scenario Node Class
All custom nodes must inherit from `ScenarioGraphNode`, be serializable, and reside in the correct category namespace. 
Refer to [LogScenarioNode.cs](file://examples/LogScenarioNode.cs) for a complete template:
* Class must have `[Serializable]` and `[Preserve]` decoration.
* Fields should be serialized private fields with PascalCase public getters.

### 2. Data-Only Nodes (`IDataOnlyNode`, `IDataSourceNode`)
If a node exists only to feed values via data-links and has no exec control-flow connections, it must implement `IDataOnlyNode` and `IDataSourceNode`.
Refer to [CustomDataNode.cs](file://examples/CustomDataNode.cs) for a complete template.
* Implement `ResolvePort` to fetch parameters safely and validate expected types.
* Visual linter will block exec transitions to and from this node.

## Workflow

1. Determine if the node is an **Exec Node** (runs in a sequence) or a **Data-Only Node** (feeds data).
2. Inherit from `ScenarioGraphNode`. If data-only, implement `IDataOnlyNode` and `IDataSourceNode`.
3. Add serialized private fields with proper PascalCase properties for configuration.
4. Set the correct `ScenarioNodeType` override.
5. Create a corresponding Editor script under `NodeEditors` folder inheriting from `ScenarioNodeEditorBase` if custom drawing is required.

## Output Format

- Show the node runtime class with namespaces and XML comments.
- Include serialization decoration `[Serializable]`.
- If data-only, show `ResolvePort` implementation.

## Test Prompts

1. "Create a custom LogScenarioNode that accepts a string message and a log level enum."
2. "Build a custom data-only node named TimeOfDayNode that reads system time and outputs a float hour."
3. Negative: "Create a node inheriting ScenarioGraphNode but don't add [Serializable] or NodeType." Skill should refuse and explain why.
