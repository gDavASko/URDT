# Model & Reasoning-Effort Selection (skill copy)

Self-contained copy of the canonical rule for choosing **which model** an agent
uses and **how hard it reasons**. This copy lives inside the skill so the skill is
self-contained; do not link to an external path. Master copy for reference only:
`llm-wiki/raw/model-and-reasoning-effort-selection.md`.

> Derived from: Sebastian Raschka, "Controlling Reasoning Effort in LLMs".

---

## Two orthogonal axes — set both, explicitly

| Axis | Question | Values |
|:-----|:---------|:-------|
| **Capability tier** | *Which* model runs the task? | `cheap → medium → high → max → top` |
| **Reasoning effort** | *How hard* does it think first? | `off → low → medium → high` |

The dials are decoupled: a top model can run at low effort, a mid model at high
effort. Pick the cheapest `(tier × effort)` that clears the task's cost of error.

## Core principles

1. Effort is a separate control from capability — decide both on purpose.
2. Diminishing returns: do not default to maximum effort.
3. A smaller model at higher effort can match a larger model at lower effort;
   prefer the cheaper combination when it clears the bar.
4. No reliable auto-selection: assign effort explicitly per role.
5. Mechanical / trivial work runs with reasoning **off**.
6. A hard token budget is the fail-safe; the task must tolerate a truncated trace.

## Role → tier × effort

| Role | Capability tier | Reasoning effort |
|:-----|:----------------|:-----------------|
| **worker** (mechanical execution, normalization) | cheap | **off / low** |
| **judge** (standard verification) | medium | **medium** |
| **evaluator** (important verification) | high | **high** |
| **reasoner** (architecture, algorithm design) | max | **high** |
| **researcher** (investigation before implementation) | high–max | **high** |
| **manager / orchestrator** (planning, control) | top | **medium** (→ high when decomposing a hard problem) |

Two hard rules:
1. NEVER assign mechanical work at high effort to a top-tier model.
2. NEVER validate a critical result with a weak model at low effort.

## How to set the effort

1. Native effort dial where the runtime exposes it (IDE subagents: `deep-reasoner`
   = high, `reasoning-architect` = medium, `fast-worker` = low).
2. Otherwise system-prompt conditioning: prepend `Reasoning effort: low | medium |
   high` (or "answer directly, no reasoning" for `off`).
3. Hard token/thinking budget cap on top, as the inference-time fail-safe.

> Concrete model ids stay machine-owned in project/runtime config. This rule
> governs the decision procedure (tier × effort), not the id mapping.
