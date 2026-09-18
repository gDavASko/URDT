---
name: davasko-bitrix-team-stats
description: >
  Bitrix24 task statistics for innovation-effect audits: per-employee and
  per-phase metrics b1-b12 (throughput, cycle time, deadline discipline,
  reopen rate, estimate accuracy, WIP, task mix, complexity, self-assigned
  share, bug MTTR, time-to-first-action) from the tasks REST webhook.
  Standalone deliverable (RU report + people dashboard) + bitrix-summary.json
  consumable by davasko-dev-performance-audit. Triggers: "bitrix stats",
  "статистика битрикс", "статистика задач", "bitrix team stats",
  "аудит по задачам", "b24 audit".
---

# DavASko Bitrix24 Team Stats

Plan of record: `kbpro-ai-docs/PLAN-bitrix-team-stats-skill.md`.
References on demand: `references/b24-contracts.md` (JSON shapes),
`references/b24-dashboard-template.html` (placeholder `/*__DATA_JSON__*/null`).

## Role

Orchestrator of a Bitrix24 task-statistics audit. Node scripts do ALL
deterministic work (fetch, metrics, consolidation, dashboards); the LLM does
ONLY: (a) semantic mapping proposals confirmed by the user, (b) the final
analyst report. Never count tasks yourself; never invent numbers.

Purpose framing is identical to perf-audit v2: assess the LEAD's innovation
effect; per-employee deltas are self-vs-self; group deviations are 1:1
conversation topics; verdicts positive/neutral/negative/indeterminate.

## Hard rules

1. **Webhook from `KBPRO_BITRIX24_WEBHOOK_BASE` env only** — never logged,
   echoed, or written to any file. Read-only REST; no task writes ever.
2. **Nothing on drive C** — outputs under `%PERF_AUDIT_REPORTS%\bitrix\`,
   scripts hard-fail on C: paths.
3. **Rate limits**: all fetching goes through b24fetch.js (throttle ~2 rps,
   `batch` x50 for histories). Raw fetch is cached; re-runs reuse the cache
   unless `--force`.
4. **User conventions (decisions 2026-07-16)**:
   - dev task = tag `Dev` OR Dev token in the title;
   - bugs = tag `Баг`/`Bug` (live team convention, primary signal) OR title
     heuristic (fix/bug/баг/краш...); candidates confirmed on first run
     (`bug_rule.confirmed_ids/excluded_ids`), until then
     `BUG_RULE_HEURISTIC_UNCONFIRMED`;
   - project names come from the SHIPPED registry
     `references/b24-projects.json` (groupId -> name; ids 102/100/94/92/86/
     68/60/54/12 confirmed by the user 2026-07-16). For groupIds missing
     there, run b24projects.js, propose names from signals, confirm with the
     user and ADD them to the registry file.
5. **All mappings are confirmed by the user** (AskUserQuestion) and cached in
   `mapping-config.json`: Bitrix userId ↔ employee (ФИО ↔ git identity is
   semantic — transliteration!), groupId ↔ project name/repo, bug ids.
6. Guardrail pairs: closed/мес and cycle time are only interpreted next to
   reopen rate. No ranking; attention flags = 1:1 topics.
7. AI never self-validates: after any change to b24metrics.js run
   `node scripts/test/runB24FixtureTest.js` (machine judge, 18 assertions).
   Final analyst = `deep-reasoner`; mapping proposals need no LLM tier (you).

## Workflow

### Step 0 — Preconditions

`KBPRO_BITRIX24_WEBHOOK_BASE`, `PERF_AUDIT_REPORTS` set (non-C). Quick probe:
call `profile` via a 3-line node -e snippet; on error stop with setup hints.

### Step 1 — Local knowledge base, then interview (Russian)

**FIRST read the shared knowledge base**
`../davasko-dev-performance-audit/references/known-facts.json` (employees with
b24_id/git ids, confirmed absences, innovations timeline, phase setups) and
the local `references/b24-projects.json` (groupId -> project names). Prefill
run-config from them; ask ONLY about missing facts; append newly confirmed
b24_ids / absences / group names back after the run.

Then collect the rest into `<reports>/bitrix/run-config.json` (contract in
b24-contracts.md): innovation + phases (reuse the perf-audit values when the
audits run together); employees 1-6; absences per employee.

### Step 2 — Fetch + mappings (once per run)

```
node scripts/b24fetch.js --from <min> --to <max> --out <reports>/bitrix/raw-<from>-<to>.json
node scripts/b24projects.js --raw <raw> --out <reports>/bitrix/group-signals.json
```

Fetch WITHOUT `--responsible` (full context is needed for group inference and
task-mix). Then:
1. **Employee mapping**: from raw `users` (ФИО, должность) propose matches to
   the audited employees (semantic, transliteration-aware); confirm with the
   user; write `employees[].b24_ids` into run-config.
2. **Project inference**: from group-signals (top tags, title words,
   responsibles, activity range) propose a name/repo match per groupId;
   confirm; write `mapping-config.json`.
3. **Bug rule**: list heuristic bug candidates (titles only, ≤ 20 samples);
   user confirms/excludes; write `bug_rule` into run-config.

### Step 3 — Per-employee incremental loop

For each employee (strictly one at a time, checkpoint in
`<reports>/bitrix/run-state.json`):

```
node scripts/b24metrics.js --raw <raw> --config run-config.json --employee <slug> --out <reports>/bitrix/<slug>/bitrix-summary.json
```

Then spawn the final analyst (`deep-reasoner`) with the summary + innovation
context → `<slug>/report.md` (RU; same structure as perf-audit
report-template: management summary → guardrail pairs → dynamics → 1:1 topics
→ verdict JSON). Collect verdicts into `verdicts.json`, then:

```
node scripts/b24consolidate.js --inputs <all done summaries> --out-dir <reports>/bitrix/_consolidated/<date>/ --verdicts verdicts.json
```

Report progress ("готово K из M") + paths after every employee.

### Step 4 — Team pass + optional join with perf-audit

Final analyst once more on `b24-consolidated-summary.json` for the team view.
If a perf-audit run with the SAME phases exists, MERGE the sources into one
table: re-run the perf-audit consolidator with
`node ../davasko-dev-performance-audit/scripts/consolidate.js ... --bitrix
<reports>/bitrix` — bitrix summaries attach to git employees by slug/display,
and the unified dashboard shows B24 columns in the people table plus a
"Bitrix24: задачи" section on every personal page. The analyst cross-checks
sources (git volume vs task throughput, m9 fixed-by-others vs b5 reopen rate)
— discrepancies are findings, not errors.

## Failure handling

- Script exit ≠ 0 → show stderr, fix or stop; no fabricated data.
- Bitrix API errors inside batch are collected into flags
  (`HISTORY_ERRORS:<n>`), not fatal; report discloses them.
- Missing webhook/env → stop with setup instructions.
- Fields partially filled (estimates, complexity) → metrics carry
  `PARTIAL_FIELD_*` flags; the analyst must mention coverage.
