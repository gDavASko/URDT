/**
 * URDT Autonomous Reviewer — 2D Environment Probe Runner
 * 
 * Connects to Unity URDT WebSocket server, discovers all UI elements,
 * builds a scene graph, and probes interactable elements with synthetic input.
 * 
 * Usage: npx tsx src/runner.ts [--port 9002] [--timeout 30000]
 */

import { UrdtWsClient, type StateEnvelope, type BeaconState } from './protocol/urdt_client.js';
import { UrdtPipeClient, RawCommandSerializer, UrdtCommandTypes } from './protocol/pipe_client.js';
import { TriadDiscoveryEngine, type ControlBinding } from './control_discovery/triad_discovery.js';
import { RuntimeGraphifyEngine } from './l3_gdd/runtime_graphify_engine.js';
import { FlashHoganTicker } from './l1_kinematics/flash_hogan.js';
import { MotorPrimitivesFactory } from './l1_kinematics/motor_primitives.js';
import { TacticalDispatcher } from './l2_tactics/tactical_dispatcher.js';
import { StagnationPolicy } from './l2_tactics/stagnation_policy.js';
import { FailureEvidenceBuilder } from './l3_gdd/failure_evidence_builder.js';
import * as fs from 'node:fs';
import * as path from 'node:path';

// ─── Config ──────────────────────────────────────────────────────────────────
const ARGS = process.argv.slice(2);
function getArg(name: string, fallback: string): string {
  const idx = ARGS.indexOf(name);
  return idx >= 0 && idx + 1 < ARGS.length ? ARGS[idx + 1] : fallback;
}
const PORT = parseInt(getArg('--port', '9002'), 10);
const TIMEOUT = parseInt(getArg('--timeout', '30000'), 10);
const WS_URL = `ws://127.0.0.1:${PORT}`;
const PIPE_NAME = process.env.URDT_PIPE_NAME || 'urdt_fast_ticker';
const REPORT_DIR = 'E:/Projects/URDT/URDT_Sandbox';
const REPORT_PATH = path.join(REPORT_DIR, 'probe_report.json');

// ─── Probe Result Types ──────────────────────────────────────────────────────
interface ProbeAction {
  beaconId: string;
  layer: string;
  action: string;
  screenPixel: { x: number; y: number };
  timestamp: number;
  result: 'OK' | 'NO_CHANGE' | 'ERROR' | 'TIMEOUT';
  detail?: string;
  durationMs: number;
}

interface ProbeReport {
  startTime: string;
  endTime: string;
  wsUrl: string;
  sceneName: string;
  totalBeacons: number;
  interactableBeacons: number;
  actionsPerformed: number;
  successCount: number;
  failureCount: number;
  actions: ProbeAction[];
  sceneGraph: Record<string, unknown>;
}

// ─── Helpers ─────────────────────────────────────────────────────────────────
function log(msg: string): void {
  const ts = new Date().toISOString().slice(11, 23);
  console.log(`[${ts}] ${msg}`);
}

function sleep(ms: number): Promise<void> {
  return new Promise(resolve => setTimeout(resolve, ms));
}

function generateIdempotencyKey(): string {
  return `probe-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

// ─── Main Runner ─────────────────────────────────────────────────────────────
async function main(): Promise<void> {
  log('╔═══════════════════════════════════════════════════════════╗');
  log('║   URDT AUTONOMOUS REVIEWER — 2D ENVIRONMENT PROBE       ║');
  log('╚═══════════════════════════════════════════════════════════╝');
  log(`Target: ${WS_URL}`);
  log(`Named Pipe: \\\\.\\pipe\\${PIPE_NAME}`);
  log('');

  const report: ProbeReport = {
    startTime: new Date().toISOString(),
    endTime: '',
    wsUrl: WS_URL,
    sceneName: '',
    totalBeacons: 0,
    interactableBeacons: 0,
    actionsPerformed: 0,
    successCount: 0,
    failureCount: 0,
    actions: [],
    sceneGraph: {},
  };

  // ── Step 1: Connect to Unity ────────────────────────────────────────────
  log('Step 1: Connecting to Unity URDT server...');
  const ws = new UrdtWsClient(WS_URL);

  let connected = false;
  const connectTimeout = setTimeout(() => {
    if (!connected) {
      log('ERROR: Connection timeout. Is Unity in Play Mode with UrdtServerHost?');
      log('  1. Open scene URDT_TestPoligon_UI');
      log('  2. Press Play in Unity Editor');
      log('  3. Re-run this script');
      process.exit(1);
    }
  }, TIMEOUT);

  ws.on('connected', () => {
    connected = true;
    clearTimeout(connectTimeout);
    log('  ✓ Connected to Unity URDT server');
  });

  ws.on('error', (err: Error) => {
    if (!connected) {
      log(`  ✗ Connection error: ${err.message}`);
    }
  });

  await ws.connect();
  await sleep(1000); // Wait for handshake

  if (!connected) {
    log('Waiting for handshake...');
    await sleep(3000);
  }

  if (!connected) {
    log('ERROR: Could not establish connection. Aborting.');
    ws.close();
    process.exit(1);
  }

  // ── Step 2: Query all UI elements ───────────────────────────────────────
  log('');
  log('Step 2: Discovering scene elements...');

  // Send query command
  const queryCmd = {
    $schema: 'urdt/command_v2.json' as const,
    idempotencyKey: generateIdempotencyKey(),
    worldRevision: 0,
    action: 'query',
    pointerId: 0,
    params: { selector: {} }, // empty selector = all elements
  };

  ws.sendCommand(queryCmd);
  log('  → Sent query (all elements)');

  // Wait for state update
  let state: StateEnvelope | null = null;
  const statePromise = new Promise<StateEnvelope>((resolve) => {
    const handler = (s: StateEnvelope) => {
      state = s;
      ws.removeListener('state', handler);
      resolve(s);
    };
    ws.on('state', handler);
  });

  // Also listen for query response
  let queryResponse: Record<string, unknown> | null = null;
  const responsePromise = new Promise<Record<string, unknown>>((resolve) => {
    const handler = (msg: Record<string, unknown>) => {
      queryResponse = msg;
      ws.removeListener('message', handler);
      resolve(msg);
    };
    ws.on('message', handler);
  });

  // Wait for either (with timeout)
  const raceResult = await Promise.race([
    statePromise,
    responsePromise,
    sleep(5000).then(() => null),
  ]);

  if (!raceResult && !state && !queryResponse) {
    log('  ✗ No response to query within 5s');
    log('  Trying to read latest state...');
    state = ws.getLatestState();
  }

  // Process discovered elements
  const beacons: BeaconState[] = state?.beacons || [];
  report.sceneName = state?.sceneName || 'unknown';
  report.totalBeacons = beacons.length;

  log(`  Scene: ${report.sceneName}`);
  log(`  Total beacons: ${beacons.length}`);

  if (beacons.length === 0) {
    log('  ⚠ No beacons discovered. Scene may need TestId labels.');
    log('  Attempting hit_test scan of screen grid...');
    
    // Fallback: scan screen grid with hit_test
    const screenPoints = [
      { x: 960, y: 540 },  // center
      { x: 480, y: 270 },  // top-left quarter
      { x: 1440, y: 270 }, // top-right quarter
      { x: 480, y: 810 },  // bottom-left quarter
      { x: 1440, y: 810 }, // bottom-right quarter
    ];

    for (const pt of screenPoints) {
      const hitCmd = {
        $schema: 'urdt/command_v2.json' as const,
        idempotencyKey: generateIdempotencyKey(),
        worldRevision: state?.worldRevision || 0,
        action: 'hit_test',
        pointerId: 0,
        params: { x: pt.x, y: pt.y },
      };
      ws.sendCommand(hitCmd);
      log(`  → Hit test at (${pt.x}, ${pt.y})`);
      await sleep(200);
    }
    await sleep(1000);
  }

  // ── Step 3: Build scene graph ───────────────────────────────────────────
  log('');
  log('Step 3: Building scene graph...');

  const graphify = new RuntimeGraphifyEngine();
  const triadEngine = new TriadDiscoveryEngine();

  // Register beacons as graph nodes
  const sceneName = report.sceneName || 'unknown';
  const activeModalId = state?.activeModalId || null;
  const beaconNodes = beacons.map(b => ({ id: b.id, semanticLabel: b.id }));
  graphify.registerState(sceneName, activeModalId, beaconNodes);

  const { hash: sceneHash } = graphify.computeNodeHash(sceneName, activeModalId, beaconNodes);
  log(`  Scene hash: ${sceneHash}`);

  // Discover control triads via semantic correlation
  const triadMap = triadEngine.correlateSemantics(
    beacons.map(b => ({
      id: b.id,
      semanticLabel: b.id,
      controlType: b.layer === 'UI_CANVAS' ? 'button' : 'stick',
      screenPixel: b.screenPixel,
    }))
  );

  const interactable = beacons.filter(b => b.flags.isInteractable);
  report.interactableBeacons = interactable.length;
  log(`  Interactable beacons: ${interactable.length}`);
  log(`  Triad bindings discovered: ${triadMap.size}`);

  report.sceneGraph = {
    hash: sceneHash,
    beaconCount: beacons.length,
    interactableCount: interactable.length,
    triadBindingCount: triadMap.size,
  };

  // ── Step 4: Probe interactable elements ─────────────────────────────────
  log('');
  log('Step 4: Probing interactable elements...');

  const stagnation = new StagnationPolicy();
  const flashHogan = new FlashHoganTicker();
  const motorFactory = new MotorPrimitivesFactory();

  // Determine actions for each interactable
  const targets = interactable.length > 0 ? interactable : beacons.slice(0, 10);

  for (let i = 0; i < targets.length; i++) {
    const beacon = targets[i];
    const actionStart = Date.now();

    // Choose action based on beacon layer
    let actionType = 'click';
    if (beacon.layer === 'PHYSICS_2D' || beacon.layer === 'PHYSICS_3D') {
      actionType = 'drag';
    }

    log(`  [${i + 1}/${targets.length}] ${actionType} → ${beacon.id} at (${beacon.screenPixel.x}, ${beacon.screenPixel.y})`);

    // Send the command
    const actionCmd = {
      $schema: 'urdt/command_v2.json' as const,
      idempotencyKey: generateIdempotencyKey(),
      worldRevision: state?.worldRevision || 0,
      action: actionType,
      pointerId: 1,
      params: {
        x: beacon.screenPixel.x,
        y: beacon.screenPixel.y,
        ...(actionType === 'drag' ? { endX: beacon.screenPixel.x + 100, endY: beacon.screenPixel.y, durationMs: 300 } : {}),
        ...(actionType === 'swipe' ? { dx: 0, dy: -100, durationMs: 200 } : {}),
      },
    };

    ws.sendCommand(actionCmd);
    await sleep(500); // Wait for action to complete

    // Check result via state change
    const postState = ws.getLatestState();
    const elapsed = Date.now() - actionStart;

    const probeAction: ProbeAction = {
      beaconId: beacon.id,
      layer: beacon.layer,
      action: actionType,
      screenPixel: beacon.screenPixel,
      timestamp: Date.now(),
      result: 'OK',
      durationMs: elapsed,
    };

    if (postState && postState.worldRevision !== (state?.worldRevision || 0)) {
      probeAction.result = 'OK';
      probeAction.detail = `worldRevision changed: ${state?.worldRevision} → ${postState.worldRevision}`;
      report.successCount++;
      log(`    ✓ OK (${elapsed}ms) - world revision changed`);
    } else {
      probeAction.result = 'NO_CHANGE';
      probeAction.detail = 'No world revision change detected';
      report.failureCount++;
      log(`    ○ No change detected (${elapsed}ms)`);
    }

    report.actions.push(probeAction);
    report.actionsPerformed++;

    // Check stagnation
    const stagnationResult = stagnation.checkStagnation({
      lastActionTimestampMs: actionStart,
      lastStateChangeTimestampMs: postState ? Date.now() : actionStart - 5000,
      genre: 'UI_MENU',
      stuckCounter: report.failureCount,
    });
    if (stagnationResult.isStagnated) {
      log(`  ⚠ Stagnation detected (level ${stagnationResult.stuckLevel}) — ${stagnationResult.recommendedAction}`);
      await sleep(1000);
    }
  }

  // ── Step 5: Generate report ─────────────────────────────────────────────
  log('');
  log('Step 5: Generating probe report...');

  report.endTime = new Date().toISOString();

  // Ensure output directory exists
  if (!fs.existsSync(REPORT_DIR)) {
    fs.mkdirSync(REPORT_DIR, { recursive: true });
  }

  fs.writeFileSync(REPORT_PATH, JSON.stringify(report, null, 2), 'utf-8');
  log(`  Report saved: ${REPORT_PATH}`);

  // ── Summary ─────────────────────────────────────────────────────────────
  log('');
  log('╔═══════════════════════════════════════════════════════════╗');
  log('║                   PROBE SUMMARY                          ║');
  log('╠═══════════════════════════════════════════════════════════╣');
  log(`║  Scene:        ${report.sceneName.padEnd(41)}║`);
  log(`║  Beacons:      ${String(report.totalBeacons).padEnd(41)}║`);
  log(`║  Interactable: ${String(report.interactableBeacons).padEnd(41)}║`);
  log(`║  Actions:      ${String(report.actionsPerformed).padEnd(41)}║`);
  log(`║  Success:      ${String(report.successCount).padEnd(41)}║`);
  log(`║  No Change:    ${String(report.failureCount).padEnd(41)}║`);
  log('╚═══════════════════════════════════════════════════════════╝');

  // Generate failure evidence if needed
  if (report.failureCount > 0) {
    const failedActions = report.actions.filter(a => a.result !== 'OK');
    const packet = FailureEvidenceBuilder.build({
      failingPhase: '2d-environment-probe',
      gddInvariantViolated: 'All interactable beacons must respond to synthetic input',
      stagnationType: 'MICRO_STUCK',
      lastKnownWorldRevision: state?.worldRevision || 0,
      activeScene: report.sceneName,
      actionHistoryBeforeFailure: failedActions.map(fa => ({
        beaconId: fa.beaconId,
        action: fa.action,
        screenPixel: fa.screenPixel,
        detail: fa.detail || '',
      })),
    });

    const evidencePath = path.join(REPORT_DIR, 'probe_evidence.json');
    FailureEvidenceBuilder.exportToFile(packet, evidencePath);
    log(`  Evidence packet: ${evidencePath}`);
  }

  // Cleanup
  ws.close();
  log('');
  log('Done. Disconnected from Unity.');
  process.exit(0);
}

// ── Entry Point ──────────────────────────────────────────────────────────────
main().catch(err => {
  console.error('[URDT Runner] Fatal error:', err);
  process.exit(1);
});
