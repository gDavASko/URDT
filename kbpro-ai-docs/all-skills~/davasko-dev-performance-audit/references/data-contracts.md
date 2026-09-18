# Data Contracts (schema_version 2.0)

Single source of truth for JSON shapes passed between scripts and agents.
All scripts import `scripts/lib/common.js` for constants, window slicing,
git/GitLab helpers, and JSON IO. Machine JSON is written compact (`writeJson`);
human-adjacent JSON may use `writeJsonPretty`.

`consolidate.js` refuses summaries with `schema_version != 2.0` — v1 runs must
be re-computed, never mixed in.

## Storage layout (plan v2 §3.5 — NOTHING on drive C)

Environment for every run (set by the orchestrator after the interview):

```
PERF_AUDIT_CACHE   = <non-C dir>   e.g. E:\perf-audit\cache      (partial clones, selfcheck-report.json)
PERF_AUDIT_REPORTS = <non-C dir>   e.g. E:\perf-audit\reports    (all run artifacts, OUTSIDE any git repo)
```

```
<PERF_AUDIT_REPORTS>/
├── run-state.json                     # incremental pipeline checkpoint (below)
├── <author-slug>/<run-date>/
│   ├── run-config.json                # interview results
│   ├── mr-data.json                   # fetchMrData.js output
│   ├── registry/
│   │   ├── dump-<window_id>.json      # dumpWindow.js
│   │   ├── window-<window_id>.json    # metrics + llm_scores + rework
│   │   └── rework-index.json          # reworkIndex.js
│   ├── metrics-summary.json           # mergeRegistry.js
│   ├── report.md                      # final analyst (RU)
│   └── dashboard.html                 # single-employee view
└── _consolidated/<run-date>/
    ├── consolidated-summary.json
    ├── verdicts.json                  # collected analyst verdicts
    └── dashboard.html                 # people view (main deliverable)
<PERF_AUDIT_CACHE>/selfcheck-report.json
```

## run-config.json

```json
{
  "schema_version": "2.0",
  "author": { "display": "str", "slug": "kebab-slug", "ids": ["email", "nick"], "usernames": ["gitlab-username"] },
  "phases": [{ "id": "P1", "name": "str|null", "from": "YYYY-MM-DD", "to": "YYYY-MM-DD" }],
  "innovation": {
    "name": "str",
    "introduced_at": "YYYY-MM-DD|null",
    "per_dev_adoption_dates": { "<author-slug>": "YYYY-MM-DD" }
  },
  "absences": [{ "from": "YYYY-MM-DD", "to": "YYYY-MM-DD", "note": "vacation" }],
  "repos": [{ "id": "group/project", "clone_url": "https://...", "profile": "unity|other" }],
  "cache_dir": "abs path (non-C)",
  "detail_level": "summary|full"
}
```

`innovation.per_dev_adoption_dates` is optional; when absent, mergeRegistry
adds the `ADOPTION_BY_DATE_ONLY` flag. `absences` windows overlapping >= 10
days or >= 30% of a window mark it `EXCLUDED_ABSENCE` (dropped from aggregates).

## run-state.json (incremental pipeline, plan v2 §4в)

Written by the ORCHESTRATOR after each finished employee; on session restart,
resume from the first employee not in `done`.

```json
{
  "schema_version": "2.0",
  "run_date": "YYYY-MM-DD",
  "employees": ["slug1", "slug2"],
  "done": ["slug1"],
  "in_progress": "slug2|null",
  "consolidated_at": "ISO|null",
  "notes": []
}
```

## dump-<window_id>.json (dumpWindow.js) — unchanged core, v2 notes

Same shape as v1: `window_id, phase, range{from,to,days}, authors, repos[],
path_table[], commits[], new_modules_signals[], excluded{}, diff_samples[],
notes[]`. v2 changes:
- file `kind` gains `code` (non-Unity sources; `eff_loc = loc_add + loc_del`);
- `new_modules_signals.cs_module_class` fires only for classes INHERITING
  `LogicSystem`/`GameComponent` (has `base` field);
- patch-ids computed in batches (no per-commit process spawn).
Commit text field for classification is `subject` (never `message`/`title`).

## window-<window_id>.json (computeMetrics.js + reworkIndex.js + analyzer)

```json
{
  "schema_version": "2.0", "rubrics_version": "2.0",
  "window_id": "P1_W1_2025-01-01", "phase": "P1",
  "range": { "from": "", "to": "", "days": 0 },
  "sample_gate": "OK|LOW_SAMPLE",
  "metrics": {
    "m1": { "cs": 0, "prefab": 0, "asset": 0, "shader": 0, "code": 0 },
    "m2": 0, "m3": 0,
    "m4": { "count": 0, "confirmed": null, "flag": null },
    "m5": { "commits_pm": 0, "files_pm": 0, "eff_loc_pm": 0 },
    "m8": { "added_cs_lines": 0, "churn_lines": 0, "self_fix_lines": 0,
             "churn_ratio": 0, "self_fix_ratio": 0 },
    "m9": { "added_cs_lines": 0, "fixed_lines_by_others": 0,
             "fix_commits_by_others": 0, "ratio": 0 },
    "m11": { "median_files": 0, "median_loc": 0, "monster_share": 0 },
    "m13": { "projects": 0, "systems": 0, "kinds": 0, "context_switches": 0 },
    "m15": { "active_days": 0, "active_working_days": 0, "working_days": 0,
              "active_share": 0, "max_gap_working_days": 0, "gini": 0 },
    "m16": { "reverted_by_others": 0, "self_reverts": 0 },
    "other_files_excluded": 0
  },
  "llm_scores": { "m6": { "band": [0,0], "confidence": "", "evidence": [], "notes": "" } },
  "rework_links": [{ "kind": "self_churn|self_fix|fixed_by_others|reverted_by_others",
                      "origin_sha": "", "origin_date": "", "rework_sha": "",
                      "rework_date": "", "repo": "", "file": "", "lines": 0, "age_days": 0 }],
  "source_dump": "dump-....json", "notes": []
}
```

Rework attribution: links live in the window of `origin_date` (plan v2 §3.2C).
`rework_links` are evidence CANDIDATES for human review, capped at 50/window.

## rework-index.json (reworkIndex.js)

```json
{
  "schema_version": "2.0", "engine": "blame-lines-v2", "horizon_days": 21,
  "blame_ops": 0,
  "hub_files_excluded": [{ "repo": "", "path": "", "touches": 0, "authors": 0 }],
  "flags": ["SHALLOW_LIMITED:<repo>", "REPO_MISSING:<repo>", "BLAME_BUDGET_EXHAUSTED"]
}
```

## mr-data.json (fetchMrData.js)

```json
{
  "schema_version": "2.0", "range": { "from": "", "to": "" },
  "usernames": [], "fetched_at": "ISO",
  "mrs": [{
    "repo": "group/project", "iid": 0, "author_username": "", "title": "",
    "state": "merged|opened|closed", "draft": false, "target_branch": "",
    "created_at": "ISO", "merged_at": "ISO|null", "closed_at": "ISO|null",
    "merge_commit_sha": "|null", "squash_commit_sha": "|null",
    "changes_count": "str|null", "labels": [],
    "approved_by": ["username"], "commit_shas": ["sha"]
  }],
  "flags": ["MR_LIST_FAILED:<repo>", "MR_DETAIL_BUDGET_EXHAUSTED"]
}
```

## metrics-summary.json (mergeRegistry.js)

```json
{
  "schema_version": "2.0", "rubrics_version": "2.0",
  "author": {}, "innovation": {}, "generated_at": "ISO",
  "analyzed_range": { "from": "", "to": "" },
  "guardrail_pairs": [{ "speed": "m5.commits_pm", "quality": "m9.ratio", "note": "" }],
  "phases": [{
    "id": "P1", "name": "", "from": "", "to": "", "profile": "system|content|mixed|unknown",
    "metrics": {
      "months": 0, "m1_pm": {}, "other_files_excluded_pm": 0,
      "m2_pm": 0, "m3_avg_per_window": 0, "m4": {}, "m5": {},
      "m8": { "added_cs_lines_pm": 0, "churn_ratio": 0, "self_fix_ratio": 0 },
      "m9": { "ratio": 0, "fix_commits_by_others_pm": 0 },
      "m11": {}, "m13": {}, "m15": {}, "m16_pm": {},
      "mr": { "merged_count": 0, "merged_pm": 0, "cycle_time_days_median": 0,
               "review_coverage": 0,
               "no_mr_share": 0, "no_mr_commits": 0 },
      "m6": { "band": [0,0], "confidence": "", "composition": "" },
      "m20": { "compared_to": "P1", "deltas_pm": {}, "slopes_prev": {},
                "slopes_cur": {}, "qualitative_trends": {} }
    },
    "slopes": { "m5.commits_pm": 0 },
    "confidence": {}, "windows": [], "excluded_windows": []
  }],
  "window_series": [{ "window_id": "", "phase": "", "from": "", "to": "",
                       "excluded_absence": false, "sample_gate": "",
                       "values": {}, "llm": {} }],
  "per_repo": [{ "repo": "", "commits": 0, "share": 0 }],
  "flags": ["UNVALIDATED", "ADOPTION_BY_DATE_ONLY", "MR_DATA_MISSING", "..."]
}
```

## verdicts.json (orchestrator collects from final analyst)

```json
{ "<author display>": { "verdict": "positive|neutral|negative|indeterminate", "reason": "RU sentence" } }
```

## consolidated-summary.json / dashboard DATA (consolidate.js)

```json
{
  "schema_version": "2.0", "generated_at": "ISO",
  "innovation": { "name": "" }, "source_files": [],
  "employees": [ /* metrics-summary.json objects */ ],
  "attention": { "<display>": [{ "metric": "m9.ratio", "delta": 0, "groupMedian": 0 }] },
  "verdicts": { "<display>": { "verdict": "", "reason": "" } },
  "warnings": []
}
```

`attention` = last-phase delta deviating from the group median by > 1.5*MAD
(3+ employees required). The dashboard template consumes exactly this object
via the `/*__DATA_JSON__*/null` placeholder.
