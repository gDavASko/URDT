# Bitrix24 API contract for Gantt building (verified live 2026-07-15, kbpro.bitrix24.ru)

## Access

- Incoming webhook base URL in env var `KBPRO_BITRIX24_WEBHOOK_BASE` (user-level; scopes:
  tasks, user, batch). Never hardcode or commit it.
- Call: `POST {base}/{method}.json` with `Content-Type: application/json`.
- The MCP server `b24-dev-mcp` is DOCUMENTATION ONLY — it is not connected to the portal.

## Reading tasks

```
POST tasks.task.list
{"filter":{"GROUP_ID":94},
 "select":["ID","TITLE","STATUS","RESPONSIBLE_ID","DEADLINE","START_DATE_PLAN",
           "END_DATE_PLAN","PARENT_ID","TAGS","UF_TASKS_TASK_1783529349965"],
 "start":0}
```
- Paginate with `start = response.next` until `next` is absent. `total` gives the count.
- Complexity (integer person-hours) is a portal-specific userfield. Resolve via
  `task.item.userfield.getlist` (look for type `double`). On kbpro it is
  `UF_TASKS_TASK_1783529349965`; in responses it comes back camel-cased
  (`ufTasksTask1783529349965`).
- Task type (логика/графика/…) lives both in TAGS and in the title prefix
  `[Dev][<type>] Module - Submodule`.

## Resolving people

`POST user.get {"FILTER":{"ACTIVE":"True"}}` (paginate). Match LAST_NAME + NAME.
Known IDs: Вереин Андрей=20, Клюенков Владислав=12, Вакулин Иван=22, Ливерко Алексей=58,
Марченко Семён=128, Давлетбаев Александр=66.

## Writing plan dates + assignee — batch IS safe here

```
POST batch
{"halt":0,"cmd":{
  "u30362":"tasks.task.update?taskId=30362&fields[START_DATE_PLAN]=2026-07-15T10%3A00%3A00%2B05%3A00&fields[END_DATE_PLAN]=2026-07-15T14%3A00%3A00%2B05%3A00&fields[RESPONSIBLE_ID]=20&fields[DURATION_TYPE]=hours&fields[DURATION_PLAN]=4"
}}
```
- Max 50 cmds per batch. URL-encode date values (`+` → `%2B`).
- The same update must write `UF_TASKS_TASK_1783529349965`, `DURATION_TYPE=hours`, and
  `DURATION_PLAN=<integer person-hours>` so task estimate and Bitrix duration stay in sync.
- Dates ISO-8601 with portal offset (+05:00 for kbpro). `task.item.getdata` displays them
  converted to +03:00 — that is the same instant, not an error.

## Dependency arrows — THE core gotcha

Bitrix has TWO link storages:

| Storage | Written by | Read by Gantt UI («Проекты ПРО») |
|---|---|---|
| New dependence table (linkType 0..3) | REST `task.dependence.add` / UI drag (UI writes both) | **NO** — arrows not drawn |
| Legacy DEPENDS_ON («предыдущие задачи», finish-start only) | `task.item.update` FIELDS.DEPENDS_ON / UI drag | **YES** — arrows drawn |

Consequences (all verified experimentally):

1. **Create arrows ONLY via** `POST task.item.update {"TASKID":<dep>,"FIELDS":{"DEPENDS_ON":[<pred>]}}`.
   DEPENDS_ON REPLACES the whole predecessor list. Semantics: finish(pred) → start(dep).
2. `task.dependence.add` produces "phantom" links: the cycle-checker sees them
   (re-adding fails with «Нельзя создавать циклическую зависимость»), `DEPENDS_ON` stays
   empty, UI draws nothing. Inside `batch` it is even worse — reports success, half-writes.
   Clean up phantoms with `task.dependence.delete` (same from/to as created).
3. Error messages print node pairs in (dependent, predecessor) order — the REVERSE of the
   request's (taskIdFrom, taskIdTo). Don't conclude the direction is swapped; it isn't:
   taskIdFrom = predecessor, taskIdTo = dependent, linkType 2 = finish-start.
4. `task.dependence.delete` does NOT remove a legacy DEPENDS_ON row, and
   `task.item.update DEPENDS_ON` does not create a new-table row.
5. `task.item.update` is slow (1–4 s). Run link-setting sequentially with a ~250 ms pause,
   in the background. Do not batch it (unverified) — sequential is proven.
6. If the finish-start constraint is violated by current plan dates, Bitrix may auto-shift
   the dependent task. Always write dates first, links second, and keep constraints satisfied
   (zero-complexity tasks get a 1-hour slot inside the 10:00-18:00 working window).

## Planning window and capacity

- Capacity unit is integer person-hours in `UF_TASKS_TASK_1783529349965`.
- Working window is 10:00-18:00 MSK/portal time, exactly 8 person-hours per workday.
- Never place more than 8 person-hours for one assignee on one workday.
- Tasks are sequential by exact time: 4h from 10:00 ends 14:00; the successor starts 14:00.
- When a task crosses the end of the day, continue the remainder from 10:00 on the next workday.
- Write plan dates/duration first, then write `DEPENDS_ON`; verify after both steps.

## Verification

`POST task.item.getdata {"TASKID":<id>}` → returns `DEPENDS_ON` (array of predecessor IDs as
strings), `START_DATE_PLAN`, `END_DATE_PLAN`. Sample every person's chain plus one
cross-module joint and one marker task.

## UI display notes (tell the user)

- The Gantt tab does not live-update: hard refresh (Ctrl+F5) after REST writes.
- Red bars = PRIORITY=2 («Важная задача»), not overdue and not an error.
- A multi-day bar visually spans weekends; effort is still counted in workdays only, max 8 person-hours per workday.
- Row duplicates in the left list come from the view's «Группировка» (a task shows in every
  matching group), not from duplicated data — verify via API count if in doubt.
- Old deadlines conflicting with new plan dates make bars/markers look alarming; offer a
  batch to align DEADLINE with END_DATE_PLAN.

## Misc gotchas

- PowerShell 5.1 `Out-File -Encoding utf8` writes a BOM; strip it before `JSON.parse` in Node.
- Webhook rate limit ~2 req/s: pause 250–400 ms between sequential calls.
- Russian production calendar: no public holidays between 12.06 and 04.11; still keep
  `config.holidays` for other windows and corporate days off.
