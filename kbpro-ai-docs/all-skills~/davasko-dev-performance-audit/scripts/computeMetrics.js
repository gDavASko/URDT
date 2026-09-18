#!/usr/bin/env node
'use strict';
// computeMetrics.js — deterministic per-window metrics for the perf-audit skill.
// Reads dumpWindow.js output and writes window-<id>.json. No LLM work here.

const path = require('path');
const common = require('./lib/common.js');

const HELP = `computeMetrics.js — compute deterministic metrics for one audit window

Usage:
  node computeMetrics.js --dump registry/dump-<id>.json --out registry/window-<id>.json
  node computeMetrics.js --help

Computed here: m1, m2, m3, raw m4, m5, m11, m13, m15, self-revert part of m16.
reworkIndex.js owns m8/m9 and reverted-by-others m16 updates.`;

function median(values) {
  const a = values.filter(Number.isFinite).slice().sort((x, y) => x - y);
  if (!a.length) return 0;
  const mid = Math.floor(a.length / 2);
  return a.length % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

function round(n, digits = 3) {
  if (!Number.isFinite(n)) return 0;
  const k = 10 ** digits;
  return Math.round(n * k) / k;
}

function gini(values) {
  const a = values.filter(v => Number.isFinite(v) && v >= 0).slice().sort((x, y) => x - y);
  const n = a.length;
  if (!n) return 0;
  const sum = a.reduce((s, v) => s + v, 0);
  if (sum === 0) return 0;
  let weighted = 0;
  for (let i = 0; i < n; i++) weighted += (i + 1) * a[i];
  return round((2 * weighted) / (n * sum) - (n + 1) / n);
}

function pathOf(dump, file) {
  if (typeof file.p === 'number' && Array.isArray(dump.path_table)) {
    return dump.path_table[file.p] || '';
  }
  return file.path || '';
}

function dateOnly(iso) {
  return String(iso || '').slice(0, 10);
}

// Max gap in WORKING days between active days (plan v2 §3.2H): vacations and
// weekends must not read as "productivity collapse".
function maxGapWorkingDays(days, from, to) {
  const sorted = [...days].sort();
  const gapBetween = (a, b) => {
    // working days strictly between a and b
    const start = common.addDays(a, 1);
    const end = common.addDays(b, -1);
    return start > end ? 0 : common.workingDaysBetween(start, end);
  };
  if (!sorted.length) return common.workingDaysBetween(from, to);
  let max = gapBetween(common.addDays(from, -1), sorted[0]);
  for (let i = 1; i < sorted.length; i++) {
    max = Math.max(max, gapBetween(sorted[i - 1], sorted[i]));
  }
  max = Math.max(max, gapBetween(sorted[sorted.length - 1], common.addDays(to, 1)));
  return Math.max(0, max);
}

function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  if (!args.dump) { console.error('computeMetrics.js: missing --dump'); process.exit(1); }

  const dump = common.readJson(path.resolve(String(args.dump)));
  const outFile = args.out
    ? path.resolve(String(args.out))
    : path.join(path.dirname(path.resolve(String(args.dump))), `window-${dump.window_id}.json`);

  const commits = Array.isArray(dump.commits) ? dump.commits : [];
  const days = Number(dump.range && dump.range.days) || common.daysBetween(dump.range.from, dump.range.to);

  const uniquePathsByKind = { cs: new Set(), prefab: new Set(), asset: new Set(), shader: new Set(), code: new Set() };
  const systems = new Set();
  const kinds = new Set();
  const repos = new Set();
  const filesPerCommit = [];
  const locPerCommit = [];
  const activeDays = new Map();
  let effLoc = 0;
  let assetEffLoc = 0;
  let csCommitCount = 0;
  let selfReverts = 0;

  const repoByCommitDate = [];
  for (const c of commits) {
    repos.add(c.repo);
    repoByCommitDate.push({ date: c.date || '', repo: c.repo || '' });
    const files = Array.isArray(c.files) ? c.files : [];
    let commitLoc = 0;
    let hasCs = false;
    for (const f of files) {
      const p = pathOf(dump, f);
      const kind = f.kind || common.classifyFile(p);
      if (uniquePathsByKind[kind]) uniquePathsByKind[kind].add(p);
      kinds.add(kind);
      if (f.system) systems.add(f.system);
      commitLoc += (Number(f.loc_add) || 0) + (Number(f.loc_del) || 0);
      effLoc += Number(f.eff_loc) || 0;
      if (kind === 'asset' || kind === 'prefab') assetEffLoc += Number(f.eff_loc) || 0;
      if (kind === 'cs') hasCs = true;
    }
    if (hasCs) csCommitCount++;
    filesPerCommit.push(files.length);
    locPerCommit.push(commitLoc);
    if (c.is_revert || String(c.cc_type).toLowerCase() === 'revert') selfReverts++;
    const d = dateOnly(c.date);
    if (d) activeDays.set(d, (activeDays.get(d) || 0) + 1);
  }

  // Files excluded from analysis (meta, misc) are reported SEPARATELY and never
  // inflate volume metrics (v1 defect E: synthetic __other_N paths in m1/m5).
  const otherFilesExcluded = Number(dump.excluded && dump.excluded.other_files) || 0;

  // Context switches (v1 defect G): a superproject + submodule commit pair made
  // within the same hour is one work context, not a switch.
  repoByCommitDate.sort((a, b) => a.date.localeCompare(b.date));
  const SWITCH_MIN_MS = 60 * 60 * 1000;
  let contextSwitches = 0;
  for (let i = 1; i < repoByCommitDate.length; i++) {
    const prev = repoByCommitDate[i - 1];
    const cur = repoByCommitDate[i];
    if (cur.repo === prev.repo) continue;
    const dt = Date.parse(cur.date) - Date.parse(prev.date);
    if (Number.isFinite(dt) && Math.abs(dt) < SWITCH_MIN_MS) continue;
    contextSwitches++;
  }

  const m1 = {};
  for (const [k, set] of Object.entries(uniquePathsByKind)) m1[k] = set.size;
  const totalFiles = Object.values(m1).reduce((s, v) => s + v, 0);

  // Adaptive sample gate (plan v2 §3.2K): content-profile windows qualify via
  // effective asset LOC even with few cs commits.
  const sampleGate = (csCommitCount >= common.LOW_SAMPLE_MIN_CS_COMMITS
    || assetEffLoc >= common.LOW_SAMPLE_MIN_ASSET_EFF_LOC) ? 'OK' : 'LOW_SAMPLE';

  const rawModules = Array.isArray(dump.new_modules_signals)
    ? new Set(dump.new_modules_signals.map(s => `${s.dir || ''}:${s.class_name || ''}:${s.reason || ''}`)).size
    : 0;

  // Rhythm over WORKING days only (v1 defect: weekends inflated Gini for
  // everyone with a normal schedule).
  const perWorkingDayCounts = [];
  let workingDaysInRange = 0;
  let activeWorkingDays = 0;
  for (let cursor = dump.range.from; cursor <= dump.range.to; cursor = common.addDays(cursor, 1)) {
    if (!common.isWorkingDay(cursor)) continue;
    workingDaysInRange++;
    const n = activeDays.get(cursor) || 0;
    if (n > 0) activeWorkingDays++;
    perWorkingDayCounts.push(n);
  }

  const windowPayload = {
    schema_version: common.SCHEMA_VERSION,
    rubrics_version: common.RUBRICS_VERSION,
    window_id: dump.window_id,
    phase: dump.phase,
    range: dump.range,
    sample_gate: sampleGate,
    metrics: {
      m1,
      m2: commits.length,
      m3: systems.size,
      m4: { count: rawModules, confirmed: null, flag: sampleGate === 'LOW_SAMPLE' ? 'RAW_ONLY' : null },
      m5: {
        commits_pm: round(common.perMonth(commits.length, days)),
        files_pm: round(common.perMonth(totalFiles, days)),
        eff_loc_pm: round(common.perMonth(effLoc, days)),
      },
      m8: { candidates: 0 },
      m9: { candidates: 0, by_others: 0, by_self: 0 },
      m11: {
        median_files: round(median(filesPerCommit)),
        median_loc: round(median(locPerCommit)),
        monster_share: round(filesPerCommit.length ? filesPerCommit.filter(v => v > 30).length / filesPerCommit.length : 0),
      },
      m13: {
        projects: repos.size,
        systems: systems.size,
        kinds: kinds.size,
        context_switches: contextSwitches,
      },
      m15: {
        active_days: activeDays.size,
        active_working_days: activeWorkingDays,
        working_days: workingDaysInRange,
        active_share: round(workingDaysInRange ? activeWorkingDays / workingDaysInRange : 0),
        max_gap_working_days: maxGapWorkingDays(activeDays.keys(), dump.range.from, dump.range.to),
        gini: gini(perWorkingDayCounts),
      },
      m16: { reverted_by_others: 0, self_reverts: selfReverts },
      other_files_excluded: otherFilesExcluded,
    },
    llm_scores: sampleGate === 'OK' ? {} : undefined,
    m19: 'N/A',
    rework_links: [],
    source_dump: path.basename(String(args.dump)),
    notes: Array.isArray(dump.notes) ? dump.notes : [],
  };

  common.writeJson(outFile, windowPayload);
  console.error(`wrote ${outFile} (commits=${commits.length}, sample_gate=${sampleGate})`);
}

if (require.main === module) {
  try { main(); } catch (e) { console.error(`computeMetrics.js: ${e.stack || e.message}`); process.exit(1); }
}
