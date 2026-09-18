---
name: davasko-dev-performance-audit
description: >
  Innovation-effect audit (v2) over corporate GitLab (gitlab.kbpro.ru):
  did the lead's innovation (tool/process/rule) measurably change development?
  Blame-level rework ratios, MR approval metrics, per-month normalized volumes,
  guardrail speed/quality pairs, ITS-lite trend detection, incremental
  per-employee pipeline (1-6 devs), people dashboard with 1:1 attention flags.
  Triggers: "оценка сотрудника по коммитам", "анализ эффекта внедрения",
  "git-аналитика сотрудника".
---
<!-- skill metadata
required_reading:
  - references/data-contracts.md
  - references/metrics.md
  - references/rubrics.md
  - references/agent-prompts.md
  - references/unity-git-gotchas.md
  - references/consolidated-dashboard-example.html
plan: ../../PLAN-dev-performance-audit-skill.md (source of truth for all rules)
-->

# DavASko Dev Performance Audit v2

Plan of record: `kbpro-ai-docs/PLAN-dev-performance-audit-v2.md`.
Read references ON DEMAND per step, never all upfront (token economy):
`references/data-contracts.md` (JSON shapes), `references/metrics.md`
(metric catalog), `references/rubrics.md` (inject ONLY its "Compact excerpt"),
`references/agent-prompts.md` (spawn templates + tiering),
`references/report-template.md`, `references/unity-git-gotchas.md`.

## Role

You are the orchestrator. Node scripts do ALL deterministic work (discovery,
sync, dumping, counting, blame, merging, dashboards); LLM agents do ONLY
judgment (rubric scoring, spot-check judging, final report). You never count
anything yourself and never let an agent count.

**Purpose:** assess the effect of the LEAD's innovation on development — team
level AND each developer personally (self-vs-self). Deviations of one developer
from the group are 1:1 conversation topics, never verdicts about the person.
Verdicts: positive / neutral / negative / **indeterminate** (mandatory when
confounders explain the deltas). Readers: the lead, upper management
(management summary), 1:1 talks.

## Hard rules

1. **Nothing on drive C.** Cache, reports, temp dumps — all on a user-chosen
   non-C path (`PERF_AUDIT_CACHE`, `PERF_AUDIT_REPORTS`, default proposal
   `E:\perf-audit\...`). Scripts hard-fail on C: paths. Reports live OUTSIDE
   any git repo; ask the user where to put them every run.
2. **Blobless partial clones only** (`syncRepos.js` uses
   `--filter=blob:none`). Never full clones (repos are 5-8 GB); never offer a
   local-only fallback for a GitLab-wide audit — on token/access failure stop
   with setup instructions.
3. **Token economy:** agents never read skill docs; workers get one compact
   window dump + the rubrics Compact excerpt; the final analyst gets aggregates
   only; inter-agent traffic is English; every step is cached and reruns must
   not recompute or re-score unchanged data.
4. **Tiering (§4а):** analyzers = `fast-worker`; spot-check judge =
   `reasoning-architect` (fresh context, 10-15% of scored windows, min 1);
   final analyst and dispute resolver = `deep-reasoner`. Parallel analyzer
   waves of 3-4 when windows > 6. AI never self-validates; report carries
   `UNVALIDATED` until the judge pass completes.
5. **No simulated agents, no invented numbers.** Script/agent failure →
   surface the real error. Analyzer returns bad JSON → one strict-JSON retry →
   `ANALYZER_FAILED`, continue, disclose in the report.
6. **Guardrail pairs:** never report or conclude speed without its paired
   quality metric (`guardrail_pairs` in metrics-summary.json).
7. **No commit-title/comment metrics:** commit subjects and MR comments are not
   performance metrics. Do not score commit-message quality, commit-type
   profiles derived from subjects, time-to-first-review by comments, or review
   coverage by comments. MR review coverage means approvals only.
8. **Human gates:** no git writes, no wiki publication. `GITLAB_TOKEN` from
   env only, never written or echoed.
9. **No ranking.** Cross-employee output is the people table + attention flags
   (median ± 1.5 MAD), explicitly framed as 1:1 topics.

## Workflow

### Step 0 — Self-check (once per environment)

If `<PERF_AUDIT_CACHE>/selfcheck-report.json` is missing or FAIL:
`node scripts/selfCheck.js [--skip-gitlab] [--probe-repo <path>]`.
Blocked on FAIL in env/gitlab/git (includes: paths on C:, git < 2.27, missing
token). WARN in agents/judge → verify at runtime with a cheap echo subagent.

### Step 1 — Local knowledge base, then interview (Russian)

**FIRST read `references/known-facts.json`** — the skill's persistent local
knowledge base (stable facts recorded once: employees with git/B24 ids,
confirmed past absences, innovations timeline, known phase setups, project
registry pointer). Prefill run-config from it and ask the user ONLY about
missing or new facts. After the run, APPEND newly confirmed facts back into
the file (new absences, resolved gitlab usernames / b24 ids, new innovations)
— past periods never change, only extend. Never re-ask or re-mine what is
already recorded there.

Collect into `run-config.json` (contract in data-contracts.md):
innovation (name, introduction date(s), optional per-dev adoption dates —
enables diff-in-diff; without them flag `ADOPTION_BY_DATE_ONLY`);
phases (user sets boundaries empirically, typically ≤ 6 months each);
employees (1-6: ids/emails/usernames); absences per employee (vacation windows
→ `EXCLUDED_ABSENCE`); scope (default: whole GitLab discovery; non-Unity repos
contribute counters+volume only); **paths for cache and reports (NOT on C:)** —
export `PERF_AUDIT_CACHE` / `PERF_AUDIT_REPORTS` for all child scripts.

### Step 2 — Discovery, sync, author resolution (shared for the whole run)

```
node scripts/discoverRepos.js --from <min> --to <max> --author <id> --out <reports>/discovery.json
node scripts/syncRepos.js --projects <reports>/discovery.json [--recreate-full]
node scripts/resolveAuthor.js --author <id> --cache <cache> --gitlab
```

Confirm the project list and alias candidates with the user (iterate discovery
to fixed point, max 3 rounds). Warn that discovery is capped by token
visibility.

### Step 3 — Per-employee incremental loop (§4в)

Process employees STRICTLY one at a time; after each one the user already has
that person's finished report. Maintain `<reports>/run-state.json`
(contract in data-contracts.md); on restart resume from the first not-done.

For the current employee `<slug>` with run dir `<reports>/<slug>/<date>/`:

1. **MR data:** `node scripts/fetchMrData.js --projects discovery.json
   --usernames <u> --from <min> --to <max> --out <run>/mr-data.json`.
   This fetches MR approvals and commit shas; MR comments/notes are ignored and
   must not affect metrics because mandatory AI review comments pollute them.
2. **Windows:** slice phases via `sliceWindows` (monthly, remainder merges into
   the last month). Per window (skip cached windows whose repo tips are
   unchanged):
   `node scripts/dumpWindow.js --from --to --authors --cache --window-id --phase --out <run>/registry/dump-<id>.json`
   `node scripts/computeMetrics.js --dump ... --out <run>/registry/window-<id>.json`
3. **Analyzers:** for each `sample_gate == OK` window spawn a `fast-worker`
   with the analyzer template (window dump + rubrics Compact excerpt).
   Sequential ≤ 6 windows, else waves of 3-4. Merge strict-JSON outputs into
   window files.
4. **Rework:** `node scripts/reworkIndex.js --registry <run>/registry
   --authors <ids> --cache <cache>` (blame-level, origin-window attribution).
5. **Judge:** spot-check 10-15% of scored windows (min 1) with
   `reasoning-architect`; disagreement > 2 → re-run analyzer once; still
   disagreeing → `deep-reasoner` resolves that window.
6. **Merge:** `node scripts/mergeRegistry.js --run <run> [--validated]`
   (per-month normalization, ITS-lite slopes, absences, MR metrics without
   comment-derived fields).
7. **Report:** spawn `deep-reasoner` final analyst (template in
   agent-prompts.md) → `<run>/report.md` (RU) + verdict JSON. Generate the
   single-employee `dashboard.html` by replacing `/*__DATA_JSON__*/null` in
   `references/dashboard-template.html` with `{employees:[summary], verdicts,
   attention:{}}` (mechanical — do it yourself).
8. **Checkpoint + consolidate:** append verdict to
   `<reports>/_consolidated/<date>/verdicts.json`, update `run-state.json`,
   then `node scripts/consolidate.js --inputs <all done summaries> --out-dir
   <reports>/_consolidated/<date>/ --verdicts verdicts.json` — the people
   dashboard is valid after EVERY employee. When bitrix-team-stats summaries
   exist for the same phases (`<reports>/bitrix/<slug>/bitrix-summary.json`),
   add `--bitrix <reports>/bitrix` — task metrics merge into the SAME people
   table and per-employee pages (matched by slug/display).
9. Tell the user: "готово K из M", paths to the new report + updated
   consolidated dashboard. Continue to the next employee.

### Step 4 — Final team pass

After the last employee: spawn the final analyst once more on
`consolidated-summary.json` for the team-level summary (management summary
first). When bitrix data was merged (--bitrix), the analyst MUST cross-check
sources: git m9 (fixed-by-others) vs B24 b5 (reopen rate), git volume vs B24
throughput — discrepancies are findings, not data errors. Present: paths to
all reports/dashboards, judge agreement stats, active flags
(ADOPTION_BY_DATE_ONLY / LOW_SAMPLE / EXCLUDED_ABSENCE / UNVALIDATED /
MR_DATA_MISSING and what each weakens).

## Failure handling

- Script exit ≠ 0 → show stderr, fix or stop; never fabricate outputs.
- Missing `GITLAB_TOKEN` / API / discovery / sync / cache failure → stop with
  setup instructions; never substitute local history.
- Judge unavailable → subagent-fallback judge (fresh context); note the tier
  in the report.
- Machine judge for the rework engine:
  `node scripts/test/runFixtureTest.js` must PASS after any change to
  dumpWindow/computeMetrics/reworkIndex.
