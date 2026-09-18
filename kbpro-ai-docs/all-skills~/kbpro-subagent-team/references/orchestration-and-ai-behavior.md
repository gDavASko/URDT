# Orchestration and AI Behavior Rules

## Scope

Apply an orchestration pattern and assign agents only to solve a concrete user
task. Do not activate team work, load every pattern, or read every agent context
because the skill is available.

## Main Agent Responsibilities

- Define the outcome, constraints, authority boundary, and evidence required for completion.
- Choose the smallest applicable pattern and the minimum necessary registered agents.
- Split only independent work; retain dependency ordering and enough context to prevent rework.
- Give each agent a bounded task, relevant local artifacts, and a required output format.
- Resolve conflicts, collect evidence, arrange independent review where needed, and report the actual outcome.

## Agent Behavior Rules

- Follow the assigned pattern, local agent context, repository rules, and user authorization.
- Do not invent findings, agent outputs, permissions, source contents, or validation results.
- Report uncertainty, blockers, and missing authority immediately.
- Keep context and output proportional to the assigned subtask; do not bulk-load unrelated documents.
- Treat external writes, credentials, publication, deletion, and irreversible changes as requiring explicit authority.

## Selection Rules

- Prefer one pattern. Combine patterns only for distinct phases with clear handoff criteria.
- Prefer the lowest-cost model tier that satisfies the registered agent role.
- Use a worker for bounded mechanics, a researcher for unresolved questions, and a judge for independent assessment.
- Do not use an agent when the work is trivial, indivisible, or would cost more coordination than direct execution.
