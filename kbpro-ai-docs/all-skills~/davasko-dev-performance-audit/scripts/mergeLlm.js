#!/usr/bin/env node
'use strict';
// mergeLlm.js — merge analyzer agent outputs (llm-<wid>.json) into the window
// files: llm_scores, m4.confirmed, m10 unclassified reclassification.
// Usage: node mergeLlm.js --run <run dir>

const fs = require('fs');
const path = require('path');
const common = require('./lib/common.js');

const args = common.parseArgs(process.argv);
if (!args.run) { console.error('need --run'); process.exit(1); }
const registry = path.join(path.resolve(String(args.run)), 'registry');

let merged = 0;
for (const f of fs.readdirSync(registry).filter(x => /^llm-.*\.json$/.test(x))) {
  const wid = f.replace(/^llm-/, '').replace(/\.json$/, '');
  const winFile = path.join(registry, `window-${wid}.json`);
  if (!fs.existsSync(winFile)) { console.warn(`WARN: no window file for ${wid}`); continue; }
  let llm;
  try {
    llm = common.readJson(path.join(registry, f));
  } catch (e) {
    console.warn(`WARN: bad JSON in ${f}: ${e.message}`);
    continue;
  }
  const win = common.readJson(winFile);
  win.llm_scores = llm.llm_scores || {};
  if (llm.hybrid_updates) {
    if (win.metrics && win.metrics.m4 && llm.hybrid_updates.m4_confirmed != null) {
      win.metrics.m4.confirmed = Number(llm.hybrid_updates.m4_confirmed);
    }
    const recls = llm.hybrid_updates.m10_unclassified || {};
    const total = Object.values(recls).reduce((s, v) => s + (Number(v) || 0), 0);
    if (win.metrics && win.metrics.m10 && total > 0) {
      const m10 = win.metrics.m10;
      const movable = Math.min(total, m10.unclassified || 0);
      let moved = 0;
      for (const [k, v] of Object.entries(recls)) {
        if (!(k in m10) || k === 'unclassified') continue;
        const take = Math.min(Number(v) || 0, movable - moved);
        m10[k] += take;
        moved += take;
        if (moved >= movable) break;
      }
      m10.unclassified -= moved;
    }
  }
  win.llm_flags = llm.flags || [];
  common.writeJson(winFile, win);
  merged++;
  console.log(`merged ${wid}: scores=${Object.keys(win.llm_scores).length}, m4_confirmed=${win.metrics?.m4?.confirmed}`);
}
console.log(`done: ${merged} window(s) updated`);
