#!/usr/bin/env node
'use strict';
// consolidate.js v2 — merge per-employee metrics-summary.json files into the
// people dashboard + consolidated JSON. Mechanical only; narrative verdicts
// belong to the final analyst agent.
//
// v2 changes:
//   - INCREMENTAL by design (plan v2 §4в): works from ONE summary upward, so
//     the people dashboard is rebuilt after every finished employee and the
//     user always has a valid consolidated view of whoever is done.
//   - v1 "criteria" axes are gone; the dashboard shows per-metric deltas with
//     guardrail pairs (schema 2.0 only — older summaries are refused).
//   - Attention detection for 1:1 (plan v2 §1): an employee's phase-over-phase
//     delta that deviates from the group median by > 1.5 * MAD is flagged as a
//     conversation topic. Descriptive, never a ranking.
//   - Optional --verdicts <file>: analyst-produced verdicts JSON merged into
//     the dashboard ({ "<display>": { verdict, reason } }).
//
//   - Optional --bitrix <dir-or-files>: bitrix-summary.json files from the
//     davasko-bitrix-team-stats skill are matched to employees by slug/display
//     and embedded, so the dashboard renders git and task metrics in ONE table.
//
// Usage:
//   node consolidate.js --inputs <summary1.json,...|reports-root> --out-dir <dir>
//                       [--verdicts <file>] [--innovation "<name>"]
//                       [--bitrix <bitrix-summaries dir or comma files>]

const fs = require('fs');
const path = require('path');
const common = require('./lib/common.js');

const HELP = `consolidate.js — build the consolidated people view (schema 2.0)

Usage:
  node consolidate.js --inputs <summary1.json,summary2.json|reports-root> --out-dir <dir>
                      [--verdicts verdicts.json] [--innovation "<name>"]

Accepts 1+ summaries (incremental per-employee pipeline rebuilds this after
every finished employee). Refuses schema_version != ${common.SCHEMA_VERSION}.`;

// Metrics whose deltas participate in outlier detection.
const ATTENTION_KEYS = [
  'mr.cycle_time_days_median', 'm5.commits_pm', 'm9.ratio',
  'm8.self_fix_ratio', 'mr.review_coverage', 'm15.active_share',
];

function die(msg, code = 1) { console.error(msg); process.exit(code); }

function latestRunSummary(authorDir) {
  const runs = fs.readdirSync(authorDir, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .map(d => d.name)
    .sort()
    .reverse();
  for (const r of runs) {
    const f = path.join(authorDir, r, 'metrics-summary.json');
    if (fs.existsSync(f)) return f;
  }
  return null;
}

function resolveInputs(inputArg) {
  const parts = String(inputArg).split(',').map(s => s.trim()).filter(Boolean);
  const files = [];
  for (const p of parts) {
    const full = path.resolve(p);
    if (fs.existsSync(full) && fs.statSync(full).isFile()) {
      files.push(full);
    } else if (fs.existsSync(full) && fs.statSync(full).isDirectory()) {
      const direct = path.join(full, 'metrics-summary.json');
      if (fs.existsSync(direct)) { files.push(direct); continue; }
      for (const d of fs.readdirSync(full, { withFileTypes: true }).filter(x => x.isDirectory())) {
        if (d.name === '_consolidated') continue;
        const s = latestRunSummary(path.join(full, d.name));
        if (s) files.push(s);
      }
    } else {
      throw new Error(`input not found: ${p}`);
    }
  }
  return [...new Set(files)];
}

function get(obj, dotted) {
  let cur = obj;
  for (const part of dotted.split('.')) {
    if (cur == null) return undefined;
    cur = cur[part];
  }
  return typeof cur === 'number' ? cur : undefined;
}

function lastDelta(summary, key) {
  const phases = summary.phases || [];
  const last = phases[phases.length - 1];
  return last && last.metrics.m20 && last.metrics.m20.deltas_pm
    ? last.metrics.m20.deltas_pm[key]
    : undefined;
}

function medianOf(nums) {
  const a = nums.filter(Number.isFinite).slice().sort((x, y) => x - y);
  if (!a.length) return undefined;
  const mid = Math.floor(a.length / 2);
  return a.length % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

// Outliers per metric: |delta - group median| > 1.5 * MAD (needs 3+ employees;
// with fewer there is no meaningful "group dynamic" to deviate from).
function detectAttention(summaries) {
  const attention = {};
  if (summaries.length < 3) return attention;
  for (const key of ATTENTION_KEYS) {
    const deltas = summaries.map(s => ({ name: s.author?.display || 'AUTHOR', d: lastDelta(s, key) }));
    const values = deltas.map(x => x.d).filter(Number.isFinite);
    if (values.length < 3) continue;
    const med = medianOf(values);
    const mad = medianOf(values.map(v => Math.abs(v - med)));
    if (!Number.isFinite(mad) || mad === 0) continue;
    for (const { name, d } of deltas) {
      if (!Number.isFinite(d)) continue;
      if (Math.abs(d - med) > 1.5 * mad) {
        (attention[name] = attention[name] || []).push({
          metric: key,
          delta: Math.round(d * 1000) / 1000,
          groupMedian: Math.round(med * 1000) / 1000,
        });
      }
    }
  }
  return attention;
}

// Resolve bitrix-summary.json inputs: comma files, or a dir scanned for
// <slug>/bitrix-summary.json and bitrix-summary*.json.
function resolveBitrixInputs(arg) {
  const files = [];
  for (const p of String(arg).split(',').map(s => s.trim()).filter(Boolean)) {
    const full = path.resolve(p);
    if (fs.existsSync(full) && fs.statSync(full).isFile()) { files.push(full); continue; }
    if (fs.existsSync(full) && fs.statSync(full).isDirectory()) {
      for (const f of fs.readdirSync(full)) {
        if (/^bitrix-summary.*\.json$/.test(f)) files.push(path.join(full, f));
      }
      for (const d of fs.readdirSync(full, { withFileTypes: true }).filter(x => x.isDirectory())) {
        const cand = path.join(full, d.name, 'bitrix-summary.json');
        if (fs.existsSync(cand)) files.push(cand);
      }
    }
  }
  return [...new Set(files)];
}

function normName(s) {
  return String(s || '').toLowerCase().replace(/\s+/g, ' ').trim();
}

// Attach bitrix summaries to git summaries by slug or display name.
function attachBitrix(employees, bitrixSummaries, warnings) {
  const used = new Set();
  for (const emp of employees) {
    const gitNames = new Set([normName(emp.author?.display), normName(emp.author?.slug),
      ...((emp.author?.ids || []).map(normName))].filter(Boolean));
    const match = bitrixSummaries.find(b => !used.has(b)
      && (gitNames.has(normName(b.author?.display)) || gitNames.has(normName(b.author?.slug))));
    if (match) {
      emp.bitrix = match;
      used.add(match);
    }
  }
  for (const b of bitrixSummaries) {
    if (!used.has(b)) warnings.push(`Bitrix summary for "${b.author?.display}" matched no git employee (check display/slug spelling).`);
  }
}

function buildDashboard(outFile, data) {
  const templateFile = path.join(common.SKILL_ROOT, 'references', 'dashboard-template.html');
  const template = fs.readFileSync(templateFile, 'utf8');
  const marker = '/*__DATA_JSON__*/null';
  if (!template.includes(marker)) throw new Error(`placeholder ${marker} not found in dashboard template`);
  fs.writeFileSync(outFile, template.replace(marker, JSON.stringify(data)), 'utf8');
}

function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  if (!args.inputs) die('consolidate.js: missing --inputs');
  if (!args['out-dir']) die('consolidate.js: missing --out-dir');
  const outDir = path.resolve(String(args['out-dir']));
  common.assertNotDriveC(outDir, 'consolidate out-dir');
  common.ensureDir(outDir);

  const files = resolveInputs(args.inputs);
  if (!files.length) die('consolidate.js: no metrics-summary.json found in inputs');

  const summaries = files.map(f => ({ file: f, data: common.readJson(f) }));
  const bad = summaries.filter(s => s.data.schema_version !== common.SCHEMA_VERSION);
  if (bad.length) {
    console.error(`consolidate.js: incompatible schema_version (need ${common.SCHEMA_VERSION}):`);
    for (const s of bad) console.error(`  ${s.file}: schema=${s.data.schema_version}`);
    console.error('Re-run the audit for these employees with the v2 skill (old v1 summaries cannot be mixed in).');
    process.exit(2);
  }

  const verdicts = args.verdicts && fs.existsSync(path.resolve(String(args.verdicts)))
    ? common.readJson(path.resolve(String(args.verdicts)))
    : {};

  const employees = summaries.map(s => s.data);
  const attention = detectAttention(employees);
  const warnings = [];

  if (args.bitrix) {
    const bitrixFiles = resolveBitrixInputs(args.bitrix);
    if (!bitrixFiles.length) warnings.push('--bitrix given but no bitrix-summary*.json found.');
    const bitrixSummaries = bitrixFiles.map(f => common.readJson(f));
    attachBitrix(employees, bitrixSummaries, warnings);
  }
  const rangeKeys = new Set(employees.map(e => `${e.analyzed_range?.from}..${e.analyzed_range?.to}`));
  if (rangeKeys.size > 1) {
    warnings.push(`Analyzed ranges differ across employees (${[...rangeKeys].join(' | ')}) — deltas are self-vs-self, but group attention flags are weaker.`);
  }
  if (employees.length < 3) {
    warnings.push('Fewer than 3 employees — group outlier detection is skipped.');
  }

  const data = {
    schema_version: common.SCHEMA_VERSION,
    generated_at: new Date().toISOString(),
    innovation: args.innovation ? { name: String(args.innovation) } : (employees[0].innovation || null),
    source_files: files,
    employees,
    attention,
    verdicts,
    warnings,
  };

  const jsonFile = path.join(outDir, 'consolidated-summary.json');
  const htmlFile = path.join(outDir, 'dashboard.html');
  common.writeJsonPretty(jsonFile, data);
  buildDashboard(htmlFile, data);
  console.log(`wrote ${jsonFile}`);
  console.log(`wrote ${htmlFile} (employees=${employees.length}, attention=${Object.keys(attention).length})`);
}

if (require.main === module) {
  try { main(); } catch (e) { console.error(`consolidate.js: ${e.stack || e.message}`); process.exit(1); }
}
