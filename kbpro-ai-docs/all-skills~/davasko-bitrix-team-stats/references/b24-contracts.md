# Bitrix Team Stats — Data Contracts (schema b24-1.0)

All machine JSON compact; human-adjacent pretty. Nothing on drive C.
Layout: `%PERF_AUDIT_REPORTS%\bitrix\`.

```
bitrix/
├── run-config.json
├── mapping-config.json          # confirmed group names / bug ids (cached forever)
├── raw-<from>-<to>.json         # b24fetch.js cache
├── group-signals.json           # b24projects.js (for project inference)
├── run-state.json               # incremental checkpoint {employees, done, in_progress}
├── <slug>/bitrix-summary.json   # b24metrics.js
├── <slug>/report.md             # final analyst (RU)
└── _consolidated/<date>/{b24-consolidated-summary.json, b24-dashboard.html, verdicts.json}
```

## run-config.json

```json
{
  "employees": [{ "slug": "ivanov", "display": "Иван Иванов", "b24_ids": [316],
                   "absences": [{ "from": "YYYY-MM-DD", "to": "YYYY-MM-DD" }] }],
  "phases": [{ "id": "P1", "name": "до внедрения", "from": "YYYY-MM-DD", "to": "YYYY-MM-DD" }],
  "innovation": { "name": "str", "introduced_at": "YYYY-MM-DD|null" },
  "bug_rule": { "heuristic": true, "confirmed_ids": [123], "excluded_ids": [456] }
}
```

## raw-<from>-<to>.json (b24fetch.js)

```json
{
  "schema_version": "b24-1.0", "range": {"from": "", "to": ""},
  "responsible": "all|[ids]", "fetched_at": "ISO",
  "tasks": [{ "id": 0, "title": "", "groupId": 0, "responsibleId": 0, "createdBy": 0,
               "createdDate": "ISO", "closedDate": "ISO|null", "deadline": "ISO|null",
               "status": 5, "timeEstimate": 0, "timeSpentInLogs": 0,
               "tags": ["Dev"], "complexity": 5.0 }],
  "histories": { "<taskId>": { "rows": [{ "date": "ISO", "field": "STATUS|DEADLINE|NEW",
                                            "from": "2", "to": "3", "userId": 316 }],
                                "first_row": {} } },
  "users": [{ "id": 316, "name": "Имя Фамилия", "position": "" }],
  "flags": ["HISTORY_PAGE_CAP:<id>", "HISTORY_ERRORS:<n>"]
}
```

Fetch strategy: (created in [from-180d..to]) UNION (closed in [from..to]) so
cycle time of tasks closed in-phase but created earlier is correct. History
keeps only NEW/STATUS/DEADLINE rows (compact); >=50 rows -> HISTORY_PAGE_CAP.

## bitrix-summary.json (b24metrics.js)

```json
{
  "schema_version": "b24-1.0",
  "author": { "display": "", "slug": "", "b24_ids": [] },
  "innovation": {}, "generated_at": "ISO", "analyzed_range": {},
  "phases": [{
    "id": "P1", "name": "", "from": "", "to": "", "months": 0,
    "metrics": {
      "b1": { "closed": 0, "closed_pm": 0 },
      "b2": { "cycle_days_median": 0, "start_to_close_days_median": 0 },
      "b3": { "with_deadline_share": 0, "hit_rate": 0, "overdue_days_median": 0 },
      "b4": { "deadline_shifts_avg": 0 },
      "b5": { "reopen_rate": 0, "reopened": 0, "reopen_events": 0 },
      "b6": { "estimate_ratio_median": 0, "sample": 0 },
      "b7": { "wip_avg": 0 },
      "b8": { "<tag>": 0 },
      "b9": { "complexity_avg": 0, "complexity_sum_pm": 0, "filled_share": 0 },
      "b10": { "self_assigned_share": 0 },
      "b11": { "bugs_closed": 0, "bugs_closed_pm": 0, "bug_mttr_days_median": 0 },
      "b12": { "time_to_first_action_days_median": 0, "sample": 0 },
      "counts": { "dev_closed": 0, "dev_created": 0, "all_seen": 0 },
      "b20": { "compared_to": "P1", "deltas_pm": {} }
    },
    "windows": [], "excluded_windows": []
  }],
  "window_series": [{ "window_id": "", "phase": "", "from": "", "to": "",
                       "excluded_absence": false, "values": {} }],
  "flags": ["PARTIAL_FIELD_COMPLEXITY", "PARTIAL_FIELD_ESTIMATE",
             "BUG_RULE_HEURISTIC_UNCONFIRMED", "EXCLUDED_ABSENCE:<wid>"]
}
```

Semantics:
- dev filter: tag `Dev` OR Dev token in title (b24common.isDevTask);
- closure metrics -> window of CLOSED_DATE; creation metrics -> CREATED_DATE;
- reopen = STATUS transition from {4,5} to {2,3} in history;
- phase metrics are pooled over included windows and normalized per month;
- b7 WIP is a created/closed-interval approximation (documented);
- b12 uses only kept history fields — an approximation of "first action".

## Consolidated DATA (b24consolidate.js -> dashboard placeholder)

```json
{ "schema_version": "b24-1.0", "generated_at": "ISO", "innovation": {},
  "employees": [ /* bitrix-summary objects */ ],
  "attention": { "<display>": [{ "metric": "b5.reopen_rate", "delta": 0, "groupMedian": 0 }] },
  "verdicts": { "<display>": { "verdict": "", "reason": "" } },
  "warnings": [] }
```

attention = last-phase delta deviating from group median by > 1.5*MAD (needs
3+ employees). Dashboard placeholder: `/*__DATA_JSON__*/null`.
