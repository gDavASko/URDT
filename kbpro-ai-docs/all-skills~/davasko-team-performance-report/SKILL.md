---
name: davasko-team-performance-report
description: >
  Create executive HTML performance reports for KBPro teams from Git/GitLab
  commits, MR approvals, and Bitrix24 task statistics. Use when the user
  asks to evaluate developers, compare team members, rank employees for
  management, explain why someone scored high/low, or generate a leadership
  report for a date interval and employee list.
---

# DavASko Team Performance Report

## Role

Build a management-facing performance report, not a raw engineering dashboard.
The report must answer: who is stronger, who is middle, who is weaker, and why.
Every score must be explained with concrete evidence from commits, files,
modules, MR approvals, and Bitrix24 tasks.

Use the existing `davasko-dev-performance-audit` scripts as the Git/MR data
engine when available. This skill defines the report product, language, ranking
logic, and Bitrix24 extension.

## Inputs

Ask for or infer only these required inputs:

- analysis interval: `from` and `to` dates;
- employee list: display name plus known git emails/usernames;
- output directory, default `E:\perf-audit\reports`;
- repositories scope, default all discovered GitLab projects;
- absences/vacations if known;
- Bitrix24 availability: `KBPRO_BITRIX24_WEBHOOK_BASE` and
  `PERF_AUDIT_REPORTS` env vars, or explicit user confirmation that Bitrix24
  must be skipped for this run.

Do not include employees explicitly excluded by the user in the final report.
Do not mention excluded employees in visible report text.

## Hard Rules

1. Output is **HTML only** unless the user explicitly asks for another format.
2. Use Russian for report text. Write for CTO, producer, and upper management:
   no unexplained abbreviations, no internal agent chatter, no service notes
   about changed formulas, no insulting or casual labels.
3. Do not use commit-title/message quality as a metric.
4. Do not use MR comments/notes as a metric. Mandatory AI reviews pollute them.
   MR review coverage means approvals only.
5. Do not validate human performance from one number. Use weighted scoring plus
   concrete metric-level evidence.
6. Account for vacations/absences through period normalization. Do not hide a
   person behind “no data”; score from available normalized data and disclose
   coverage.
7. Prefab/asset-heavy Unity work must be visible. Report C#, prefab, asset,
   shader, and other meaningful kinds separately where they explain a score.
8. Давлетбаев-style CTO/AI-infrastructure work must be separated from ordinary
   developer ranking when the commits show rules, skills, harnesses, knowledge
   base, and AI tooling rather than product feature delivery.

## Workflow

### 1. Prepare Data

Use the Git/MR pipeline from `../davasko-dev-performance-audit`:

- discover and sync repos for the interval;
- resolve author identities;
- dump monthly windows;
- compute metrics;
- fetch MR approvals and MR commit shas only;
- merge per-employee summaries;
- read LLM evidence files for qualitative examples.

If data already exists under `E:\perf-audit`, reuse it. Do not mutate raw dumps
unless the user explicitly asks to recompute.

### 2. Collect Bitrix24 Task Data

Use the existing `../davasko-bitrix-team-stats` skill as the Bitrix24 engine.
Do not reimplement Bitrix24 REST fetching in this skill.

Read these files before a Bitrix-enabled run:

- `../davasko-bitrix-team-stats/SKILL.md`;
- `../davasko-bitrix-team-stats/references/b24-contracts.md`;
- `../davasko-bitrix-team-stats/references/b24-projects.json`.

Bitrix24 is a normal input source, not a future optional idea. If
`KBPRO_BITRIX24_WEBHOOK_BASE` and `PERF_AUDIT_REPORTS` are set, run the
Bitrix pipeline for the same interval and employee list:

```bash
node ../davasko-bitrix-team-stats/scripts/b24fetch.js --from <from> --to <to> --out <reports>/bitrix/raw-<from>-<to>.json
node ../davasko-bitrix-team-stats/scripts/b24projects.js --raw <reports>/bitrix/raw-<from>-<to>.json --out <reports>/bitrix/group-signals.json
node ../davasko-bitrix-team-stats/scripts/b24metrics.js --raw <reports>/bitrix/raw-<from>-<to>.json --config <reports>/bitrix/run-config.json --employee <slug> --out <reports>/bitrix/<slug>/bitrix-summary.json
node ../davasko-bitrix-team-stats/scripts/b24consolidate.js --inputs <employee summaries> --out-dir <reports>/bitrix/_consolidated/<date>/ --verdicts <reports>/bitrix/verdicts.json
```

Follow the Bitrix skill's own rules:

- webhook comes only from `KBPRO_BITRIX24_WEBHOOK_BASE`;
- read-only Bitrix REST, no task writes;
- output must not go to drive C;
- mappings of Bitrix users, projects, and bug rules must be confirmed and
  cached by the Bitrix skill contract;
- never count tasks manually when the Bitrix scripts can compute them.

If Bitrix24 env/access is missing, the final HTML must contain a visible
data-coverage warning: "Bitrix24 task data was not available for this run".
Do not invent task metrics.

### 3. Join Git/MR With Bitrix24

When Bitrix summaries exist, merge them into the same employee pages and the
ranking table. Use `references/bitrix24-extension.md` for the exact metrics and
interpretation.

Join evidence in priority order:

- explicit Bitrix task ID in branch, MR title, MR description, commit body, or
  commit message;
- Bitrix task URL in MR/commit text;
- same project, same employee, overlapping dates, similar task/title keywords;
- manually confirmed mapping file from the Bitrix skill.

The report must show findings such as:

- high task complexity with modest Git volume;
- high Git volume with weak task closure;
- reopened tasks connected to rework/fixed-by-others in Git;
- tasks closed without linked MR;
- commits/MR without task trace.

### 4. Score Employees

Use the scoring model in `references/scoring-model.md`.

The default weights:

- practical value: 40%;
- quality/stability: 30%;
- pace/throughput: 20%;
- process transparency: 10%.

Adjust only with a clear written reason. Keep Davletbaev/CTO infrastructure in
a separate section, not in the same weighted developer table.

### 5. Explain Every Metric

Use `references/report-writing-rules.md`.
For concrete examples copied from the current accepted report style, read
`references/example-report-pattern.md`.

For every employee page, the metric table must contain:

- metric name;
- weight/category;
- latest-period value;
- signal: strong side / normal context / risk point;
- concrete example: file, module, repo, commit sha, or product area.

The last column must not say what the metric means in general. It must say why
this employee's value is high/low/important.

### 6. Include Concrete Extremes

For each employee identify visible extremes:

- highest/lowest compared with the group;
- sharp delta from previous phase;
- unusually high prefab/asset share;
- high useful volume with low quality guardrails;
- low volume but good process quality;
- many MR without approvals;
- many commits not found in merged MR;
- repeated fix/rework cycles in the same product area;
- strong architecture evidence such as shared base extraction or framework
  reuse;
- weak architecture evidence such as direct core coupling, debug logs left in
  gameplay, or widened encapsulation.

Describe each extreme in product language, with a concrete example.

### 7. Build HTML

Use `assets/executive-report-template.html` as the layout pattern:

- top navigation tabs;
- `Рейтинг` tab with management summary and ranking table;
- one tab per employee;
- separate CTO/AI-infrastructure tab when applicable;
- `Методика` tab;
- wide tables, no clipped columns;
- no duplicated management summary on employee tabs.

If using the current completed report as an example, treat
`E:\perf-audit\reports\_consolidated\2026-07-16\relative-ranking-report.html`
as the visual/content style reference.

## Deliverables

Always provide:

- path to the final HTML report;
- short summary of what was generated;
- any missing data warnings;
- verification performed, such as searches for excluded metrics and checks that
  each employee tab has concrete metric evidence.
