/**
 * Regression gate — the only way a candidate skill reaches the shared core library.
 *
 *   npx tsx scripts/regression_gate.ts --baseline [--smoke M01_SnapToSlot,...]      record the smoke baseline
 *   npx tsx scripts/regression_gate.ts --candidate <name> --modules M33_Arkanoid[,...] [--runs 2]
 *   npx tsx scripts/regression_gate.ts --check-knowledge      smoke vs baseline; on regression roll the game
 *                                                             knowledge back to the baseline snapshot and keep the
 *                                                             rollback only if it fixes the regression
 *
 * Pass = the candidate completes each target module in every run (verified by the success predicate and chosen by
 * the knowledge ranking) AND no smoke module that passed in the baseline fails now. Pass → promoted to core;
 * the report is kept in CoreAgent/knowledge/core/gate_reports either way.
 */

import fs from 'node:fs';
import path from 'node:path';
import { getRuntime, shutdown } from '../src/task/runtime.js';
import { CORE_DIR } from '../src/knowledge/store.js';

process.on('unhandledRejection', e => console.error('[gate] unhandled rejection:', (e as Error)?.message ?? e));
const arg = (k: string) => { const i = process.argv.indexOf(`--${k}`); return i > 0 ? process.argv[i + 1] : undefined; };
const DEFAULT_SMOKE = 'M01_SnapToSlot,M05_TimelineSequencer,M09_CoverageAccumulator,M12_AngularDeltaTracker,M16_RhythmPhaseDetector,M22_GridPathfinding,M28_DualBridgeRoute,M34_Snake';
const smoke = (arg('smoke') ?? DEFAULT_SMOKE).split(',');
const baselineFile = path.join(CORE_DIR, 'gate_baseline.json');
const { runner } = await getRuntime();

async function play(module: string, tag: string) {
  const r: any = await runner.run({
    taskId: `gate_${tag}_${module}`, goal: `Complete every stage of ${module}`, context: { module },
    target: { scope: module }, success: { all: [{ beacon: '@scope', path: 'IsCompleted', op: '==', value: true }] },
    autonomy: 'full', budget: { timeMs: 240000, actions: 800 }, audit: false,
  }).catch((e: Error) => ({ status: 'error', summary: e.message, durationMs: 0, knowledge: { skillsTried: [] } }));
  return { module, ok: r.status === 'success', seconds: Math.round(r.durationMs / 100) / 10, tried: r.knowledge?.skillsTried ?? [] };
}

async function smokeRun(tag: string) {
  const out: Record<string, { ok: boolean; seconds: number }> = {};
  for (const m of smoke) {
    let r = await play(m, tag);
    // A single failure is not a regression: flaky mechanics are re-checked once before the verdict.
    if (!r.ok) { console.log(`  smoke ${m.padEnd(26)} FAIL ${r.seconds}s — re-checking`); r = await play(m, `${tag}_recheck`); }
    out[m] = { ok: r.ok, seconds: r.seconds };
    console.log(`  smoke ${m.padEnd(26)} ${r.ok ? 'ok' : 'FAIL'} ${r.seconds}s`);
  }
  return out;
}

fs.mkdirSync(path.join(CORE_DIR, 'gate_reports'), { recursive: true });
if (process.argv.includes('--baseline')) {
  const b = await smokeRun('baseline');
  const { store } = await runner.knowledge();
  const knowledgeSnapshot = store.snapshot('baseline');
  fs.writeFileSync(baselineFile, JSON.stringify({ at: new Date().toISOString(), knowledgeSnapshot, results: b }, null, 2));
  console.log(`baseline saved: ${Object.values(b).filter(x => x.ok).length}/${smoke.length} ok, knowledge snapshot ${knowledgeSnapshot}`);
} else if (process.argv.includes('--check-knowledge')) {
  const base = fs.existsSync(baselineFile) ? JSON.parse(fs.readFileSync(baselineFile, 'utf-8')) : null;
  if (!base) { console.error('no baseline: run --baseline first'); process.exit(2); }
  const { store } = await runner.knowledge();
  const now = await smokeRun('kcheck');
  const regressed = Object.entries(now).filter(([m, v]) => base.results[m]?.ok && !v.ok).map(([m]) => m);
  const report: any = { at: new Date().toISOString(), regressed, rolledBack: false };
  if (!regressed.length) console.log('knowledge OK: no regression against the baseline');
  else if (!base.knowledgeSnapshot) console.log(`regressions ${regressed.join(', ')} — baseline has no knowledge snapshot, cannot roll back`);
  else {
    const current = store.snapshot('pre-check-rollback');
    store.rollback(base.knowledgeSnapshot);
    const fixed: string[] = [];
    for (const m of regressed) { const r = await play(m, 'kcheck_rollback'); if (r.ok) fixed.push(m); }
    if (fixed.length) { report.rolledBack = true; report.fixed = fixed; console.log(`knowledge was harmful: rolled back to ${base.knowledgeSnapshot}; fixed ${fixed.join(', ')}`); }
    else { store.rollback(current); console.log(`regressions ${regressed.join(', ')} are not caused by knowledge (rollback did not help) — knowledge restored`); }
  }
  fs.writeFileSync(path.join(CORE_DIR, 'gate_reports', `knowledge-check-${Date.now()}.json`), JSON.stringify(report, null, 2));
} else {
  const name = arg('candidate');
  const modules = (arg('modules') ?? '').split(',').filter(Boolean);
  const runs = Number(arg('runs') ?? 2);
  if (!name || !modules.length) { console.error('usage: --candidate <name> --modules M..[,M..] [--runs 2]'); process.exit(2); }
  const baseline = fs.existsSync(baselineFile) ? JSON.parse(fs.readFileSync(baselineFile, 'utf-8')).results : null;
  const target: any[] = [];
  for (const m of modules) for (let i = 0; i < runs; i++) {
    let r = await play(m, `${name}_${i}`);
    let used = r.tried.find((t: any) => t.skill === name);
    // The candidate was not even tried (navigation/stale screen): that says nothing about the skill — retry once.
    if (!used) { r = await play(m, `${name}_${i}_retry`); used = r.tried.find((t: any) => t.skill === name); }
    target.push({ ...r, candidateUsed: !!used, candidateOk: !!used?.ok });
    console.log(`  target ${m} run ${i + 1}: ${r.ok ? 'ok' : 'FAIL'} ${r.seconds}s candidate ${used ? (used.ok ? 'solved it' : 'ran, did not solve') : 'not used'}`);
  }
  const now = await smokeRun(`cand_${name}`);
  const regressions = baseline ? Object.entries(now).filter(([m, v]) => baseline[m]?.ok && !v.ok).map(([m]) => m) : [];
  const targetOk = target.every(t => t.ok && t.candidateOk);
  const pass = targetOk && regressions.length === 0 && !!baseline;
  const report = { candidate: name, at: new Date().toISOString(), pass, targetOk, regressions, baselinePresent: !!baseline, target, smoke: now };
  fs.writeFileSync(path.join(CORE_DIR, 'gate_reports', `${name}-${Date.now()}.json`), JSON.stringify(report, null, 2));
  if (pass) {
    const { store } = await runner.knowledge();
    console.log(`PASS → promoted to core: ${store.promote(name)}`);
  } else {
    console.log(`REJECTED: ${!baseline ? 'no baseline (run --baseline first); ' : ''}${!targetOk ? 'candidate did not solve every target run; ' : ''}${regressions.length ? `regressions: ${regressions.join(', ')}` : ''}`);
  }
}
shutdown();
process.exit(0);
