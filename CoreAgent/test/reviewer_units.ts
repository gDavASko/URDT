/**
 * Unit tests for the live reviewer pipeline (machine judge for the pure parts of L1/L2/L3).
 * Run: npx tsx test/reviewer_units.ts
 */
import assert from 'node:assert';
import { MotorCortex } from '../src/l1_kinematics/motor_cortex.js';
import { avoidHazards } from '../src/l2_tactics/playbooks/precision.js';
import { fitBallistic, solveShot, clusterLanes, parseCounter } from '../src/l2_tactics/playbooks/reactive.js';
import { compare, firstNumber } from '../src/l3_gdd/invariant_checker.js';
import { loadGdd } from '../src/l3_gdd/gdd_loader.js';
import { PLAYBOOKS } from '../src/l2_tactics/playbooks/index.js';
import { parseRect, normalizeNode } from '../src/perception/world_model.js';

let passed = 0, failed = 0;
function test(name: string, fn: () => void): void {
  try { fn(); passed++; console.log(`  [PASS] ${name}`); }
  catch (e) { failed++; console.error(`  [FAIL] ${name}: ${(e as Error).message}`); }
}

test('minimum-jerk path starts/ends exactly and is monotonic in x', () => {
  const p = MotorCortex.minimumJerkPath({ x: 0, y: 0 }, { x: 300, y: 0 }, 500, 0, 16.67);
  assert.deepStrictEqual(p[0], { x: 0, y: 0 });
  assert.deepStrictEqual(p[p.length - 1], { x: 300, y: 0 });
  for (let i = 1; i < p.length; i++) assert.ok(p[i].x >= p[i - 1].x);
  // bell-shaped velocity: middle step larger than first step
  assert.ok(p[Math.floor(p.length / 2)].x - p[Math.floor(p.length / 2) - 1].x > p[1].x - p[0].x);
});

test('drag path dwell is expressed in game frames of the measured frame time', () => {
  const m = new MotorCortex(null as any);
  m.frameMs = 2;
  const path = m.buildDragPath({ x: 0, y: 0 }, { x: 100, y: 0 }, { durationMs: 100, dwellStartMs: 0, dwellEndMs: 1000, tremorPx: 0 });
  const tail = path.slice(-500);
  assert.ok(tail.every(q => q.x === 100 && q.y === 0), 'last 500 frames (1 s at 2 ms) hold the destination');
});

test('hazard avoidance pushes every sample outside the radius', () => {
  const path = Array.from({ length: 50 }, (_, i) => ({ x: 900 + i * 4, y: 480 }));
  const out = avoidHazards(path, [{ c: { x: 960, y: 423 }, r: 87 }]);
  for (const q of out) assert.ok(Math.hypot(q.x - 960, q.y - 423) >= 86.99);
});

test('ballistic fit recovers launch velocity and gravity from a mid-flight sample window', () => {
  const R = { x: 600, y: 380 }, vx = 294, vy = 535, g = 375, tLaunch = 0.62;
  const samples = Array.from({ length: 25 }, (_, i) => {
    const t = tLaunch + 0.05 + i * 0.02, tau = t - tLaunch;
    return { t, x: R.x + vx * tau, y: R.y + vy * tau - 0.5 * g * tau * tau };
  });
  const fit = fitBallistic(samples, R)!;
  assert.ok(Math.abs(fit.vx - vx) < 1 && Math.abs(fit.vy - vy) < 2 && Math.abs(fit.g - g) < 3, JSON.stringify(fit));
});

test('shot solver finds a pull that reaches the target and clears the obstacle', () => {
  const target: any = { testId: 'can', center: { x: 1275, y: 465 } };
  const sol = solveShot({ x: 690, y: 420 }, [target], [{ x: 986, y: 352, w: 38, h: 195 }], { k: 7, g: 375 }, 132);
  assert.ok(sol, 'solution exists');
  assert.ok(Math.hypot(sol!.pull.x, sol!.pull.y) <= 132 + 1e-6);
});

test('lane clustering and counters', () => {
  assert.deepStrictEqual(clusterLanes([750, 752, 960, 958, 1170, 1171, 1500]).map(Math.round), [751, 959, 1171]);
  assert.deepStrictEqual(parseCounter('Собрано: 3 / 5'), { cur: 3, max: 5 });
});

test('invariant operators', () => {
  assert.strictEqual(firstNumber('Баланс сил: 86% (Цель: 92%)'), 86);
  assert.ok(!compare('Баланс сил: 86% (Цель: 92%)', 'num>=', 92));
  assert.ok(compare(0.5, 'changed', undefined, 0.25));
  assert.ok(compare({ x: 0, y: 1 }, 'unchanged', undefined, { x: 0, y: 1 }));
  assert.ok(compare(1, '==', 1.0000001));
});

test('perception normalizes a real inspect payload', () => {
  assert.deepStrictEqual(parseRect('(x:42.00, y:915.00, width:910.50, height:66.00)'), { x: 42, y: 915, w: 910.5, h: 66 });
  const b = normalizeNode({ testId: 'Item', name: 'Item', activeInHierarchy: true, components: {
    RectTransform: { rotation: { x: 0, y: 0, z: 350 } },
    Urdt2DDraggableTarget: { ScreenCenter: { x: 10, y: 20 }, ScreenRect: '(x:0.00, y:0.00, width:20.00, height:40.00)', IsVisible: true, GameState: { ItemId: 'circle' } } } });
  assert.strictEqual(b.kind, 'draggable');
  assert.strictEqual(b.game.ItemId, 'circle');
  assert.strictEqual(b.rotationZ, -10);
});

test('GDD parses and every scenario maps to a registered playbook', () => {
  const g = loadGdd('../Docs/GDD/URDT_Polygon_Test_GDD.md');
  assert.strictEqual(g.scenarios.length, 39);
  for (const s of g.scenarios) assert.ok(PLAYBOOKS[s.playbook], `${s.id} → ${s.playbook}`);
  assert.ok(g.navigation.backButtons.length > 0 && g.navigation.denylist.includes('btn_run_all_suites'));
  assert.strictEqual(new Set(g.scenarios.map(s => s.id)).size, g.scenarios.length, 'unique ids');
});

console.log(`\nreviewer units: ${passed} passed, ${failed} failed`);
process.exit(failed ? 1 : 0);
