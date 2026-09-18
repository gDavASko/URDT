---
name: KBPro-Bitrix-GantCreator
description: "Build a Gantt chart from existing Bitrix24 project tasks — schedule by complexity in person-hours, assign people with individual start dates/times, write plan dates and finish-start arrows. Triggers: построй гант, распиши задачи по ганту, составь гант график, gantt from Bitrix tasks, спланируй задачи проекта в битриксе."
status: candidate
owner: KBPro
source:
  - Validated live repair on project GROUP_ID=100 (Nu Pogodi Robots), 2026-07-21: 142 Dev tasks, 1624 person-hours, 137 links, zero errors.
  - Validated live run on project GROUP_ID=94 (DavASko АИ-Песочница), 2026-07-15: 142 tasks, 137 links, zero errors.
license: project-internal
allowed_tools:
  - filesystem-read
  - powershell
  - bash
  - http-request
required_reading:
  - references/bitrix-gantt-api.md
  - patterns/dev.md
known_risks:
  - "CRITICAL: `task.dependence.add` creates links the Gantt UI does NOT draw (phantom links). Arrows are rendered ONLY from the legacy DEPENDS_ON field written via `task.item.update`. See references/bitrix-gantt-api.md."
  - "CRITICAL: `task.dependence.add` inside `batch` silently half-executes (cycle-checker sees the link, UI and DEPENDS_ON do not). Never batch dependence methods."
  - Hardcoding the webhook URL — always use the KBPRO_BITRIX24_WEBHOOK_BASE env var.
  - Overwriting plan dates of tasks that are already in progress — filter by status or confirm with the user.
  - Setting DEPENDS_ON on a task whose plan dates violate finish-start may auto-shift dates; schedule dates FIRST, links SECOND, and give zero-complexity tasks a 1-hour slot inside the 10:00-18:00 MSK work window.
  - Weekend/holiday leakage — the scheduler must place starts/ends on workdays only (see config.holidays).
---

# KBPro Bitrix Gantt Creator

## Purpose

Turn an existing flat backlog of tasks in a Bitrix24 project (workgroup) into a full Gantt plan:
per-person sequential chains, plan dates computed from the task complexity field (integer person-hours),
responsible assignees, and finish-start dependency arrows visible in the «Проекты ПРО» Gantt view.

This skill PLANS and WRITES. It does not create tasks (use `bitrix-task-importer` for that).

## Inputs — ask the user during planning (all five, before touching the portal)

1. **Project**: which Bitrix24 project (GROUP_ID) to build the Gantt in.
2. **Start date**: the date the Gantt starts from.
3. **Task selection principle**: how to pick tasks inside the project — by tag, by title
   prefix (e.g. `[Dev]`), by status, or another filter. Default: all open tasks of the group.
4. **People**: which employee(s) to assign, each with a work interval — "from DATE"
   (and optionally "until DATE"). Resolve names to Bitrix user IDs via `user.get`.
5. **Sequencing pattern**: which pattern from `patterns/` to apply (dev, artists,
   animators, gamedesign). If the tasks look like `[Dev][type] Module - Submodule`,
   recommend `patterns/dev.md`.

Also ask (or decide and state): fixed module→person assignments and module priority order,
if the user has preferences (e.g. "programming system first, humanoid → Andrey").

## Workflow

1. **Read `references/bitrix-gantt-api.md` first** — it contains the non-obvious API contract
   (two link storages, batch bugs, field names). Skipping it produces invisible arrows.
2. **Discover**: `scripts/fetch-tasks.cjs --group <ID>` → `tasks.json`.
   Resolve the complexity field via `task.item.userfield.getlist` (on kbpro.bitrix24.ru it is
   `UF_TASKS_TASK_1783529349965`, type double, used as integer person-hours). Resolve user IDs via `user.get`.
3. **Plan**: pick the pattern, build `config.json` (people, start dates, block assignments,
   type priority, tail types, holidays). For non-trivial distribution use a reasoning subagent
   to balance load (minimize makespan, align finish dates), then freeze the plan into config.
   Show the block-level plan to the user before writing anything.
4. **Schedule**: `scripts/schedule.cjs` → `updates.json` + human-readable summary.
   Verify: 0 errors, all tasks covered, per-block person-hour sums match.
5. **Apply dates**: `scripts/apply-dates.cjs` (batch, 50 per call — batching plain
   `tasks.task.update` is safe). This writes plan dates, responsible, person-hour estimate,
   `DURATION_TYPE="hours"`, and `DURATION_PLAN`.
6. **Apply links**: `scripts/set-links.cjs` (sequential legacy `task.item.update` with
   `DEPENDS_ON`; ~1–4 s per call, run in background). NEVER use `task.dependence.add`.
7. **Verify**: `scripts/verify.cjs` — sample tasks across persons: dates intact,
   `DEPENDS_ON` = immediate predecessor, cross-module joints correct.
8. Tell the user to hard-refresh the Gantt page (Ctrl+F5) — the UI does not live-update
   from REST changes.

## Scheduling rules common to all patterns

- Workdays only (Mon–Fri minus `config.holidays`); one workday is max `8` person-hours.
  Work slot = 10:00–18:00 MSK/portal time; tasks are scheduled by exact start/end time inside the slot.
- A person is one chain: tasks strictly sequential, linked finish-start, including
  transitions between modules.
- If a 4-hour task starts at 10:00, it ends at 14:00 and the next task starts at 14:00.
  If a task crosses 18:00, the remainder continues from 10:00 on the next workday.
- Zero-complexity tasks ("markers") stay in the chain: they get a 1-hour slot inside the
  10:00–18:00 work window, so same-day tasks still
  go strictly one after another and arrows render without date shifts.
- Every task except each person's first has exactly one predecessor. No hanging tasks.
- To draw arrows in the Bitrix Gantt UI, write dependencies only through legacy
  `task.item.update` / `DEPENDS_ON=[previousTaskId]`; `task.dependence.add` is not enough.
- Do not touch deadlines unless asked (pre-existing deadlines may conflict; offer to align them).

## Pattern library

- `patterns/dev.md` — developers (Dev): module-sequential with sound/voiceover tail. The
  reference pattern, validated live.
- `patterns/other-roles.md` — draft sequencing principles for artists, animators,
  game designers. Refine on first real use.

## Examples

- `examples/project94-config.json` — the real config of the validated run.
- `examples/project94-run.md` — what happened, numbers, and the resulting plan.
