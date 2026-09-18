---
name: KBPro-Bitrix-GantEditor
description: "Edit an existing Bitrix24 Gantt plan — insert a task (found by name/content or newly created) into a person's chain, move a task to another slot or another employee, then reflow all downstream dates and arrows. Triggers: перенеси задачу в ганте, передай задачу другому сотруднику, вставь задачу в план, встрой задачу в гант, подвинь задачу, move/reschedule task in gantt."
status: candidate
owner: KBPro
source:
  - Companion of KBPro-Bitrix-GantCreator; shares its API contract and scheduling rules.
license: project-internal
allowed_tools:
  - filesystem-read
  - powershell
  - bash
  - http-request
required_reading:
  - ../KBPro-Bitrix-GantCreator/references/bitrix-gantt-api.md
  - references/operations.md
known_risks:
  - Same API traps as GantCreator (phantom links via task.dependence.add, batch bug) — read the shared reference first.
  - Applying a reflow without showing the user the shift preview (finish-date deltas per person) — always preview and confirm before writing.
  - A task listed twice or dropped while hand-editing plan-new.json — reflow.cjs validates duplicates, but count tasks before/after.
  - Moving a task into a slot that violates the pattern (e.g. звуки before логика of the same module) — warn the user, proceed only on their confirmation.
  - Tasks already in progress/done keep their reality regardless of plan; confirm before re-dating them.
---

# KBPro Bitrix Gantt Editor

## Purpose

Surgical changes to an ALREADY BUILT Gantt (made by KBPro-Bitrix-GantCreator or by hand):
insert one or more tasks into a person's chain, move a task within a chain, hand a task to
another employee, or pull a task out — and then repair the whole remaining plan from the
mutation point on: downstream dates shift, the source chain closes its gap, finish-start
arrows are re-wired (including clearing the predecessor of a new chain head).

## Inputs — ask the user

1. **Project** (GROUP_ID).
2. **Which task(s)**: existing — the user gives a name or describes the content, and YOU
   find it in the fetched plan (title/tag match; show the match for confirmation). Or new —
   create it first via the `bitrix-task-json-builder` + `bitrix-task-importer` skills, then
   inject the new IDs (they appear in plan.json's `unplanned` bucket after re-read).
3. **Destination**: which person's chain and where — after task X / before task Y /
   start or end of a module block / "к такой-то дате". Resolve to an exact position.
4. **Source gap** (for moves): close the gap by shifting the source chain left (default) or
   keep dates as-is for everything before the removed slot (always true — prefixes never move).

## Workflow

1. Read `../KBPro-Bitrix-GantCreator/references/bitrix-gantt-api.md` (shared API contract)
   and `references/operations.md` (how to express each operation as a plan.json edit).
2. **Read current plan**: `scripts/read-plan.cjs --group <ID>` → `plan.json`
   (per-person chains ordered by plan dates + `unplanned` bucket).
3. **Locate the task(s)** in plan.json by title/content; quote the exact match(es) and the
   destination position to the user before editing.
4. **Edit**: copy `plan.json` → `plan-new.json`, apply the operation as an array edit
   (move/insert the task object; new tasks need `{id, title, cx}`; assignee change = moving
   the object to another person's `tasks` array — reflow sets RESPONSIBLE_ID from the chain).
5. **Reflow**: `scripts/reflow.cjs --plan plan-new.json --orig plan.json` →
   `updates.json` + minimal `changes-dates.json` / `changes-links.json`.
   Unchanged prefixes reproduce identical dates; everything from the mutation point shifts.
   Reflow schedules by integer person-hours in the 10:00-18:00 MSK window, max 8 hours/day.
6. **Preview to the user**: per-person new finish dates vs old, number of shifted tasks.
   Wait for confirmation.
7. **Apply dates**: `node ../KBPro-Bitrix-GantCreator/scripts/apply-dates.cjs --updates changes-dates.json`.
8. **Apply links**: `scripts/apply-links.cjs --links changes-links.json` (handles cleared
   predecessors, unlike Creator's set-links). Run in background if large.
9. **Verify**: `node ../KBPro-Bitrix-GantCreator/scripts/verify.cjs --updates updates.json`
   (sample includes chain heads and the mutation neighborhood).
10. Tell the user to hard-refresh the Gantt (Ctrl+F5).

## Scheduling invariants (must hold after every edit)

- Same as GantCreator: workdays only, `cx` integer person-hours per task, max 8 hours per day,
  10:00–18:00 MSK working window, markers (`cx=0`) as 1-hour slots, one linear chain per person, every non-head task has
  exactly one predecessor.
- Dates/responsible/`UF_TASKS_TASK_1783529349965`/`DURATION_PLAN` are applied first; `DEPENDS_ON`
  arrows are applied second through legacy `task.item.update`, never through `task.dependence.add`.
- The pattern's ordering conventions (see `../KBPro-Bitrix-GantCreator/patterns/dev.md`)
  are ADVISORY here: the user's explicit placement wins, but warn on violations
  (e.g. звуки inserted before the module's логика).
- Deadlines are never touched unless asked.

## Scripts

- `scripts/read-plan.cjs` — live plan → plan.json (chains + unplanned).
- `scripts/reflow.cjs` — recompute dates/links from edited chains; emits full updates and
  minimal change sets (validates duplicate task entries).
- `scripts/apply-links.cjs` — targeted DEPENDS_ON updates incl. clearing (pred=null → []).
- Reused from Creator: `apply-dates.cjs`, `verify.cjs`.

## Examples

- `examples/move-task-example.md` — a worked move: task to another employee mid-chain.
