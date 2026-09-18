---
name: bitrix-task-importer
description: "Upload JSON task array to Bitrix24. Triggers: залей задачи в Битрикс, импортируй JSON в Bitrix24, create tasks in Bitrix, run task import."
status: candidate
owner: KBPro
source:
  - references/api-contract.md
  - references/security.md
license: project-internal
allowed_tools:
  - filesystem-read
  - powershell
  - http-request
required_reading:
  - references/api-contract.md
  - references/security.md
known_risks:
  - Hardcoding a Bitrix webhook URL into a script or committing it — must use the KBPRO_BITRIX24_WEBHOOK_BASE env var.
  - Running the import twice and creating duplicate tasks (the API has no idempotency key).
  - Sending tags in tasks.task.add instead of the second task.item.update call (tags get lost).
  - Reading JSON with the wrong encoding and corrupting Cyrillic titles/descriptions.
  - Importing against the wrong workspace because GROUP_ID/RESPONSIBLE_ID were changed.
  - Importing, moving, or reassigning tasks without checking per-assignee capacity, causing more than 8 person-hours to land on the same working day.
---

# Bitrix Task Importer

## Purpose

Upload a JSON task backlog (an array of `{ "fields": { ... } }` objects) into Bitrix24 via the incoming webhook, using the project's two-step contract (`tasks.task.add` then `task.item.update` for tags). This skill is the operational counterpart of
[bitrix-task-json-builder](../bitrix-task-json-builder/SKILL.md).

## When To Use

* A `*_tasks.json` backlog exists and the user wants it created in Bitrix24.
* Re-running an import after fixing the JSON.

## Do Not Use

* For building or decomposing the JSON from a ТЗ — use `bitrix-task-json-builder`.
* For editing the field contract — that lives in the builder skill and the protocol.

## Core Rules (summary — full text in references/)

1. **Webhook is a secret.** The base URL comes from the `KBPRO_BITRIX24_WEBHOOK_BASE` environment variable. Never hardcode or commit it. See [security.md](references/security.md).
2. **Two-step per task**: `tasks.task.add.json` for the main fields, then `task.item.update.json` (legacy API) to set `TAGS` reliably. See [api-contract.md](references/api-contract.md).
3. **Tags only via the second call**, never inside `tasks.task.add`.
4. **UTF-8 throughout**: read the JSON as UTF-8 and send bodies as `application/json; charset=utf-8`.
5. **Capacity fields are mandatory**: `UF_TASK_CAPACITY` / `UF_TASKS_TASK_1783529349965` is an integer person-hour estimate (`1`, `4`, `8`, `20`, `77`). If the source gives person-days, convert once as `ceil(personDays * 8)`.
6. **Capacity-aware Gantt is mandatory**: before creating, moving, or reassigning tasks, verify the target assignee's schedule. One working day may contain at most `8` total person-hours for that assignee inside `10:00-18:00 MSK`. Set `DURATION_TYPE="hours"` and `DURATION_PLAN` to the same integer hour estimate, and sync `UF_TASKS_TASK_1783529349965` to the same hour value. If tasks are inserted first, shift later tasks forward instead of overlapping them. Visible arrows require legacy `DEPENDS_ON` via `task.item.update` after dates are valid.
7. **No idempotency**: running twice creates duplicates. Confirm before re-running.

## Workflow

1. **Verify the secret**: ensure `KBPRO_BITRIX24_WEBHOOK_BASE` is set in the shell/user environment. If missing, stop and ask the user to set it (do not invent a URL).
2. **Validate the JSON**: confirm it parses as an array and every item has the required `fields` (`TITLE`, `GROUP_ID`, `RESPONSIBLE_ID`, `PRIORITY`, `ALLOW_TIME_TRACKING`, and `UF_TASK_CAPACITY` or `UF_TASKS_TASK_1783529349965`). Capacity must parse as an integer person-hour value.
3. **Capacity/Gantt dry summary**: report how many tasks will be created, to which `GROUP_ID`/`RESPONSIBLE_ID`, the total person-hours per assignee, and whether any working day would exceed `8` person-hours. Get user confirmation (creating tasks is an outward-facing action).
4. **Run** [scripts/import-bitrix-tasks.js](scripts/import-bitrix-tasks.js) with `-JsonPath <path>`.
5. **Per task**: the script sends `tasks.task.add`, captures the new task id, and if `TAGS` exist sends `task.item.update`.
6. **Report**: created task ids, applied tags, and any per-task errors.

## Output Format

* A run log: for each task — title, resulting task id (or error), and tag-update status.
* A final summary line: total created / total failed.

## Test Prompts

1. "Залей examples/sample-backlog.json в Битрикс (вебхук уже в KBPRO_BITRIX24_WEBHOOK_BASE)."
2. "Проверь JSON и покажи, сколько задач создастся и в какую группу, но пока не импортируй."
3. "Импорт упал на третьей задаче — разбери ответ Bitrix и подскажи причину."
