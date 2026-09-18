# Rubrics Reference (rubrics_version 2.0)

English only. Analyzer and judge must score only from the compact window dump,
diff samples, and metric context passed by the orchestrator. Use bands, not
points: `[min,max]`, `confidence: high|medium|low`, and mandatory evidence
entries `sha:file:reason`.

Rubric content is unchanged from 1.0 (it was correct); 2.0 adds the compact
excerpt below. Token economy (plan v2 §4б): agents receive ONLY the "Compact
excerpt" section, never this whole file. Full anchors below are for humans and
for resolving judge disputes.

## Compact excerpt (inject this into agent prompts as RUBRICS_EXCERPT)

```
Scoring: bands [min,max] 0-10, confidence high|medium|low, evidence "sha:file:reason" (2-5 per metric). LOW_SAMPLE -> no scores. Inapplicable -> "N/A". Wider band + lower confidence when samples are thin.
m6 rules-compliance: 0 core rules violated repeatedly | 5 mixed, isolated questionable patterns | 10 exemplary style, no rule debt.
m7 complexity (not volume): 0 mechanical churn | 5 ordinary feature work, few systems | 10 core architecture work, high blast radius.
m14 criticality of zones (higher=more critical, not better): 0 docs only | 5 gameplay/module-local runtime | 10 core platform/lifecycle/build foundations.
m17 architectural discipline: 0 bypasses architecture/duplicates infra | 5 mostly local, some boundary uncertainty | 10 strong fit, minimal coupling, clean extension path.
m18 completeness: 0 broken/abandoned | 5 usable slice with follow-up debt | 10 complete integrated vertical slice.
```

## Scoring Protocol

- Suppress all qualitative scores when `sample_gate == LOW_SAMPLE`.
- Return `"N/A"` for non-Unity or rubric-inapplicable windows instead of
  inventing a score.
- Use `AUTHOR` only; do not infer identity, seniority, salary, or HR outcome.
- Cite 2-5 evidence entries per scored metric. Evidence must be concrete:
  sha + file + short reason.
- Prefer a wider band and lower confidence when diff samples are thin, dominated
  by generated/bulk changes, or mostly assets/shaders.
- Trend calls are not made by analyzer. `mergeRegistry.js` applies the
  non-overlapping-band rule.

## Output Shape

```json
{
  "window_id": "",
  "sample_gate": "OK",
  "llm_scores": {
    "m6": { "band": [6, 7], "confidence": "medium", "evidence": ["sha:file:reason"], "notes": "" },
    "m7": { "band": [0, 0], "confidence": "low", "evidence": [], "notes": "" },
    "m14": {},
    "m17": {},
    "m18": {}
  },
  "hybrid_updates": {
    "m4_confirmed": null
  },
  "flags": []
}
```

## m6 — Rules Compliance

Score conformity with KBPro/Unity engineering rules: lifecycle hygiene, no
global finds, dependency usage, allocation discipline, TextMeshPro, async
handling, error handling.

| Anchor | Meaning |
|---|---|
| 0 | Diff repeatedly violates core rules or introduces obvious unsafe patterns. |
| 3 | Several rule breaches or ad-hoc patterns requiring follow-up cleanup. |
| 5 | Mixed: mostly acceptable, with isolated questionable patterns. |
| 7 | Consistently follows local patterns; minor style or edge-case issues only. |
| 10 | Exemplary local style, defensive handling, no visible rule debt. |

## m7 — Work Complexity Profile

Score observed technical complexity, not volume and not seniority.

| Anchor | Meaning |
|---|---|
| 0 | Mechanical text/config churn with no engineering decision visible. |
| 3 | Narrow content or simple component edits. |
| 5 | Ordinary feature work across a few files/systems. |
| 7 | Cross-system integration, nontrivial Unity lifecycle or data-flow decisions. |
| 10 | Core platform/architecture work with high blast radius and careful boundaries. |

## m14 — Criticality of Touched Zones

Score importance/risk of touched zones. Higher means more critical, not better.

| Anchor | Meaning |
|---|---|
| 0 | Isolated docs or non-runtime artifacts only. |
| 3 | Local content/prefab/config zones with limited blast radius. |
| 5 | Gameplay systems or module-local runtime behavior. |
| 7 | Shared module infrastructure, DI wiring, asset loading, UI/event boundaries. |
| 10 | Core KBPro/platform code, lifecycle/state machine, build/runtime foundations. |

## m17 — Architectural Discipline

Score reuse of platform architecture and preservation of module boundaries.

| Anchor | Meaning |
|---|---|
| 0 | Bypasses architecture, duplicates infrastructure, or couples unrelated modules. |
| 3 | Works but uses ad-hoc state/control paths where KBPro has established systems. |
| 5 | Mostly local and acceptable, with some boundary or reuse uncertainty. |
| 7 | Clear use of established services/events/presenters and clean boundaries. |
| 10 | Strong architecture fit, minimal coupling, obvious extension/cleanup path. |

## m18 — Completeness

Score whether work appears finished in the observed window.

| Anchor | Meaning |
|---|---|
| 0 | Broken or abandoned integration, TODO/commented code dominates. |
| 3 | Partial implementation with visible loose ends or orphan branches. |
| 5 | Usable slice with follow-up debt or incomplete validation hooks. |
| 7 | Coherent finished work; only ordinary polish remains. |
| 10 | Complete vertical slice, integrated and validated by available evidence. |

## Hybrid Notes

- `m4_confirmed`: confirm only finished modules. If signals are scaffolds,
  return `0` confirmed or leave `null` with explanation.
