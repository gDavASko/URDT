/**
 * Experiment A: every polygon mechanic as a meta-AI task WITHOUT GDD and WITHOUT playbook hints —
 * only the module name (scope) and the success criterion. Measures what the universal L3 agent solves alone.
 * Output: URDT_Sandbox/experiments/nogdd_<ts>.json and .md
 */
import fs from 'node:fs';
import path from 'node:path';
import { getRuntime, shutdown, ROOT } from '../src/task/runtime.js';

const inv = JSON.parse(fs.readFileSync(path.join(ROOT, 'URDT_Sandbox/review/inventory.json'), 'utf-8'));
const only = process.argv[2]?.split(',');
const modules = Object.keys(inv).filter(m => !only || only.some(o => m.startsWith(o)));
const { runner } = await getRuntime();
const rows: any[] = [];
const outDir = path.join(ROOT, 'URDT_Sandbox/experiments');
fs.mkdirSync(outDir, { recursive: true });
const stamp = new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19);
for (const m of modules) {
  const r = await runner.run({
    taskId: `nogdd_${m}`, goal: `Complete mechanic ${m}`, target: { scope: m },
    success: { all: [{ beacon: '@scope', path: 'IsCompleted', op: '==', value: true }] },
    autonomy: 'full', budget: { timeMs: 150000, actions: 300 },
  });
  const solvedBy = r.status !== 'success' ? '-' : r.strategy.some(s => s.step.startsWith('skill:') && /COMPLETED/.test(s.outcome)) ? 'skill (recognized)' : 'explorer';
  const row = { module: m, status: r.status, solvedBy, actions: r.actions, seconds: +(r.durationMs / 1000).toFixed(1), learned: r.learnedSkills.slice(0, 4), findings: r.findings.filter(f => f.kind !== 'ASSUMPTION').map(f => `${f.kind}: ${f.message}`).slice(0, 3) };
  rows.push(row);
  console.log(`${m.padEnd(28)} ${r.status.padEnd(8)} ${solvedBy.padEnd(18)} ${row.seconds}s ${r.actions} actions`);
  fs.writeFileSync(path.join(outDir, `nogdd_${stamp}.json`), JSON.stringify(rows, null, 2));
}
const ok = rows.filter(r => r.status === 'success');
const md = [`# Experiment A — no GDD, no hints (${stamp})`, '', `Solved ${ok.length}/${rows.length}: explorer ${ok.filter(r => r.solvedBy === 'explorer').length}, recognized skill ${ok.filter(r => r.solvedBy !== 'explorer').length}.`, '',
  '| Mechanic | Result | Solved by | Actions | Time, s | Learned |', '|---|---|---|---|---|---|',
  ...rows.map(r => `| ${r.module} | ${r.status} | ${r.solvedBy} | ${r.actions} | ${r.seconds} | ${(r.learned[0] ?? '').replace(/\|/g, '/')} |`)].join('\n');
fs.writeFileSync(path.join(outDir, `nogdd_${stamp}.md`), md);
console.log(`SUMMARY solved ${ok.length}/${rows.length}`);
shutdown();
process.exit(0);
