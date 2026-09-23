/**
 * URDT Autonomous Reviewer — CLI entry point (Mode 1 mapping + Mode 2 GDD-driven play).
 *
 *   npx tsx src/reviewer.ts --gdd ../Docs/GDD/URDT_Polygon_Test_GDD.md [--only M01,M02] [--suite 2d|ui]
 *                           [--explore] [--no-slm] [--no-exploits] [--out ../URDT_Sandbox/review]
 */

import fs from 'node:fs';
import path from 'node:path';
import { UrdtWireClient } from './protocol/urdt_wire_client.js';
import { WorldModel, sleep } from './perception/world_model.js';
import { MotorCortex } from './l1_kinematics/motor_cortex.js';
import { MicroSlmArbiter } from './l2_tactics/micro_slm_arbiter.js';
import { PlaybookContext, PlaybookResult } from './l2_tactics/playbook_context.js';
import { PLAYBOOKS } from './l2_tactics/playbooks/index.js';
import { loadGdd, ScenarioSpec, ExploitSpec } from './l3_gdd/gdd_loader.js';
import { InvariantChecker } from './l3_gdd/invariant_checker.js';
import { AppNavigator } from './l3_gdd/app_navigator.js';
import { ScenarioReport, FailureEvidencePacket, writeEvidence, writeHtmlReport } from './l3_gdd/audit_report.js';

const argv = process.argv.slice(2);
const arg = (name: string, fallback?: string) => { const i = argv.indexOf(name); return i >= 0 && i + 1 < argv.length ? argv[i + 1] : fallback; };
const flag = (name: string) => argv.includes(name);

const ROOT = path.resolve(path.dirname(new URL(import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1')), '..', '..');
const GDD_PATH = path.resolve(arg('--gdd', path.join(ROOT, 'Docs', 'GDD', 'URDT_Polygon_Test_GDD.md'))!);
const OUT_DIR = path.resolve(arg('--out', path.join(ROOT, 'URDT_Sandbox', 'review'))!);
const HARNESS_DIR = path.join(ROOT, '.harness');
const REPORT_HTML = path.resolve(arg('--report', path.join(ROOT, 'Docs', 'QA_Audit_Report.html'))!);
const MAP_PATH = path.join(OUT_DIR, 'application_map.json');

function log(msg: string): void {
  console.log(`[${new Date().toISOString().slice(11, 23)}] ${msg}`);
}

async function main(): Promise<void> {
  fs.mkdirSync(OUT_DIR, { recursive: true });
  const gdd = loadGdd(GDD_PATH);
  const only = arg('--only')?.split(',').map(s => s.trim().toUpperCase());
  const suite = arg('--suite');
  const scenarios = gdd.scenarios.filter(s => (!only || only.some(o => s.id.toUpperCase().startsWith(o))) && (!suite || s.suite === suite));
  log(`GDD "${gdd.title}" v${gdd.version}: ${scenarios.length}/${gdd.scenarios.length} scenarios selected`);

  const client = new UrdtWireClient({ log });
  client.on('disconnected', () => log('Unity disconnected (Play Mode stop?) — reconnect loop engaged'));
  const hs = await client.connect(120000);
  log(`connected: ${hs.data?.instanceId ?? '?'} screen ${hs.data?.screen?.width}x${hs.data?.screen?.height}`);
  const consoleErrors: string[] = [];
  client.on('event:log_error', (d: any) => { consoleErrors.push(String(d?.message ?? d?.msg ?? JSON.stringify(d))); if (consoleErrors.length > 50) consoleErrors.shift(); });
  await client.call('subscribe', { events: ['log_error', 'scene_loaded'] });

  const world = new WorldModel(client);
  const motor = new MotorCortex(client);
  const arbiter = new MicroSlmArbiter();
  const slmLoaded = flag('--no-slm') ? false : await arbiter.initialize();
  log(`micro-SLM arbiter: ${slmLoaded ? 'Qwen GGUF loaded' : 'deterministic heuristic mode'}`);

  // ── Mode 1: application map ─────────────────────────────────────────────
  const navigator = new AppNavigator(world, motor, gdd.navigation, log, slmLoaded ? arbiter : undefined);
  navigator.load(MAP_PATH);
  if (flag('--explore') || Object.keys(navigator.map.nodes).length === 0) {
    log('MODE 1: exploring the application graph');
    await navigator.navigate(id => id.split('#')[0] === gdd.navigation.home, 'home');
    await navigator.explore(Number(arg('--explore-budget', '30')), 2);
    navigator.save(MAP_PATH);
    log(`MODE 1: ${Object.keys(navigator.map.nodes).length} screens, ${navigator.map.edges.length} transitions, ${navigator.map.structuralDefects.length} structural defects → ${MAP_PATH}`);
  }

  const reports: ScenarioReport[] = [];
  const checker = new InvariantChecker(world);

  for (let si = 0, retried = new Set<string>(); si < scenarios.length; si++) {
    const scenario = scenarios[si];
    motor.trace.length = 0;
    arbiter.decisions.length = 0;
    consoleErrors.length = 0;
    const started = Date.now();
    log(`══ ${scenario.id}: ${scenario.title} [${scenario.playbook}]`);
    const ctx = new PlaybookContext(client, world, motor, arbiter, scenario);
    let result: PlaybookResult;
    const exploitResults: ScenarioReport['exploits'] = [];

    try {
      await client.waitReady(120000);
      const entered = await enterScenario(ctx, navigator, gdd.navigation.entries);
      if (!entered) {
        result = { status: 'DISCOVERY_REQUIRED', summary: 'could not reach the scenario screen' };
      } else {
        if (!flag('--no-exploits')) {
          for (const ex of scenario.exploits ?? []) exploitResults.push(await runExploit(ctx, checker, ex));
        }
        const playbook = PLAYBOOKS[scenario.playbook];
        if (!playbook) throw new Error(`unknown playbook ${scenario.playbook}`);
        ctx.startWatch();
        result = await playbook(ctx);
        if (result.status !== 'COMPLETED' && ctx.latched.completed) {
          result = { status: 'COMPLETED', summary: `${result.summary} (completion latched by watcher)` };
        }
      }
    } catch (err) {
      result = { status: 'FAILED', summary: `agent exception: ${(err as Error).message}`, stagnationType: 'CRASH_EXCEPTION' };
      ctx.say((err as Error).stack ?? String(err));
    }
    ctx.stopWatch();
    // Play Mode was restarted under us (runtime unavailable / socket dropped): the scenario did not really
    // run, so wait for the new runtime, re-map from the fresh boot screen and retry it once.
    if (/E_RUNTIME_UNAVAILABLE|E_DISCONNECTED|E_NOT_CONNECTED/.test(result.summary) && !retried.has(scenario.id)) {
      retried.add(scenario.id);
      log(`   Unity runtime restarted during ${scenario.id} — waiting and retrying the scenario`);
      await client.waitReady(180000);
      await client.call('subscribe', { events: ['log_error', 'scene_loaded'] }).catch(() => undefined);
      si--;
      continue;
    }

    checker.fallback = (id: string) => ctx.latched.beacons.get(id) ?? null;
    const invariants = client.isReady ? await checker.evaluate(scenario.invariants, ctx.moduleId) : [];
    checker.fallback = null;
    const pass = invariants.length > 0 && invariants.every(i => i.pass || i.severity === 'MINOR') && exploitResults.every(e => e.pass);
    const verdict: ScenarioReport['verdict'] = result.status === 'DISCOVERY_REQUIRED' ? 'BLOCKED' : pass ? 'CERTIFIED' : 'FAILED';
    const report: ScenarioReport = {
      id: scenario.id, title: scenario.title, suite: scenario.suite, playbook: scenario.playbook,
      startedAt: new Date(started).toISOString(), durationMs: Date.now() - started, verdict, result,
      invariants, exploits: exploitResults, dod: scenario.dod, hypotheses: ctx.hypotheses,
      arbiterDecisions: [...arbiter.decisions], actions: [...motor.trace], log: ctx.log,
    };
    if (verdict !== 'CERTIFIED') {
      report.evidence = await buildEvidence(ctx, scenario, result, invariants, consoleErrors);
      const file = writeEvidence(HARNESS_DIR, report.evidence);
      log(`   evidence → ${file}`);
    }
    reports.push(report);
    log(`══ ${scenario.id}: ${verdict} — ${result.status}: ${result.summary} (${((Date.now() - started) / 1000).toFixed(1)}s)`);
    for (const i of invariants) log(`   ${i.pass ? '✓' : '✗'} ${i.id} ${i.text} (observed ${JSON.stringify(i.observed)})`);
    for (const e of exploitResults) log(`   ${e.pass ? '✓' : '✗'} exploit ${e.id} ${e.text}`);
    fs.writeFileSync(path.join(OUT_DIR, 'review_results.json'), JSON.stringify(reports, null, 2), 'utf-8');
  }

  navigator.save(MAP_PATH);
  writeHtmlReport(REPORT_HTML, { startedAt: new Date().toISOString(), gdd: GDD_PATH, slm: slmLoaded ? 'Qwen2.5-0.5B GGUF (node-llama-cpp)' : 'heuristic', map: navigator.map, reports });
  const certified = reports.filter(r => r.verdict === 'CERTIFIED').length;
  log(`SUMMARY: ${certified}/${reports.length} certified → ${REPORT_HTML}`);
  for (const r of reports) log(`  ${r.verdict.padEnd(9)} ${r.id.padEnd(28)} ${(r.durationMs / 1000).toFixed(1).padStart(6)}s  ${r.result.summary}`);
  arbiter.dispose();
  client.close();
  process.exit(certified === reports.length ? 0 : 1);
}

/** Navigates to the scenario's screen through the Mode 1 map (and relaunches 2D mechanics fresh). */
async function enterScenario(ctx: PlaybookContext, nav: AppNavigator, entries: Record<string, { window: string; via: string[] }>): Promise<boolean> {
  const s = ctx.scenario;
  const entry = entries[s.suite];
  if (!entry) return false;
  const atWindow = (id: string) => id.split('#')[0].split('+').includes(entry.window);
  const atCatalog = (id: string) => atWindow(id) && !id.includes('#');

  if (s.suite === 'ui') {
    if (!(await nav.navigate(atWindow, entry.window))) {
      // Unknown route: follow the declared entry path (it becomes part of the map).
      for (const via of entry.via) await nav.press(via);
    }
    return atWindow((await nav.current()).id);
  }

  // 2D: go to the catalog (leaving any running mechanic), then launch the mechanic's card.
  if (!(await nav.navigate(atCatalog, `${entry.window} catalog`))) {
    for (const via of entry.via) await nav.press(via);
  }
  if (!atCatalog((await nav.current()).id)) return false;
  const card = await ctx.ensureOnScreen(s.launch!);
  if (!card) return false;
  const opened = await ctx.tapUntil(s.launch!, async () => (await ctx.world.inspect(ctx.moduleId))?.visible === true, 8, 700);
  if (opened) {
    await nav.current();
    await sleep(300);
  }
  return opened;
}

async function runExploit(ctx: PlaybookContext, checker: InvariantChecker, ex: ExploitSpec): Promise<ScenarioReport['exploits'][number]> {
  ctx.say(`EXPLOIT ${ex.id}: ${ex.text}`);
  const baselines = await checker.baseline(ex.expect, ctx.moduleId);
  const parts = await ctx.parts();
  const junk = ex.junk ? parts.find(b => b.testId === ex.junk) : parts.find(b => b.kind === 'draggable' && (b.props.IsJunk === true || b.game?.IsBroken === true || b.game?.IsJunk === true));
  const target = ex.target ? parts.find(b => b.testId === ex.target) : parts.find(b => (b.kind === 'slot' || (b.kind === 'draggable' && b.game?.IsSource === false)) && PlaybookContext.isGameplay(b));
  switch (ex.action) {
    case 'drag_junk_to_slot':
      if (!junk || !target) { ctx.say('  no junk/target pair observable'); return { id: ex.id, text: ex.text, pass: false, results: [] }; }
      await ctx.motor.drag(junk.center, target.center, { dwellEndMs: 120 }, `EXPLOIT DRAG ${junk.testId}`);
      break;
    case 'drop_item_outside': {
      const item = parts.find(b => b.kind === 'draggable' && PlaybookContext.isGameplay(b) && b.props.IsJunk !== true);
      const mod = await ctx.world.inspect(ctx.moduleId);
      if (!item || !mod?.rect) return { id: ex.id, text: ex.text, pass: false, results: [] };
      await ctx.motor.drag(item.center, { x: mod.rect.x + 30, y: mod.rect.y + 30 }, { dwellEndMs: 80 }, `EXPLOIT DROP ${item.testId}`);
      break;
    }
    case 'spam_tap': {
      const btn = ex.button ? parts.find(b => b.testId === ex.button) : parts.find(b => b.kind === 'button' && b.visible);
      for (let i = 0; i < 10 && btn; i++) await ctx.motor.tap({ testId: btn.testId }, 16);
      break;
    }
    default:
      break;
  }
  await sleep(700);
  const results = await checker.evaluate(ex.expect, ctx.moduleId, baselines);
  const pass = results.every(r => r.pass);
  ctx.say(`  exploit ${ex.id}: ${pass ? 'game resisted (PASS)' : 'VIOLATION'}`);
  return { id: ex.id, text: ex.text, pass, results };
}

async function buildEvidence(ctx: PlaybookContext, s: ScenarioSpec, result: PlaybookResult, invariants: any[], consoleErrors: string[]): Promise<FailureEvidencePacket> {
  let shot: string | undefined;
  let dashcam: string[] = [];
  let logs: string[] = [];
  let snapshot: Array<Record<string, unknown>> = [];
  try {
    const cap = await ctx.client.request('capture', { screenshot: true, log_tail: 30, dashcam: true }, 15000);
    shot = cap.screenshot_jpeg_datauri ?? (cap.screenshot_b64 ? `data:image/png;base64,${cap.screenshot_b64}` : undefined);
    dashcam = Array.isArray(cap.dashcam_jpeg_b64) ? cap.dashcam_jpeg_b64 : [];
    logs = (cap.logs ?? []).filter((l: any) => /error|exception/i.test(l.level)).map((l: any) => l.msg);
  } catch { /* capture is best-effort, never an oracle */ }
  try {
    const parts = await ctx.parts();
    snapshot = parts.slice(0, 60).map(b => ({ id: b.testId, kind: b.kind, screenPixel: b.center, visible: b.visible, flags: { isInteractable: b.interactable, isSnapped: b.props.IsSnapped }, game: b.game }));
  } catch { /* disconnected */ }
  const violated = invariants.find(i => !i.pass);
  return {
    $schema: 'urdt/failure_evidence_v1.json',
    incidentId: `INC_${new Date().toISOString().replace(/[-:T.Z]/g, '').slice(0, 14)}_${s.id}`,
    timestamp: Date.now(),
    failingPhase: s.id,
    gddInvariantViolated: violated ? `${violated.id}: ${violated.text}` : 'none (playbook did not finish)',
    stagnationType: result.stagnationType ?? result.status,
    activeScene: 'URDT_TestPoligon_UI',
    beaconStateSnapshot: snapshot,
    actionHistoryBeforeFailure: ctx.motor.trace.slice(-30),
    diagnostics: {
      engineConsoleErrors: [...consoleErrors, ...logs].slice(-30),
      activeModalDialog: null,
      screenshotDataUri: shot,
      crashDashcamFrames: dashcam,
      recommendedAiFix: `${result.summary}. Hypotheses refuted: ${ctx.hypotheses.filter(h => h.outcome === 'refuted').map(h => h.text).slice(-3).join(' | ') || 'none'}`,
    },
  };
}

main().catch(err => {
  console.error('[URDT Reviewer] fatal:', err);
  process.exit(2);
});
