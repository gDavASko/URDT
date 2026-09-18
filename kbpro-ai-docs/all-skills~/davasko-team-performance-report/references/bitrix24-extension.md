# Bitrix24 Task Layer

This layer is a working part of the report pipeline. Use
`../davasko-bitrix-team-stats` to fetch, compute, and consolidate Bitrix24 task
statistics. Do not count Bitrix tasks manually in the report skill.

If `KBPRO_BITRIX24_WEBHOOK_BASE` and `PERF_AUDIT_REPORTS` are available, Bitrix
task data is required for the final HTML. If access is missing, the report must
say that task data was unavailable for this run and must not invent task
numbers.

## Source Skill

Before running Bitrix collection, read:

- `../davasko-bitrix-team-stats/SKILL.md`;
- `../davasko-bitrix-team-stats/references/b24-contracts.md`;
- `../davasko-bitrix-team-stats/references/b24-projects.json`.

The source skill produces:

- per-employee `<reports>/bitrix/<slug>/bitrix-summary.json`;
- team summary
  `<reports>/bitrix/_consolidated/<date>/b24-consolidated-summary.json`;
- dashboard `<reports>/bitrix/_consolidated/<date>/b24-dashboard.html`;
- attention flags and data-quality warnings.

## Required Input

- Bitrix24 webhook/env is read-only and comes only from
  `KBPRO_BITRIX24_WEBHOOK_BASE`.
- Employee Bitrix IDs mapped to report employees.
- Analysis interval matches the Git/MR interval.
- Task fields include project/group, status history, responsible, creator,
  deadline, estimate/complexity in person-hours, reopen/return signals, and
  links or text that can be matched to commits/MR when present.

## Working Commands

Use the exact commands documented by `davasko-bitrix-team-stats`; the report
skill only orchestrates and consumes the outputs.

```bash
node ../davasko-bitrix-team-stats/scripts/b24fetch.js --from <from> --to <to> --out <reports>/bitrix/raw-<from>-<to>.json
node ../davasko-bitrix-team-stats/scripts/b24projects.js --raw <reports>/bitrix/raw-<from>-<to>.json --out <reports>/bitrix/group-signals.json
node ../davasko-bitrix-team-stats/scripts/b24metrics.js --raw <reports>/bitrix/raw-<from>-<to>.json --config <reports>/bitrix/run-config.json --employee <slug> --out <reports>/bitrix/<slug>/bitrix-summary.json
node ../davasko-bitrix-team-stats/scripts/b24consolidate.js --inputs <employee summaries> --out-dir <reports>/bitrix/_consolidated/<date>/ --verdicts <reports>/bitrix/verdicts.json
```

## Metrics To Bring Into The HTML

| Metric | Meaning | Why it matters |
|---|---|---|
| Closed tasks/week | Count of completed tasks normalized by available weeks | Product throughput |
| Weighted closed hours/week | Sum of completed task complexity in person-hours per week | Better than raw task count |
| Median task complexity | Typical task size | Separates many tiny tasks from hard tasks |
| Complexity-weighted throughput | Closed tasks weighted by hours and capped for outliers | Main Bitrix productivity signal |
| Reopen/return rate | Returned/reopened tasks divided by closed tasks | Product quality / requirement fit |
| Flow correctness | Share of tasks following expected statuses: accepted -> in progress -> review/test -> done | Process discipline |
| Time to first action | Created/assigned to first meaningful activity | Responsiveness |
| Cycle time | Start to done, by task and by weighted hours | Delivery predictability |
| WIP pressure | Simultaneously active tasks | Overload/context-switch risk |
| Project mix | Projects/groups worked on in the interval | Product context |
| Bugs closed/week | Bug throughput | Support load |
| Bug reopen rate | Returned bugs | Fix quality |
| Estimate coverage | Share of tasks with complexity filled | Data quality |
| Estimate accuracy | Actual cycle vs planned hours when available | Planning quality |

## Git/MR Join

Join tasks to commits/MR using, in priority order:

1. explicit task ID in branch, MR title, commit body, or MR description;
2. Bitrix URL in MR/commit text;
3. same project + same employee + overlapping dates + similar title keywords;
4. manual mapping file confirmed by user.

Derived join metrics:

- tasks with linked commits/MR;
- commits/MR without task;
- closed task complexity with no Git footprint;
- high Git volume with few tasks closed;
- high task complexity with low Git volume;
- reopened tasks whose linked commits had high rework/fixed-by-others.

## Ranking Contribution

Bitrix metrics affect the same four executive score axes:

| Score axis | Bitrix signals |
|---|---|
| Practical value | weighted closed hours/week, closed tasks/week, project mix, high-complexity task delivery |
| Quality/stability | reopen/return rate, bug reopen rate, reopened tasks linked to Git rework |
| Pace/throughput | average tasks/week, average complexity/week, cycle time |
| Process transparency | flow correctness, estimate coverage, task-to-MR/commit traceability |

Use weighted task complexity, not raw task count, as the main Bitrix
productivity signal. A person closing two 40-hour tasks should not look weaker
than a person closing ten tiny tasks.

## Interpretation

Bitrix data explains product/task load. It does not replace Git/MR evidence.

Examples:

- Low Git volume + high weighted task complexity can mean analytical/design or
  prefab-heavy work not visible as large code output.
- High Git volume + low closed task complexity can mean unmanaged work, task
  hygiene problems, or large refactoring not represented in Bitrix.
- High reopen rate + high fixed-by-others is a strong quality risk.
- Low approval coverage + high closed task count is a review-process risk.
