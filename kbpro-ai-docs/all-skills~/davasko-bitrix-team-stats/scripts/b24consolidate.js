#!/usr/bin/env node
'use strict';
// b24consolidate.js — merge per-employee bitrix-summary.json files into the
// people dashboard. Incremental by design: works from ONE summary upward and
// is re-run after every finished employee. No ranking; attention flags
// (group median ± 1.5 MAD, 3+ employees) are 1:1 conversation topics.

const fs = require('fs');
const path = require('path');
const c = require('./lib/b24common.js');

const HELP = `b24consolidate.js — consolidated Bitrix24 people view

Usage:
  node b24consolidate.js --inputs <summary1.json,summary2.json|dir> --out-dir <dir>
                         [--verdicts verdicts.json] [--innovation "<name>"]`;

const ATTENTION_KEYS = [
  'b1.closed_pm', 'b2.cycle_days_median', 'b5.reopen_rate',
  'b3.hit_rate', 'b10.self_assigned_share',
];

function die(msg, code = 1) { console.error(msg); process.exit(code); }

function resolveInputs(arg) {
  const files = [];
  for (const p of String(arg).split(',').map(s => s.trim()).filter(Boolean)) {
    const full = path.resolve(p);
    if (fs.existsSync(full) && fs.statSync(full).isFile()) files.push(full);
    else if (fs.existsSync(full) && fs.statSync(full).isDirectory()) {
      for (const f of fs.readdirSync(full)) {
        if (/^bitrix-summary.*\.json$/.test(f)) files.push(path.join(full, f));
      }
    } else throw new Error(`input not found: ${p}`);
  }
  return [...new Set(files)];
}

function lastDelta(s, key) {
  const phases = (s.phases || []).filter(p => p.metrics);
  const last = phases[phases.length - 1];
  return last?.metrics?.b20?.deltas_pm?.[key];
}

function medianOf(nums) {
  const a = nums.filter(Number.isFinite).slice().sort((x, y) => x - y);
  if (!a.length) return undefined;
  const mid = Math.floor(a.length / 2);
  return a.length % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

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

function main() {
  const args = c.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  if (!args.inputs) die('missing --inputs');
  if (!args['out-dir']) die('missing --out-dir');
  const outDir = path.resolve(String(args['out-dir']));
  c.assertNotDriveC(outDir, 'out-dir');
  c.ensureDir(outDir);

  const files = resolveInputs(args.inputs);
  if (!files.length) die('no bitrix-summary json found in inputs');
  const employees = files.map(f => c.readJson(f));
  const bad = employees.filter(e => e.schema_version !== c.SCHEMA_VERSION);
  if (bad.length) die(`incompatible schema_version (need ${c.SCHEMA_VERSION}) in ${bad.length} input(s)`);

  const verdicts = args.verdicts && fs.existsSync(path.resolve(String(args.verdicts)))
    ? c.readJson(path.resolve(String(args.verdicts)))
    : {};
  const attention = detectAttention(employees);
  const warnings = [];
  if (employees.length < 3) warnings.push('Fewer than 3 employees — group outlier detection is skipped.');

  const data = {
    schema_version: c.SCHEMA_VERSION,
    generated_at: new Date().toISOString(),
    innovation: args.innovation ? { name: String(args.innovation) } : (employees[0].innovation || null),
    source_files: files,
    employees,
    attention,
    verdicts,
    warnings,
  };

  const templateFile = path.resolve(__dirname, '..', 'references', 'b24-dashboard-template.html');
  const template = fs.readFileSync(templateFile, 'utf8');
  const marker = '/*__DATA_JSON__*/null';
  if (!template.includes(marker)) die(`placeholder ${marker} not found in b24 dashboard template`);

  const jsonFile = path.join(outDir, 'b24-consolidated-summary.json');
  const htmlFile = path.join(outDir, 'b24-dashboard.html');
  c.writeJsonPretty(jsonFile, data);
  fs.writeFileSync(htmlFile, template.replace(marker, JSON.stringify(data)), 'utf8');
  console.log(`wrote ${jsonFile}`);
  console.log(`wrote ${htmlFile} (employees=${employees.length}, attention=${Object.keys(attention).length})`);
}

try { main(); } catch (e) { console.error(`b24consolidate.js: ${e.stack || e.message}`); process.exit(1); }
