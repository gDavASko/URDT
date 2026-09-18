#!/usr/bin/env node
'use strict';
// makeAnalyzerInput.js — build the compact analyzer payload for one window
// (token economy: the analyzer agent reads THIS file, never the full dump).
// Usage: node makeAnalyzerInput.js --run <run dir> --window <window_id>

const path = require('path');
const common = require('./lib/common.js');

const args = common.parseArgs(process.argv);
if (!args.run || !args.window) { console.error('need --run and --window'); process.exit(1); }
const run = path.resolve(String(args.run));
const wid = String(args.window);
const dump = common.readJson(path.join(run, 'registry', `dump-${wid}.json`));
const win = common.readJson(path.join(run, 'registry', `window-${wid}.json`));

const subjects = dump.commits.slice(0, 150).map(c => ({
  sha: c.sha.slice(0, 8),
  t: c.cc_type,
  s: String(c.subject).slice(0, 100),
  files: c.files.length,
  kinds: [...new Set(c.files.map(f => f.kind))],
  systems: [...new Set(c.files.map(f => f.system))].slice(0, 4),
}));

const samples = (dump.diff_samples || []).slice(0, 12).map(d => ({
  sha: d.sha.slice(0, 8),
  file: d.file,
  diff: String(d.diff).split('\n').slice(0, 90).join('\n'),
}));

const out = {
  window_id: wid,
  range: win.range,
  sample_gate: win.sample_gate,
  metrics_context: {
    m1: win.metrics.m1, m2: win.metrics.m2, m3: win.metrics.m3,
    m10: win.metrics.m10, m11: win.metrics.m11, m13: win.metrics.m13,
  },
  new_modules_signals: (dump.new_modules_signals || []).slice(0, 30),
  commits: subjects,
  unclassified_subjects: dump.commits.filter(c => c.cc_type === 'unclassified')
    .slice(0, 80).map(c => ({ sha: c.sha.slice(0, 8), s: String(c.subject).slice(0, 100) })),
  diff_samples: samples,
  notes: dump.notes || [],
};

const outFile = path.join(run, 'registry', `analyzer-input-${wid}.json`);
common.writeJson(outFile, out);
console.log(`wrote ${outFile} (${JSON.stringify(out).length} bytes)`);
