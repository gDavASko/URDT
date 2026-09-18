---
name: unity-boilerplate-generator
description: "KBPro patterns: State Machines, SO events, Object Pools. Triggers: generate state machine, create object pool, design SO config, boilerplate, pattern."
status: candidate
owner: KBPro
source:
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/Skills/UnityBoilerplateGenerator.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
license: project-internal
validated_against:
  - AGENTS.md
  - kbpro-ai-docs/unity-wiki/wiki/concepts/unity-ai-skill-validation.md
allowed_tools:
  - filesystem-read
  - filesystem-write
  - rg
forbidden_actions:
  - Do not generate code with static Singletons — use LazySrv<T>.
  - Do not generate async void — use async UniTask.
  - Do not skip Initialize and Dispose methods in generated classes.
  - Do not use magic strings — use [ConstSelector] or generated constants.
  - Do not generate new service classes if existing KBPro services cover the need.
required_reading:
  - AGENTS.md
  - kbpro-ai-docs/kbpro-wiki/raw/code_style.md
  - kbpro-ai-docs/kbpro-wiki/raw/principals.md
  - kbpro-ai-docs/kbpro-wiki/raw/Architecture/CoreFramework/Guides/HowToCreateModule.md
known_risks:
  - Generating generic patterns that conflict with existing KBPro module/system conventions.
  - Skipping namespace or member-order rules.
  - Generating State Machine that bypasses LogicSystem lifecycle.
---

# Unity Boilerplate Generator

## Purpose

Rapidly generate clean, KBPro-compliant boilerplate for State Machines, ScriptableObject data systems, and Object Pools. All generated code must compile in the project and respect KBPro lifecycle.

## When To Use

- User asks for a State Machine for a specific actor or system.
- User asks for an Object Pool for a prefab.
- User asks for a ScriptableObject config, event channel, or shared variable.
- User asks for "boilerplate" or "pattern" without specifying a concrete module.

## Generation Rules (All Patterns)

- Namespace: `KBP.{CATEGORY}` matching surrounding files.
- One class per file.
- Member order: constants → static → serialized/public fields → private fields → properties → constructors/init → lifecycle → public → protected → private → cleanup.
- `Initialize()` and `Dispose()` present in all stateful classes.
- `LazySrv<T>` for service access (never Singleton).
- `UniTask` for async (never standard Task or async void).
- `[SerializeField] private` for Unity references.

## Pattern: State Machine

```
BaseState (abstract): Enter(), Exit(), Tick(float dt)
StateMachine: current state, ChangeState<T>(), Tick() → delegates to current state
ConcreteStates: IdleState, RunState, etc.
```
- States do not know about each other — only the StateMachine manages transitions.
- Integrate with KBPro LogicSystem lifecycle if inside a module.

## Pattern: ScriptableObject Systems

- Event Channel: `[CreateAssetMenu] ScriptableObject` with `Raise()` and `OnEventRaised` UnityEvent.
- SharedVariable: `[CreateAssetMenu] ScriptableObject<T>` with `Value` property and optional change event.
- Config: `[CreateAssetMenu] ScriptableObject` with serialized data fields.

## Pattern: Object Pool

```csharp
public class PrefabPool
{
    private readonly Queue<GameObject> _pool = new Queue<GameObject>();
    private readonly GameObject _prefab;
    private readonly Transform _parent;

    public GameObject Get() { ... }
    public void Return(GameObject go) { ... }
}
```
- Pool owns lifecycle: disable on return, enable on get.
- Thread-safety only if required by the task.

## Output Format

- Show all files in a directory tree.
- Show how to register/wire the generated code into existing KBPro module/flow.
- List any ScriptableObject assets that must be created in Unity Editor.

## Test Prompts

1. "Generate a State Machine for the Train actor: Idle, Moving, Stopping states."
2. "Create an Object Pool for the passenger prefab."
3. "Design a ScriptableObject config container for the building module level data."
4. Negative: "Generate a Singleton manager for this." Skill should produce a LazySrv-based service instead.
