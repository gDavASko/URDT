# Operations cookbook: expressing Gantt edits as plan.json changes

Every operation is an edit of `plan-new.json` (a copy of `plan.json` from read-plan.cjs),
followed by `reflow.cjs --plan plan-new.json --orig plan.json`. Reflow re-lays out each
chain from its `anchor`; only tasks after the mutation point actually change.

plan.json shape:

```json
{
  "group": 100, "timezone": "+03:00", "workStart": "10:00", "workEnd": "18:00", "workHours": 8, "holidays": [],
  "people": [
    { "resp": 12, "anchor": "2026-07-22",
      "tasks": [ { "id": "30518", "title": "...", "cx": 4, "start": "...", "end": "..." } ] }
  ],
  "unplanned": [ { "id": "30900", "title": "...", "cx": 8, "resp": 66 } ]
}
```

`start`/`end` inside tasks are the LIVE values (used only for diffing via --orig);
reflow ignores them and recomputes from `anchor` + `cx` order. `cx` is integer person-hours.
The planning window is 10:00-18:00 MSK, max 8 hours per workday; a 4-hour task starting at
10:00 ends at 14:00 and the next task starts at 14:00.

## Scope rule (IMPORTANT)

In `plan-new.json` keep ONLY the chains you are editing (source and target person);
delete the other `people` entries. Reflow emits updates only for present chains, so
untouched people can never be re-dated by accident. This also protects foreign tasks
that have plan dates but no complexity value (cx=0 with a multi-day bar): reflow would
misinterpret them as 1-hour markers if their chain were included.

## Move a task within one person's chain

Cut the task object from its index, insert at the target index. Downstream of the
EARLIER of the two positions shifts.

## Hand a task to another employee

Cut the object from person A's `tasks`, insert into person B's `tasks` at the target
position. Reflow assigns RESPONSIBLE_ID = B's `resp` automatically. A's chain closes the
gap (everything after the removed slot shifts left), B's chain shifts right.

## Insert a newly created task

1. Create it via `bitrix-task-json-builder` → `bitrix-task-importer` (same group).
2. Re-run `read-plan.cjs` — the task appears in `unplanned` (no plan dates yet).
3. Move its object `{id, title, cx}` from `unplanned` into the target chain position.
   If complexity wasn't set at creation, ask the user for integer person-hours and set `cx`.

## Remove a task from the plan

Delete the object from the chain (chain closes the gap). The task keeps its last plan
dates in Bitrix unless you also clear them; its old successor gets a new predecessor via
changes-links. If the user wants it fully unplanned, clear START_DATE_PLAN/END_DATE_PLAN
and DEPENDS_ON with a manual `tasks.task.update` / `task.item.update` call.

## Change a task's duration (complexity)

Edit `cx` in place (optionally also update the portal field via
`tasks.task.update fields[UF_TASKS_TASK_1783529349965]`). Downstream shifts. Keep
`DURATION_TYPE="hours"` and `DURATION_PLAN` equal to `cx`.

## Move a whole block (module)

Cut a contiguous slice of task objects and insert it elsewhere (same or another person).
Keep the slice order intact.

## Shift a person's whole chain start

Change the person's `anchor` date. Their entire chain moves; other chains are untouched.

## Preview rules (mandatory before applying)

Report per affected person: old finish → new finish, and how many tasks changed dates.
Data comes from reflow's console output plus `changes-dates.json` length. Apply only
after the user confirms.

## Order of application

1. `apply-dates.cjs --updates changes-dates.json` (batch-safe) — dates/responsible/complexity/duration FIRST,
2. `apply-links.cjs --links changes-links.json` — legacy `DEPENDS_ON` links SECOND (finish-start must already
   hold, otherwise Bitrix may auto-shift dependents),
3. `verify.cjs --updates updates.json`.
