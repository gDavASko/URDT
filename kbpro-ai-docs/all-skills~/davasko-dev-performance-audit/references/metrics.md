# Metrics Reference (schema_version 2.0)

English only. LLM must never invent numeric values; every number traces to a
script named below. Composite axes from v1 (productivity=commits+files,
breadth=systems+projects, growth=sum|deltas|) are REMOVED — they mixed units
and produced garbage. Metrics are reported per-metric, in groups, with
guardrail pairs.

## Principles (plan v2)

1. **Per-month normalization everywhere.** Phase values divide by phase months
   (`days / 30.4375`), never by window count. Ratios need no normalization.
2. **Guardrail pairs.** A speed metric is only reported next to its quality
   pair (`guardrail_pairs` in metrics-summary.json). "Faster" without the pair
   is a forbidden conclusion.
3. **Origin attribution.** Rework/reverts attribute to the window where the
   original line/commit was BORN, not where the fix landed — otherwise the
   after-innovation phase gets blamed for before-innovation defects.
4. **Ratios, not absolutes,** for rework (share of added lines) — absolute link
   counts punished productive people.
5. **Iteration != rework.** Own non-fix commits rewriting own recent lines are
   churn (neutral); own FIX commits rewriting own recent lines are self-fix
   rework (quality signal).
6. **Working days only** for rhythm metrics (RU calendar approximation).
7. **Self-vs-self.** Deltas compare an employee's phases; cross-employee
   comparison exists only as outlier detection for 1:1 topics (consolidate.js).

## Metric classes

- **numeric**: deterministic script output. LLM must not touch.
- **hybrid**: script raw count + LLM refinement (skipped on LOW_SAMPLE → `RAW_ONLY`).
- **qualitative**: LLM band vs `rubrics.md` (suppressed on LOW_SAMPLE).
- **derived**: cross-window/phase (m20, slopes).

Sample gate (adaptive, plan v2 §3.2K): `OK` when the window has >= 10 cs
commits OR >= 30 effective asset LOC; else `LOW_SAMPLE`.

## Group A — Flow & speed

| id | name | class | formula | computed by | pair (quality) |
|----|------|-------|---------|-------------|----------------|
| m2_pm | commits per month | numeric | patch-id-deduped non-merge commits / months | dumpWindow + mergeRegistry | m16_pm.reverted_by_others |
| m5.commits_pm / files_pm / eff_loc_pm | volume per month | numeric | commits, unique analyzed files, effective LOC / months; excluded `other` files reported separately (`other_files_excluded`) and never inflate volume | computeMetrics + mergeRegistry | m9.ratio / m8.self_fix_ratio |
| mr.merged_pm | merged MRs per month | numeric | merged MRs authored by subject / months | fetchMrData + mergeRegistry | mr.review_coverage |
| mr.cycle_time_days_median | MR cycle time | numeric | median(merged_at - created_at) | fetchMrData + mergeRegistry | m9.ratio |
| m13.context_switches_pm | context switches | numeric | repo changes in chronology; superproject+submodule commits within 1h = one context | computeMetrics | — (context) |

## Group B — Quality & stability (guardrails for A)

| id | name | class | formula | computed by |
|----|------|-------|---------|-------------|
| m8.churn_ratio | iteration churn | numeric | own lines deleted by own NON-fix commits within 21d / added cs lines (blame-level, hubs excluded). NEUTRAL context, not a defect signal | reworkIndex |
| m8.self_fix_ratio | self-fix rework | numeric | own lines deleted by own FIX commits within 21d / added cs lines | reworkIndex |
| m9.ratio | fixed by others | numeric | own lines deleted by OTHERS' fix commits within 21d / added cs lines (word-boundary fix detection) | reworkIndex |
| m9.fix_commits_by_others_pm | others' fix commits | numeric | distinct others' fix commits touching subject lines / months | reworkIndex |
| m16_pm | reverts | numeric | reverts of subject commits by others + self reverts, per month, origin-window attribution | computeMetrics + reworkIndex |
| mr.review_coverage | approval coverage | numeric | merged MRs with >=1 approval / merged MRs. MR comments/notes are ignored because mandatory AI review comments pollute this signal | fetchMrData + mergeRegistry |
| mr.no_mr_share | landed without MR | numeric | subject commits absent from all MR commit sets / all subject commits (includes not-yet-merged branches — documented caveat) | fetchMrData + mergeRegistry |

Hub files (touches > P90 AND authors >= 3 per repo) are EXCLUDED from all
rework metrics, listed in `rework-index.json.hub_files_excluded`.

## Group C - Discipline & structure

| id | name | class | formula / rubric | computed by |
|----|------|-------|-------------------|-------------|
| m11 | granularity | numeric | median files, median LOC per commit, monster share (>30 files) | computeMetrics |
| m6 | rules compliance (0-10) | qualitative | rubric m6 | analyzer |
| m17 | architectural discipline (0-10) | qualitative | rubric m17 | analyzer |
| m18 | completeness (0-10) | qualitative | rubric m18 | analyzer |

Commit subjects are excluded from performance metrics. They may remain in raw dumps for traceability and internal heuristics, but reports and scores must not use commit-title quality or commit-title classification.

Commit subjects are excluded from performance metrics. They may remain in raw dumps for traceability and internal heuristics, but reports and scores must not use commit-title quality or commit-title classification.

## Group D — Complexity & breadth (context, NOT KPI)

| id | name | class | formula / rubric | computed by |
|----|------|-------|-------------------|-------------|
| m7 | work complexity (0-10) | qualitative | rubric m7 | analyzer |
| m14 | criticality of zones (0-10) | qualitative | rubric m14 (higher = more critical, not better) | analyzer |
| m1_pm | changed files by kind | numeric | unique paths per kind cs/prefab/asset/shader/code, per month | computeMetrics |
| m3, m13 | systems / breadth counters | numeric | separate counters (repos, systems, kinds) — NEVER summed into one number | computeMetrics |
| m4 | modules delivered | hybrid | added .asmdef OR new class INHERITING LogicSystem/GameComponent; LLM confirms delivery | dumpWindow + analyzer |
| m15 | rhythm | numeric | active_share = active working days / working days; max gap in working days; Gini over working days | computeMetrics |

## Group E — Adoption & knowledge

| id | name | class | formula | computed by |
|----|------|-------|---------|-------------|
| adoption | adoption by dates | config | phase boundaries + optional per-dev adoption dates from the interview. Without per-dev dates the summary carries `ADOPTION_BY_DATE_ONLY` and verdict confidence is capped | run-config.json |
| diff-in-diff | adopters vs non-adopters | derived | only when per-dev adoption dates differ; otherwise N/A | final analyst (from summaries) |
| m20 | phase dynamics (ITS-lite) | derived | per-metric per-month deltas + least-squares slopes of the window series per phase; qualitative trends by non-overlap band rule | mergeRegistry |
| attention | group outliers | derived | employee's last-phase delta deviating from group median by > 1.5*MAD (needs 3+ employees) → 1:1 conversation topic | consolidate.js |

## Removed / replaced vs v1

- v1 m8/m9 pairwise file-touch "candidates" → blame-line ratios (defects A/B/C).
- v1 axes `criteria.*` → groups + guardrail pairs (defect D).
- v1 `growth = Σ|Δ|` → m20 signed deltas + slopes.
- v1 m19 "N/A in v1" → real MR metrics (`mr.*`).
- v1 synthetic `__other_N` in m1/m5 → `other_files_excluded` reported separately.

## Cross-references

- Schemas: `references/data-contracts.md`.
- Rubric anchors + compact excerpt: `references/rubrics.md`.
- Git/Unity edge cases: `references/unity-git-gotchas.md`.
- Machine judge for the rework engine: `scripts/test/runFixtureTest.js`.
