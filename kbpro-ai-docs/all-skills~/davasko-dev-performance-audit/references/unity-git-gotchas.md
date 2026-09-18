# Unity / Git Gotchas

These rules explain why the scripts are conservative. Treat them as mandatory
interpretation rules for every report.

## Unity Asset Noise

- `.prefab` and `.asset` YAML diffs can be thousands of lines because Unity
  reserializes fileIDs, component order, or local IDs.
- Effective LOC for assets is object/component touch count, not raw added +
  deleted lines.
- `.unity` scenes are outside v1 analysis and go to the aggregate "other"
  bucket.
- Binary assets are counted as touched files only; no LOC.

## Git History Edges

- Merge commits are excluded from author metrics.
- `--all` is required: feature-branch work may not be on default branch yet.
- `--find-renames` is required or rename becomes delete+add.
- Patch-id deduplicates cherry-picks and branch duplicates, but squash merges
  are opaque. Squash-like commits are flagged `SQUASH_OPAQUE` when they cannot
  be safely deduplicated.
- Submodule commits can appear both in a submodule and a superproject context;
  sha deduplication prevents double-counting.
- Shallow clones degrade blame/rework attribution and must raise
  `SHALLOW_LIMITED`.

## Rework Interpretation

- m8 and m9 are candidates for human review only.
- Hub files (DI registries, constants, generated indices, shared config) inflate
  rework counts; `reworkIndex.js` dampens them with `hub_weight`, but the report
  must still mention the limitation.
- A repeated touch may be a normal iteration, follow-up scope, changed
  requirements, or a bug fix. Git alone cannot decide.

## Phase Comparability

- Content-heavy and system-heavy phases are not directly comparable.
- The final analyst must state each phase profile and avoid "in the raw" claims
  when profiles differ.
- Holidays, sickness, support rotations, releases, and emergency hotfix periods
  can dominate rhythm and volume metrics.

## Non-Unity Repositories

- v1 contributes counters from non-Unity repos but does not apply KBPro
  architecture rubrics to them.
- When a window is mostly non-Unity work, m6/m17 and related axes should be
  `N/A` or low-confidence partials, not invented scores.
