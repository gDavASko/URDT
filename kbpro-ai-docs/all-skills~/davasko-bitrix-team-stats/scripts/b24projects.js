#!/usr/bin/env node
'use strict';
// b24projects.js — indirect project (workgroup) signal extraction.
// The webhook has no sonet_group scope (user decision: do NOT extend it), so
// project NAMES are inferred: this script emits per-groupId signals (top tags,
// frequent title words, top responsibles, activity range, task counts); the
// ORCHESTRATOR then proposes semantic names / repo matches and the user
// confirms them once — result is cached in mapping-config.json.

const path = require('path');
const c = require('./lib/b24common.js');

const HELP = `b24projects.js — per-group signals for project name inference

Usage:
  node b24projects.js --raw <raw.json> --out <group-signals.json>`;

const STOP_WORDS = new Set(['в', 'на', 'по', 'для', 'из', 'не', 'и', 'с', 'к', 'the', 'a', 'to',
  'for', 'in', 'of', 'and', 'fix', 'dev', 'добавить', 'сделать', 'исправить', 'починить']);

function topN(map, n) {
  return [...map.entries()].sort((a, b) => b[1] - a[1]).slice(0, n)
    .map(([k, v]) => ({ value: k, count: v }));
}

function main() {
  const args = c.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  for (const req of ['raw', 'out']) {
    if (!args[req]) { console.error(`missing --${req}\n\n${HELP}`); process.exit(1); }
  }
  const raw = c.readJson(path.resolve(String(args.raw)));
  const usersById = new Map((raw.users || []).map(u => [u.id, u.name]));

  const groups = new Map();
  for (const t of raw.tasks || []) {
    let g = groups.get(t.groupId);
    if (!g) {
      g = { tasks: 0, tags: new Map(), words: new Map(), responsibles: new Map(), minDate: null, maxDate: null };
      groups.set(t.groupId, g);
    }
    g.tasks++;
    for (const tag of t.tags) g.tags.set(tag, (g.tags.get(tag) || 0) + 1);
    for (const w of String(t.title).toLowerCase().split(/[^a-zа-яё0-9_]+/i)) {
      if (w.length < 3 || STOP_WORDS.has(w)) continue;
      g.words.set(w, (g.words.get(w) || 0) + 1);
    }
    const resp = usersById.get(t.responsibleId) || `#${t.responsibleId}`;
    g.responsibles.set(resp, (g.responsibles.get(resp) || 0) + 1);
    const d = c.isoDay(t.createdDate);
    if (d) {
      if (!g.minDate || d < g.minDate) g.minDate = d;
      if (!g.maxDate || d > g.maxDate) g.maxDate = d;
    }
  }

  const registry = c.loadProjectRegistry();
  const out = [...groups.entries()]
    .sort((a, b) => b[1].tasks - a[1].tasks)
    .map(([groupId, g]) => {
      const known = registry.get(Number(groupId));
      return {
        groupId,
        known_name: known ? known.name : null,
        known_status: known ? known.status : null,
        tasks: g.tasks,
        active: { from: g.minDate, to: g.maxDate },
        top_tags: topN(g.tags, 6),
        top_title_words: topN(g.words, 10),
        top_responsibles: topN(g.responsibles, 6),
      };
    });

  const outFile = path.resolve(String(args.out));
  c.assertNotDriveC(outFile, 'group-signals output');
  c.writeJsonPretty(outFile, { schema_version: c.SCHEMA_VERSION, groups: out });
  console.log(`wrote ${outFile} (${out.length} groups)`);
}

try { main(); } catch (e) { console.error(`b24projects.js: ${e.stack || e.message}`); process.exit(1); }
