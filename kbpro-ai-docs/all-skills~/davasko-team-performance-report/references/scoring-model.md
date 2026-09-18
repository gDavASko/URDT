# Scoring Model

Use this model for the executive ranking table. All numbers must trace to
machine data or explicitly marked qualitative evidence.

## Axes

| Axis | Weight | Meaning | Strong signals | Risk signals |
|---|---:|---|---|---|
| Practical value | 40% | Delivered useful work in product or infrastructure | completed modules/stages, high effective volume, useful prefab/asset integration, cross-project delivery | low effective volume, weak completion, mostly tiny config churn |
| Quality/stability | 30% | Work does not create costly follow-up | low fixed-by-others, low self-fix, low churn, no reverts, good rule compliance, good architecture | repeated fix/rework cycles, fixes by others, debug logs, coupling, weak completion |
| Pace/throughput | 20% | Work rate normalized by available time | commits/month, MR/month, active workdays, files/month | low rhythm after absence normalization, low output |
| Process transparency | 10% | Work is reviewable and traceable | high MR approval coverage, low no-MR share | many commits not found in merged MR, low approvals |

## Bitrix24 Contribution

When `../davasko-bitrix-team-stats` data exists, Bitrix task metrics are part of
the same score, not a separate appendix.

| Axis | Git/MR share | Bitrix24 share | Bitrix24 signals |
|---|---:|---:|---|
| Practical value | 60% | 40% | weighted closed hours/week, closed tasks/week, project mix, high-complexity task delivery |
| Quality/stability | 65% | 35% | reopen/return rate, bug reopen rate, reopened tasks linked to Git rework |
| Pace/throughput | 55% | 45% | average tasks/week, average complexity/week, cycle time |
| Process transparency | 60% | 40% | flow correctness, estimate coverage, task-to-MR/commit traceability |

If Bitrix data is unavailable, score from Git/MR only and show a visible
coverage warning in the HTML. Do not silently treat missing Bitrix as zero.

Use task complexity in person-hours as the main Bitrix productivity weight.
Raw task count is a secondary context metric because it overvalues tiny tasks.

## Employee Ranking

Rank only comparable roles together. Use separate treatment for:

- CTO / technical lead infrastructure work;
- AI rules / harness / skill / knowledge-base work;
- people explicitly excluded by the user.

Use labels:

- `strong`: high total score with no critical risk;
- `solid`: good contributor with limited scale or one process issue;
- `middle`: normal contributor, useful but not leading;
- `risk`: materially below group or blocked by quality/process issues.

Avoid HR/legal finality. The report may support salary-level discussion, but
must phrase conclusions as evidence-based performance observations.

## Metric Interpretation

Do not score speed alone. Pair speed with quality:

- commits/files/effective volume with fixed-by-others, self-fix, churn;
- MR/month with approval coverage and no-MR share;
- closed tasks/week with weighted hours/week, reopen rate, and flow correctness;
- active days with vacations normalized;
- complexity/criticality with architecture and completion.

Prefab-heavy Unity work:

- count prefab files and asset files explicitly;
- explain product value: scene assembly, instrument setup, animation/sound
  wiring, tutorial objects, UI states;
- if effective volume is low despite many prefabs, say that the changed prefab
  objects/links look lighter than major module deliveries, not that prefab work
  was ignored.
