#!/usr/bin/env node
'use strict';
// mergeRegistry.js v2 — deterministic registry aggregation and metrics-summary
// export. LLM never edits metrics-summary.json.
//
// What changed vs v1:
//   - All phase count metrics are normalized PER MONTH (phase days / 30.4375),
//     never "per window" (v1 averaged over windows of unequal length).
//   - Composite axes (productivity=commits+files, breadth=systems+projects,
//     growth=sum|deltas|) are REMOVED — they mixed units and made any change
//     read as "growth". Metrics are reported per-metric, grouped, with
//     guardrail pairs (speed <-> quality) declared explicitly.
//   - ITS-lite (plan v2 §3.3): per-phase least-squares slope over the window
//     series for key metrics; m20 carries per-metric per-month deltas + slopes.
//     Verdicts belong to the analyst, not this script.
//   - Absence handling (plan v2 §3.6): windows overlapping user-declared
//     absences (run-config.json: absences[]) are flagged EXCLUDED_ABSENCE and
//     dropped from aggregates and trends.
//   - m8/m9 ratios aggregate as sum(lines)/sum(added), not mean of ratios.
//
// Qualitative band aggregation + the non-overlap trend rule are kept from v1
// (they were correct).

const fs = require('fs');
const path = require('path');
const common = require('./lib/common.js');

const HELP = `mergeRegistry.js — merge perf-audit window registry into metrics-summary.json

Usage:
  node mergeRegistry.js --run <run dir> [--validated]
  node mergeRegistry.js --registry <registry-dir> --out <summary.json> [--config run-config.json] [--validated]
  node mergeRegistry.js --help`;

const QUAL_METRICS = ['m6', 'm7', 'm14', 'm17', 'm18'];
const CONF_RANK = { low: 1, medium: 2, high: 3 };

// Guardrail pairs (plan v2 §3.4): a speed metric may only be shown next to its
// quality counterpart. Consumed by the report/dashboard generators.
const GUARDRAIL_PAIRS = [
  { speed: 'm5.commits_pm', quality: 'm9.ratio', note: 'volume vs fixed-by-others' },
  { speed: 'm5.eff_loc_pm', quality: 'm8.self_fix_ratio', note: 'volume vs self-fix rework' },
  { speed: 'm2_pm', quality: 'm16_pm.reverted_by_others', note: 'commit rate vs reverts' },
  { speed: 'mr.merged_pm', quality: 'mr.review_coverage', note: 'merge rate vs approval coverage' },
  { speed: 'mr.cycle_time_days_median', quality: 'm9.ratio', note: 'cycle time vs fixed-by-others' },
];

function round(n, d = 3) {
  if (!Number.isFinite(n)) return 0;
  const k = 10 ** d;
  return Math.round(n * k) / k;
}

function avg(nums) {
  const a = nums.filter(Number.isFinite);
  return a.length ? round(a.reduce((s, v) => s + v, 0) / a.length) : 0;
}

function sum(nums) {
  return nums.filter(Number.isFinite).reduce((s, v) => s + v, 0);
}

function confidenceMin(items) {
  const vals = items.map(x => String(x.confidence || 'low')).filter(Boolean);
  if (!vals.length) return '';
  return vals.sort((a, b) => (CONF_RANK[a] || 0) - (CONF_RANK[b] || 0))[0];
}

function bandAggregate(items) {
  const bands = items.filter(x => x && Array.isArray(x.band) && x.band.length === 2);
  if (!bands.length) return 'N/A';
  return {
    band: [round(avg(bands.map(x => Number(x.band[0]))), 1), round(avg(bands.map(x => Number(x.band[1]))), 1)],
    confidence: confidenceMin(bands),
    composition: bands.length === items.length ? 'all_components' : `partial:${bands.length}/${items.length}`,
  };
}

// Least-squares slope of value over window index (unitless direction signal).
function slope(values) {
  const ys = values.filter(Number.isFinite);
  const n = ys.length;
  if (n < 2) return 0;
  const xMean = (n - 1) / 2;
  const yMean = ys.reduce((s, v) => s + v, 0) / n;
  let num = 0, den = 0;
  for (let i = 0; i < n; i++) {
    num += (i - xMean) * (ys[i] - yMean);
    den += (i - xMean) * (i - xMean);
  }
  return den ? round(num / den) : 0;
}

function addCounts(target, source) {
  for (const [k, v] of Object.entries(source || {})) {
    if (typeof v === 'number') target[k] = (target[k] || 0) + v;
  }
  return target;
}

function get(obj, dottedPath) {
  let cur = obj;
  for (const part of dottedPath.split('.')) {
    if (cur == null) return undefined;
    cur = cur[part];
  }
  return cur;
}

// ---------------------------------------------------------------------------
// Absences (plan v2 §3.6)
// ---------------------------------------------------------------------------

function overlapDays(aFrom, aTo, bFrom, bTo) {
  const from = aFrom > bFrom ? aFrom : bFrom;
  const to = aTo < bTo ? aTo : bTo;
  return from > to ? 0 : common.daysBetween(from, to);
}

// Absence handling (plan v2 §3.6, refined 2026-07-16): instead of dropping a
// window over an arbitrary day threshold (which killed whole phases when a
// vacation fell into a merged 1.5-month window), absence days are SUBTRACTED
// from the normalization denominator — volumes are honestly "per available
// month". A window is fully excluded only when absences cover >= 60% of it.
function absenceOverlap(win, absences) {
  const wFrom = String(win.range.from), wTo = String(win.range.to);
  const wDays = common.daysBetween(wFrom, wTo);
  let overlap = 0;
  for (const a of absences) {
    overlap += overlapDays(wFrom, wTo, String(a.from), String(a.to));
  }
  overlap = Math.min(overlap, wDays);
  return { overlap, wDays, excluded: wDays > 0 && overlap / wDays >= 0.6 };
}

// ---------------------------------------------------------------------------
// Phase aggregation
// ---------------------------------------------------------------------------

function windowEffLoc(w) {
  // Recover the window's absolute eff LOC from its per-month value.
  const pm = Number(w.metrics?.m5?.eff_loc_pm) || 0;
  const days = Number(w.range?.days) || common.daysBetween(w.range.from, w.range.to);
  return pm * days / common.DAYS_PER_MONTH;
}

function windowTotalFiles(w) {
  return sum(Object.values(w.metrics?.m1 || {}).filter(v => typeof v === 'number'));
}

function inferProfile(windows) {
  const totals = { cs: 0, prefab: 0, asset: 0, shader: 0, code: 0 };
  for (const w of windows) addCounts(totals, w.metrics && w.metrics.m1);
  const content = totals.prefab + totals.asset;
  const code = totals.cs + totals.shader + totals.code;
  if (code === 0 && content === 0) return 'unknown';
  if (code >= content * 2) return 'system';
  if (content >= code * 2) return 'content';
  return 'mixed';
}

function aggregatePhase(ws, absenceDaysByWindow) {
  const phaseDays = sum(ws.map(w => {
    const raw = Number(w.range?.days) || common.daysBetween(w.range.from, w.range.to);
    return raw - ((absenceDaysByWindow && absenceDaysByWindow.get(w.window_id)) || 0);
  }));
  const months = phaseDays / common.DAYS_PER_MONTH;
  const pm = v => months ? round(v / months) : 0;

  const m1Totals = {};
  for (const w of ws) addCounts(m1Totals, w.metrics?.m1);
  const m1_pm = {};
  for (const [k, v] of Object.entries(m1Totals)) m1_pm[k] = pm(v);

  const totalCommits = sum(ws.map(w => Number(w.metrics?.m2)));
  const totalFiles = sum(ws.map(windowTotalFiles));
  const totalEffLoc = sum(ws.map(windowEffLoc));

  const m8Added = sum(ws.map(w => Number(w.metrics?.m8?.added_cs_lines)));
  const m8Churn = sum(ws.map(w => Number(w.metrics?.m8?.churn_lines)));
  const m8SelfFix = sum(ws.map(w => Number(w.metrics?.m8?.self_fix_lines)));
  const m9Fixed = sum(ws.map(w => Number(w.metrics?.m9?.fixed_lines_by_others)));
  const m9FixCommits = sum(ws.map(w => Number(w.metrics?.m9?.fix_commits_by_others)));

  return {
    months: round(months, 2),
    m1_pm,
    other_files_excluded_pm: pm(sum(ws.map(w => Number(w.metrics?.other_files_excluded)))),
    m2_pm: pm(totalCommits),
    m3_avg_per_window: avg(ws.map(w => Number(w.metrics?.m3))),
    m4: {
      count_pm: pm(sum(ws.map(w => Number(w.metrics?.m4?.count)))),
      confirmed: null,
      flag: ws.some(w => w.sample_gate === 'LOW_SAMPLE') ? 'RAW_ONLY_PARTIAL' : null,
    },
    m5: {
      commits_pm: pm(totalCommits),
      files_pm: pm(totalFiles),
      eff_loc_pm: pm(totalEffLoc),
    },
    m8: {
      added_cs_lines_pm: pm(m8Added),
      churn_ratio: m8Added ? round(m8Churn / m8Added) : 0,
      self_fix_ratio: m8Added ? round(m8SelfFix / m8Added) : 0,
    },
    m9: {
      ratio: m8Added ? round(m9Fixed / m8Added) : 0,
      fix_commits_by_others_pm: pm(m9FixCommits),
    },
    m11: {
      median_files: avg(ws.map(w => Number(w.metrics?.m11?.median_files))),
      median_loc: avg(ws.map(w => Number(w.metrics?.m11?.median_loc))),
      monster_share: avg(ws.map(w => Number(w.metrics?.m11?.monster_share))),
    },
    m13: {
      projects: avg(ws.map(w => Number(w.metrics?.m13?.projects))),
      systems: avg(ws.map(w => Number(w.metrics?.m13?.systems))),
      kinds: avg(ws.map(w => Number(w.metrics?.m13?.kinds))),
      context_switches_pm: pm(sum(ws.map(w => Number(w.metrics?.m13?.context_switches)))),
    },
    m15: {
      active_share: avg(ws.map(w => Number(w.metrics?.m15?.active_share))),
      max_gap_working_days: Math.max(0, ...ws.map(w => Number(w.metrics?.m15?.max_gap_working_days) || 0)),
      gini: avg(ws.map(w => Number(w.metrics?.m15?.gini))),
    },
    m16_pm: {
      reverted_by_others: pm(sum(ws.map(w => Number(w.metrics?.m16?.reverted_by_others)))),
      self_reverts: pm(sum(ws.map(w => Number(w.metrics?.m16?.self_reverts)))),
    },
  };
}

// Window-level series used for slopes and the dashboard trend charts.
const SERIES_KEYS = [
  ['m5.commits_pm', w => Number(w.metrics?.m5?.commits_pm)],
  ['m5.files_pm', w => Number(w.metrics?.m5?.files_pm)],
  ['m5.eff_loc_pm', w => Number(w.metrics?.m5?.eff_loc_pm)],
  ['m8.self_fix_ratio', w => Number(w.metrics?.m8?.self_fix_ratio)],
  ['m8.churn_ratio', w => Number(w.metrics?.m8?.churn_ratio)],
  ['m9.ratio', w => Number(w.metrics?.m9?.ratio)],
  ['m11.median_files', w => Number(w.metrics?.m11?.median_files)],
  ['m11.monster_share', w => Number(w.metrics?.m11?.monster_share)],
  ['m15.active_share', w => Number(w.metrics?.m15?.active_share)],
  ['m13.context_switches', w => Number(w.metrics?.m13?.context_switches)],
];

// Phase-comparison metric paths for m20 deltas (all per-month or ratio —
// duration-fair by construction).
const DELTA_PATHS = [
  'm2_pm', 'm5.commits_pm', 'm5.files_pm', 'm5.eff_loc_pm',
  'm8.self_fix_ratio', 'm8.churn_ratio', 'm9.ratio',
  'm11.median_files', 'm11.monster_share',
  'm13.context_switches_pm', 'm15.active_share',
  'm16_pm.reverted_by_others',
  'mr.merged_pm', 'mr.cycle_time_days_median',
  'mr.review_coverage', 'mr.no_mr_share',
];

function median(values) {
  const a = values.filter(Number.isFinite).slice().sort((x, y) => x - y);
  if (!a.length) return 0;
  const mid = Math.floor(a.length / 2);
  return a.length % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

// MR metrics per phase. MR comments/notes are ignored; coverage is approval-only.
// fetchMrData.js; `phaseCommitShas` are subject commit shas from the dumps.
function mrMetricsForPhase(mrs, phase, months, phaseCommitShas) {
  const inPhase = iso => iso && isoDay(iso) >= phase.from && isoDay(iso) <= phase.to;
  const merged = mrs.filter(mr => mr.state === 'merged' && inPhase(mr.merged_at));
  const hours = (a, b) => (Date.parse(b) - Date.parse(a)) / 3600000;

  const cycleDays = merged.map(mr => hours(mr.created_at, mr.merged_at) / 24);
  const reviewed = merged.filter(mr => (mr.approved_by || []).length > 0);

  // e4: subject commits in this phase that no MR accounts for (direct pushes
  // or not-yet-merged branches — flagged as one bucket, documented).
  const mrShaSet = new Set();
  for (const mr of mrs) {
    for (const s of mr.commit_shas || []) mrShaSet.add(s);
    if (mr.merge_commit_sha) mrShaSet.add(mr.merge_commit_sha);
    if (mr.squash_commit_sha) mrShaSet.add(mr.squash_commit_sha);
  }
  const total = phaseCommitShas.length;
  const noMr = phaseCommitShas.filter(sha => !mrShaSet.has(sha)).length;

  return {
    merged_count: merged.length,
    merged_pm: months ? round(merged.length / months) : 0,
    cycle_time_days_median: round(median(cycleDays), 1),
    review_coverage: merged.length ? round(reviewed.length / merged.length) : 0,
    no_mr_share: total ? round(noMr / total) : 0,
    no_mr_commits: noMr,
  };
}

function isoDay(iso) { return String(iso).slice(0, 10); }

function validateLlmEvidence(window) {
  const bad = [];
  if (!window.llm_scores) return bad;
  for (const m of QUAL_METRICS) {
    const score = window.llm_scores[m];
    if (!score) continue;
    if (!Array.isArray(score.evidence) || score.evidence.length === 0) bad.push(`${window.window_id}:${m}:missing_evidence`);
  }
  return bad;
}

function loadWindows(registryDir) {
  return fs.readdirSync(registryDir)
    .filter(f => /^window-.*\.json$/.test(f))
    .map(f => common.readJson(path.join(registryDir, f)))
    .sort((a, b) => String(a.range?.from).localeCompare(String(b.range?.from)));
}

function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }

  const runDir = args.run ? path.resolve(String(args.run)) : null;
  const registryDir = args.registry ? path.resolve(String(args.registry)) : path.join(runDir, 'registry');
  const outFile = args.out ? path.resolve(String(args.out)) : path.join(runDir, 'metrics-summary.json');
  const configFile = args.config ? path.resolve(String(args.config)) : (runDir ? path.join(runDir, 'run-config.json') : null);
  const config = configFile && fs.existsSync(configFile) ? common.readJson(configFile) : null;

  if (!registryDir || !fs.existsSync(registryDir)) {
    console.error(`mergeRegistry.js: registry not found: ${registryDir}`);
    process.exit(1);
  }
  const allWindows = loadWindows(registryDir);
  if (!allWindows.length) {
    console.error(`mergeRegistry.js: no window-*.json in ${registryDir}`);
    process.exit(1);
  }

  const flags = new Set();
  const evidenceErrors = [];
  const absences = Array.isArray(config?.absences) ? config.absences : [];
  const excludedIds = new Set();
  const absenceDaysByWindow = new Map();
  for (const w of allWindows) {
    if (w.sample_gate === 'LOW_SAMPLE') flags.add(`LOW_SAMPLE:${w.window_id}`);
    if (absences.length) {
      const { overlap, excluded } = absenceOverlap(w, absences);
      if (excluded) {
        excludedIds.add(w.window_id);
        flags.add(`EXCLUDED_ABSENCE:${w.window_id}`);
      } else if (overlap > 0) {
        absenceDaysByWindow.set(w.window_id, overlap);
        flags.add(`ABSENCE_ADJUSTED:${w.window_id}:${overlap}d`);
      }
    }
    evidenceErrors.push(...validateLlmEvidence(w));
  }
  const windows = allWindows.filter(w => !excludedIds.has(w.window_id));
  if (!windows.length) {
    console.error('mergeRegistry.js: all windows excluded by absences — nothing to aggregate');
    process.exit(1);
  }
  if (evidenceErrors.length) flags.add(`LLM_EVIDENCE_MISSING:${evidenceErrors.length}`);
  if (!args.validated) flags.add('UNVALIDATED');
  if (!config || !config.innovation || !config.innovation.per_dev_adoption_dates) {
    flags.add('ADOPTION_BY_DATE_ONLY');
  }

  const reworkFile = path.join(registryDir, 'rework-index.json');
  if (fs.existsSync(reworkFile)) {
    const rework = common.readJson(reworkFile);
    for (const f of rework.flags || []) flags.add(f);
  } else {
    flags.add('REWORK_INDEX_MISSING');
  }

  const byPhase = new Map();
  for (const w of windows) {
    if (!byPhase.has(w.phase)) byPhase.set(w.phase, []);
    byPhase.get(w.phase).push(w);
  }

  // MR data (phase 2 layer) + subject commit shas per phase (for e4).
  const mrFile = args['mr-data']
    ? path.resolve(String(args['mr-data']))
    : (runDir ? path.join(runDir, 'mr-data.json') : path.join(registryDir, 'mr-data.json'));
  const mrData = fs.existsSync(mrFile) ? common.readJson(mrFile) : null;
  if (!mrData) flags.add('MR_DATA_MISSING');
  for (const f of (mrData?.flags || [])) flags.add(f);
  const shasByPhase = new Map();
  for (const f of fs.readdirSync(registryDir).filter(x => /^dump-.*\.json$/.test(x))) {
    const d = common.readJson(path.join(registryDir, f));
    if (excludedIds.has(d.window_id)) continue;
    if (!shasByPhase.has(d.phase)) shasByPhase.set(d.phase, []);
    const arr = shasByPhase.get(d.phase);
    for (const c of d.commits || []) arr.push(c.sha);
  }

  const phases = [];
  for (const [phaseId, ws] of byPhase.entries()) {
    const p = config && Array.isArray(config.phases) ? config.phases.find(x => x.id === phaseId) : null;
    const metrics = aggregatePhase(ws, absenceDaysByWindow);
    for (const id of QUAL_METRICS) {
      metrics[id] = bandAggregate(ws.map(w => w.llm_scores && w.llm_scores[id]).filter(Boolean));
    }
    const phaseRange = {
      from: p ? p.from : ws[0].range.from,
      to: p ? p.to : ws[ws.length - 1].range.to,
    };
    metrics.mr = mrData
      ? mrMetricsForPhase(mrData.mrs || [], phaseRange, metrics.months, shasByPhase.get(phaseId) || [])
      : 'N/A';

    const slopes = {};
    for (const [key, fn] of SERIES_KEYS) slopes[key] = slope(ws.map(fn));

    phases.push({
      id: phaseId,
      name: p ? (p.name || p.id) : phaseId,
      from: p ? p.from : ws[0].range.from,
      to: p ? p.to : ws[ws.length - 1].range.to,
      profile: inferProfile(ws),
      metrics,
      slopes,
      confidence: Object.fromEntries(QUAL_METRICS.map(id => [id, confidenceMin(ws.map(w => w.llm_scores?.[id]).filter(Boolean)) || ''])),
      windows: ws.map(w => w.window_id),
      excluded_windows: allWindows.filter(w => excludedIds.has(w.window_id) && w.phase === phaseId).map(w => w.window_id),
    });
  }

  // m20 (ITS-lite): per-metric per-month deltas + slopes; qualitative trends by
  // the non-overlap band rule. NO composite "growth" number (v1 defect D).
  for (let i = 1; i < phases.length; i++) {
    const prev = phases[i - 1];
    const cur = phases[i];
    const deltas = {};
    for (const p of DELTA_PATHS) {
      const a = Number(get(prev.metrics, p));
      const b = Number(get(cur.metrics, p));
      if (Number.isFinite(a) && Number.isFinite(b)) deltas[p] = round(b - a);
    }
    const qualitative = {};
    for (const id of QUAL_METRICS) {
      const a = prev.metrics[id], b = cur.metrics[id];
      if (!a || !b || !a.band || !b.band) qualitative[id] = 'no_data';
      else if (a.band[1] < b.band[0]) qualitative[id] = 'up';
      else if (b.band[1] < a.band[0]) qualitative[id] = 'down';
      else qualitative[id] = 'no_significant_change';
    }
    cur.metrics.m20 = {
      compared_to: prev.id,
      deltas_pm: deltas,
      slopes_prev: prev.slopes,
      slopes_cur: cur.slopes,
      qualitative_trends: qualitative,
    };
  }

  // Per-window series for the dashboard (chronological, includes excluded
  // windows marked so charts can grey them out).
  const window_series = allWindows.map(w => ({
    window_id: w.window_id,
    phase: w.phase,
    from: w.range.from,
    to: w.range.to,
    excluded_absence: excludedIds.has(w.window_id),
    sample_gate: w.sample_gate,
    values: Object.fromEntries(SERIES_KEYS.map(([key, fn]) => [key, Number(fn(w)) || 0])),
    llm: Object.fromEntries(QUAL_METRICS.map(id => [id, w.llm_scores?.[id] || null])),
  }));

  const perRepoCounts = new Map();
  for (const f of fs.readdirSync(registryDir).filter(x => /^dump-.*\.json$/.test(x))) {
    const d = common.readJson(path.join(registryDir, f));
    for (const c of d.commits || []) perRepoCounts.set(c.repo, (perRepoCounts.get(c.repo) || 0) + 1);
  }
  const totalRepoCommits = [...perRepoCounts.values()].reduce((s, v) => s + v, 0) || 1;
  const per_repo = [...perRepoCounts.entries()]
    .sort((a, b) => b[1] - a[1])
    .map(([repo, commits]) => ({ repo, commits, share: round(commits / totalRepoCommits, 4) }));

  const from = config?.phases?.length ? config.phases.map(p => p.from).sort()[0] : windows[0].range.from;
  const to = config?.phases?.length ? config.phases.map(p => p.to).sort().slice(-1)[0] : windows[windows.length - 1].range.to;
  const summary = {
    schema_version: common.SCHEMA_VERSION,
    rubrics_version: common.RUBRICS_VERSION,
    author: config?.author || { display: 'AUTHOR', ids: [] },
    innovation: config?.innovation || null,
    generated_at: new Date().toISOString(),
    analyzed_range: { from, to },
    guardrail_pairs: GUARDRAIL_PAIRS,
    phases,
    window_series,
    per_repo,
    flags: [...flags].sort(),
  };

  common.writeJsonPretty(outFile, summary);
  console.error(`wrote ${outFile} (phases=${phases.length}, windows=${windows.length}/${allWindows.length}, flags=${summary.flags.length})`);
}

if (require.main === module) {
  try { main(); } catch (e) { console.error(`mergeRegistry.js: ${e.stack || e.message}`); process.exit(1); }
}
