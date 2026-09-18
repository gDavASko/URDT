---
name: kbpro-subagent-team
description: Orchestrate a KBPro task through a role-based subagent team. Use only when the user explicitly asks to work as a team, with subagents, or by the team principle.
---

# KBPro Subagent Team

Use team mode only when the user explicitly requests it. The main agent sets
goals, assigns roles, transfers only necessary context, coordinates dependencies,
arranges independent review, and remains accountable for task completion.

## Team Workflow

1. Define the done criteria, constraints, and independent work items.
2. Read the [agent registry](agents/registry.yaml), choose a registered agent, and load its local context.
3. Give the agent only its goal, required paths or artifacts, constraints, and output format.
4. Select **two independent dials** for the agent — capability tier *and* reasoning
   effort — per [Model & Reasoning-Effort Selection](references/model-and-reasoning-effort-selection.md).
5. Parallelize only independent work. Do not assign mechanical work to an expensive model.
6. Arrange independent review and resolve every confirmed issue before completion.

## Model & Reasoning-Effort Selection

Choosing an agent is two orthogonal decisions, not one: **capability tier** (*which*
model, `cheap → top`) and **reasoning effort** (*how hard* it thinks, `off → low →
medium → high`). Set both explicitly — automatic effort selection is unreliable —
and pick the cheapest `(tier × effort)` that clears the task's cost of error.

- **worker** → cheap tier, effort **off/low**; **judge** → medium tier, effort **medium**;
  **researcher** → high–max tier, effort **high**.
- Do not default to maximum effort (diminishing returns); a smaller model at higher
  effort often matches a larger model at lower effort — prefer it when cheaper.
- Mechanical work runs with reasoning off. Never validate a critical result with a
  weak model at low effort.

Full rule (self-contained local copy): [references/model-and-reasoning-effort-selection.md](references/model-and-reasoning-effort-selection.md).

## Pattern Selection

Before assigning an agent, read the [orchestration pattern registry](patterns/registry.yaml).
Match the task to a registry `triggers` entry, then load only the registered
`context` file. Choose one pattern. Load a second pattern only when the task has
a separate, necessary phase that the selected pattern does not cover.

Use `default` only when no registered trigger matches the task.

Read [Orchestration and AI Behavior Rules](references/orchestration-and-ai-behavior.md)
before applying a pattern. Patterns and agents are task-scoped: do not load or
use all of them by default.

## Command: Add an Agent

To add an agent, read and follow [Agent Creation and Registration Rules](references/agent-creation-rules.md).
Do not create an agent context or registry entry outside that process.

The registry is the only place that lists and briefly describes available agents.

## Non-Negotiable Rules

- Do not simulate agent responses or expand user authorization.
- Preserve repository rules and human gates for external or high-risk actions.
- If no suitable model or tool is available, report the limitation or reassign the work.
