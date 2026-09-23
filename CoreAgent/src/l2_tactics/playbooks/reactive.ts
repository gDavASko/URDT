/**
 * Reactive / physics playbooks: continuous perception-action loops over moving entities.
 */

import { Beacon, Point, Rect, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';

const role = (b: Beacon) => String(b.props.AreaType ?? '');

/** Parses "Собрано: 3 / 5"-like counters. */
export function parseCounter(text: string): { cur: number; max: number } | null {
  const m = text.match(/(\d+)\s*\/\s*(\d+)/);
  return m ? { cur: +m[1], max: +m[2] } : null;
}

/**
 * M13 / M25 / M26 lane runner. Each tick: perceive avatar, coins, barriers; forbid lanes with a barrier
 * about to reach the avatar; head for the lane of the nearest reachable coin. The control scheme
 * (swipe / half-screen taps / direct drag) comes from the GDD and is cross-checked with GameState.
 */
export async function laneRunner(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { control: 'swipe' | 'tap_halves' | 'direct_drag'; avatar: string; coinRole: string; hazardRole: string; dangerPx: number };
  let lanes: number[] = [];
  let lastMove = 0;
  let moves = 0;
  // Response model check: one command must move exactly one lane. Measured after each command.
  let swipeLen = 140;
  let pending: { from: number; dir: number; at: number; x0: number } | null = null;
  let overshoots = 0;
  const seenX: number[] = [];
  while (!ctx.expired()) {
    const snap = await ctx.snapshot();
    const mod = snap.get(ctx.moduleId);
    if (!mod?.rect) return { status: 'DISCOVERY_REQUIRED', summary: 'module rect missing' };
    if (mod.props.IsCompleted === true) return { status: 'COMPLETED', summary: `${moves} lane changes` };
    const parts = snap.within(mod);
    const avatar = parts.find(b => b.testId === p.avatar || role(b) === p.avatar);
    const coins = parts.filter(b => role(b) === p.coinRole && b.visible);
    const hazards = parts.filter(b => role(b) === p.hazardRole && b.visible);
    if (!avatar) { await sleep(50); continue; }
    for (const o of [...coins, ...hazards]) seenX.push(o.center.x);
    if (seenX.length > 200) seenX.splice(0, seenX.length - 200);
    lanes = clusterLanes([...seenX, avatar.center.x]);
    if (lanes.length < 2) { await sleep(60); continue; }
    const laneOf = (x: number) => lanes.reduce((best, lx, i) => (Math.abs(lx - x) < Math.abs(lanes[best] - x) ? i : best), 0);
    const cur = laneOf(avatar.center.x);
    if (pending && Date.now() - pending.at > 260) {
      const moved = cur - pending.from;
      if (Math.abs(moved) > 1) {
        overshoots++;
        ctx.report('MAJOR', 'CONTROL', `one ${p.control} command moved ${Math.abs(moved)} lanes (lane ${pending.from} → ${cur}); with ${lanes.length} lanes the middle lane${lanes.length > 3 ? 's are' : ' is'} unreachable by a single ${p.control} — coins there cannot be collected`,
          { control: p.control, from: pending.from, to: cur, lanes: lanes.length, swipePx: swipeLen });
        if (p.control === 'swipe' && swipeLen > 45) { swipeLen = Math.max(45, Math.round(swipeLen / 2)); ctx.say(`workaround: shorter swipe ${swipeLen}px`); }
      }
      pending = null;
    }
    const ahead = (b: Beacon) => b.center.y - avatar.center.y;
    const danger = new Set(hazards.filter(h => ahead(h) > -20 && ahead(h) < p.dangerPx).map(h => laneOf(h.center.x)));
    const target = coins.filter(c => ahead(c) > -10).sort((a, b) => ahead(a) - ahead(b))
      .map(c => laneOf(c.center.x)).find(l => !danger.has(l) && !pathBlocked(cur, l, danger));
    let want = target ?? cur;
    if (danger.has(cur)) {
      want = [cur - 1, cur + 1].filter(l => l >= 0 && l < lanes.length && !danger.has(l)).sort((a, b) => Math.abs(a - (target ?? cur)) - Math.abs(b - (target ?? cur)))[0] ?? cur;
    }
    if (want !== cur && Date.now() - lastMove > 140) {
      const dir = Math.sign(want - cur);
      const y = mod.rect.y + mod.rect.h * 0.45;
      if (p.control === 'swipe') {
        await ctx.motor.drag({ x: mod.center.x, y }, { x: mod.center.x + dir * swipeLen, y }, { durationMs: 110, dwellStartMs: 16, dwellEndMs: 16 }, `SWIPE ${dir > 0 ? 'R' : 'L'}`);
      } else if (p.control === 'tap_halves') {
        await ctx.motor.tap({ point: { x: mod.center.x + dir * mod.rect.w * 0.25, y } }, 16);
      } else {
        await ctx.motor.drag(avatar.center, { x: lanes[want], y: avatar.center.y }, { durationMs: 140, dwellStartMs: 16, dwellEndMs: 16 }, 'STEER');
      }
      lastMove = Date.now();
      moves++;
      pending = { from: cur, dir, at: Date.now(), x0: avatar.center.x };
    } else {
      await sleep(25);
    }
  }
  return { status: 'TIMEOUT', summary: `timeout after ${moves} lane changes`, stagnationType: 'TIMEOUT' };
}

function pathBlocked(from: number, to: number, danger: Set<number>): boolean {
  for (let l = Math.min(from, to) + 1; l < Math.max(from, to); l++) if (danger.has(l)) return true;
  return false;
}

export function clusterLanes(xs: number[]): number[] {
  const sorted = [...xs].sort((a, b) => a - b);
  const clusters: number[][] = [];
  for (const x of sorted) {
    const last = clusters[clusters.length - 1];
    if (last && x - last[last.length - 1] < 60) last.push(x); else clusters.push([x]);
  }
  return clusters.filter(c => c.length >= 2).map(c => c.reduce((a, b) => a + b, 0) / c.length);
}

/**
 * M14 slingshot. Shot 1 is an exploratory launch; the agent samples the projectile's flight, fits
 * x(t), y(t) = y0 + vy·t − g·t²/2 and derives the launch gain k = |v0| / |pull| and gravity g. Every next
 * shot is solved by forward simulation for a pull vector that hits a remaining can and clears obstacles.
 */
/** Fitted launch physics per module: the same slingshot keeps its physics across stages and restarts, so the
 *  calibration shot is spent once — not on every stage (ammo is limited). */
const SLING_MODEL = new Map<string, { k: number; g: number }>();

export async function slingshot(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { ball: string; anchor: string; targetPrefix: string; obstacleRole: string; maxPullPx: number };
  let model: { k: number; g: number } | null = SLING_MODEL.get(ctx.moduleId) ?? null;
  // The game's hit point of a target (its pivot) may differ from the visual centre the beacon reports;
  // after a miss the agent shifts its aim point and after repeated misses it switches targets.
  const AIM_OFFSETS = [0, -30, -45, 20, -15, 35];
  const misses = new Map<string, number>();
  let unseen = 0;
  for (let shot = 1; shot <= 40 && !ctx.expired(); shot++) {
    const modB = await ctx.world.inspect(ctx.moduleId);
    // Stage change / fail restart: the board is being rebuilt — wait for it instead of giving up.
    if (modB?.game?.IsInTransition === true) { await sleep(300); shot--; continue; }
    const parts = await ctx.parts();
    if ((await ctx.module()).completed) return { status: 'COMPLETED', summary: `all targets down in ${shot - 1} shots` };
    const anchor = parts.find(b => b.testId === p.anchor);
    const ball = parts.find(b => b.testId === p.ball);
    const targets = parts.filter(b => b.testId.startsWith(p.targetPrefix) && b.game?.IsHit === false && b.game?.IsObstacle !== true);
    const obstacles = parts.filter(b => role(b) === p.obstacleRole && b.rect).map(b => b.rect!);
    if (!anchor || !ball || targets.length === 0) {
      if (++unseen > 20) return { status: 'STUCK', summary: 'slingshot elements not observable', stagnationType: 'MICRO_STUCK' };
      await sleep(250); shot--; continue;
    }
    unseen = 0;
    const a = anchor.center;
    let pull: Point;
    let aimed: Beacon | null = null;
    if (!model) {
      const ang = (35 * Math.PI) / 180;
      pull = { x: -Math.cos(ang) * p.maxPullPx * 0.8, y: -Math.sin(ang) * p.maxPullPx * 0.8 };
      ctx.hypothesize('exploratory shot at 35° / 80% pull to identify launch gain and gravity from the observed flight');
    } else {
      const ranked = [...targets].sort((x, y) => (misses.get(x.testId) ?? 0) - (misses.get(y.testId) ?? 0));
      const aimAt = ranked.map(t => {
        const m = misses.get(t.testId) ?? 0;
        return { ...t, center: { x: t.center.x, y: t.center.y + AIM_OFFSETS[m % AIM_OFFSETS.length] } } as Beacon;
      });
      const sol = solveShot(a, aimAt, obstacles, model, p.maxPullPx);
      if (!sol) return { status: 'FAILED', summary: 'no feasible pull vector for the remaining targets under the fitted model' };
      pull = sol.pull;
      aimed = sol.target;
      ctx.hypothesize(`model k=${model.k.toFixed(2)}/s g=${model.g.toFixed(0)}px/s² → pull (${pull.x.toFixed(0)},${pull.y.toFixed(0)}) hits ${aimed.testId} with ${sol.clearance.toFixed(0)}px obstacle clearance`);
    }
    const release: Point = { x: a.x + pull.x, y: a.y + pull.y };
    const before = await ctx.module();
    await ctx.motor.drag(ball.center, release, { durationMs: 450, dwellEndMs: 120, tremorPx: 0 }, 'PULL');
    // Sample the flight for the physics fit.
    const samples: Array<{ t: number; x: number; y: number }> = [];
    const t0 = Date.now();
    while (Date.now() - t0 < 2600) {
      const b = await ctx.world.inspect(p.ball);
      if (b) samples.push({ t: (Date.now() - t0) / 1000, x: b.center.x, y: b.center.y });
      await sleep(15);
    }
    const flight = samples.filter(s => Math.hypot(s.x - release.x, s.y - release.y) > 6).slice(0, 30);
    const fit = fitBallistic(flight, release);
    if (fit) {
      const k = Math.hypot(fit.vx, fit.vy) / Math.hypot(pull.x, pull.y);
      model = model ? { k: (model.k + k) / 2, g: (model.g + fit.g) / 2 } : { k, g: fit.g };
      SLING_MODEL.set(ctx.moduleId, model);
      ctx.say(`fitted flight: v0=(${fit.vx.toFixed(0)},${fit.vy.toFixed(0)}) g=${fit.g.toFixed(0)} → k=${k.toFixed(2)}`);
    }
    const st = await ctx.awaitProgress(before.progress, 400);
    const h = ctx.hypotheses[ctx.hypotheses.length - 1];
    const scored = st.progress > before.progress || st.completed;
    ctx.resolve(h, scored || (!aimed && !!fit));
    if (aimed && !scored) misses.set(aimed.testId, (misses.get(aimed.testId) ?? 0) + 1);
    if (st.completed) return { status: 'COMPLETED', summary: `all targets down in ${shot} shots` };
    await sleep(300);
  }
  return { status: 'STUCK', summary: 'targets remain after 14 shots', stagnationType: 'MICRO_STUCK' };
}

/**
 * Fits the launch from flight samples, anchored at the exact release point R (known: anchor + pull).
 * x(t) is linear → vx and the launch instant tL (where x = R.x); then y(τ) − R.y = vy·τ − g·τ²/2, τ = t − tL.
 * Samples after the first horizontal reversal (bounce off an obstacle/can) are discarded.
 */
export function fitBallistic(raw: Array<{ t: number; x: number; y: number }>, release: Point): { vx: number; vy: number; g: number } | null {
  const s: typeof raw = [];
  for (let i = 0; i < raw.length; i++) {
    if (i > 1 && Math.sign(raw[i].x - raw[i - 1].x) !== Math.sign(raw[1].x - raw[0].x) && Math.abs(raw[i].x - raw[i - 1].x) > 1) break;
    s.push(raw[i]);
  }
  if (s.length < 5) return null;
  const n = s.length;
  const mt = s.reduce((a, q) => a + q.t, 0) / n, mx = s.reduce((a, q) => a + q.x, 0) / n;
  const vx = s.reduce((a, q) => a + (q.t - mt) * (q.x - mx), 0) / s.reduce((a, q) => a + (q.t - mt) ** 2, 0);
  if (!isFinite(vx) || Math.abs(vx) < 1) return null;
  const tL = mt + (release.x - mx) / vx;
  // Two-parameter LSQ: dy = a·τ + b·τ² with a = vy, b = −g/2
  let s11 = 0, s12 = 0, s22 = 0, r1 = 0, r2 = 0;
  for (const q of s) {
    const tau = q.t - tL, dy = q.y - release.y;
    s11 += tau * tau; s12 += tau ** 3; s22 += tau ** 4; r1 += tau * dy; r2 += tau * tau * dy;
  }
  const det = s11 * s22 - s12 * s12;
  if (Math.abs(det) < 1e-12) return null;
  const a = (r1 * s22 - r2 * s12) / det;
  const b = (s11 * r2 - s12 * r1) / det;
  return { vx, vy: a, g: -2 * b };
}

function solve3(M: number[][], B: number[]): number[] | null {
  const det = (m: number[][]) => m[0][0] * (m[1][1] * m[2][2] - m[1][2] * m[2][1]) - m[0][1] * (m[1][0] * m[2][2] - m[1][2] * m[2][0]) + m[0][2] * (m[1][0] * m[2][1] - m[1][1] * m[2][0]);
  const d = det(M);
  if (Math.abs(d) < 1e-9) return null;
  return [0, 1, 2].map(i => det(M.map((row, r) => row.map((v, cIdx) => (cIdx === i ? B[r] : v)))) / d);
}

export function solveShot(anchor: Point, targets: Beacon[], obstacles: Rect[], m: { k: number; g: number }, maxPull: number): { pull: Point; target: Beacon; clearance: number } | null {
  let best: { pull: Point; target: Beacon; clearance: number } | null = null;
  for (const t of targets) {
    for (let deg = -10; deg <= 80; deg += 1) {
      for (let mag = 30; mag <= maxPull; mag += 3) {
        const ang = (deg * Math.PI) / 180;
        const vx = Math.cos(ang) * mag * m.k, vy = Math.sin(ang) * mag * m.k;
        let hit = false, clearance = Infinity;
        for (let tt = 0; tt < 2.5; tt += 0.005) {
          const x = anchor.x + vx * tt, y = anchor.y + vy * tt - 0.5 * m.g * tt * tt;
          for (const o of obstacles) {
            const dx = Math.max(o.x - x, 0, x - (o.x + o.w)), dy = Math.max(o.y - y, 0, y - (o.y + o.h));
            clearance = Math.min(clearance, Math.hypot(dx, dy));
          }
          if (clearance < 34) break;
          if (Math.hypot(x - t.center.x, y - t.center.y) < 10) { hit = true; break; }
          if (x > t.center.x + 60 || y < anchor.y - 400) break;
        }
        if (hit && clearance >= 34 && (!best || clearance > best.clearance)) {
          best = { pull: { x: -Math.cos(ang) * mag, y: -Math.sin(ang) * mag }, target: t, clearance };
        }
      }
    }
    if (best) return best;
  }
  return best;
}

/**
 * M27: two-finger control (touch channel 1 = throttle, channel 2 = brake). On the ground: hold throttle.
 * Airborne: stabilise pitch with short throttle/brake pulses (the game maps them to back/front flips).
 */
export async function carDrive(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { gas: string; brake: string; body: string; distanceText: string; pitchLimitDeg: number };
  const gas = await ctx.world.inspect(p.gas);
  const brake = await ctx.world.inspect(p.brake);
  if (!gas || !brake) return { status: 'DISCOVERY_REQUIRED', summary: 'pedals not observable' };
  let gasDown = false, brakeDown = false;
  const setGas = async (on: boolean) => { if (on !== gasDown) { on ? await ctx.motor.press(gas.center, 1) : await ctx.motor.release(gas.center, 1); gasDown = on; } };
  const setBrake = async (on: boolean) => { if (on !== brakeDown) { on ? await ctx.motor.press(brake.center, 2) : await ctx.motor.release(brake.center, 2); brakeDown = on; } };
  let lastDist = -1, lastProgressT = Date.now(), crashes = 0, stalls = 0;
  ctx.hypothesize('hold throttle on the ground; in the air counter pitch with brake (nose-up) / throttle (nose-down) pulses');
  try {
    while (!ctx.expired()) {
      const mod = await ctx.world.inspect(ctx.moduleId);
      if (mod?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `finish reached (${crashes} crash recoveries)` };
      const body = await ctx.world.inspect(p.body);
      const airborne = mod?.game?.IsAirborne === true;
      const pitch = body?.rotationZ ?? 0;
      const dText = PlaybookContext.textOf(await ctx.world.inspect(p.distanceText));
      const d = parseCounter(dText)?.cur ?? 0;
      if (d !== lastDist) { lastDist = d; lastProgressT = Date.now(); }
      if (/КРУШ|CRASH/i.test(dText) || Math.abs(pitch) > 100) {
        crashes++;
        await setGas(false); await setBrake(false);
        await sleep(1500);
        continue;
      }
      if (!airborne) {
        await setBrake(false);
        await setGas(true);
      } else if (pitch > p.pitchLimitDeg) {
        await setGas(false); await setBrake(true);
      } else if (pitch < -p.pitchLimitDeg) {
        await setBrake(false); await setGas(true);
      } else {
        await setBrake(false); await setGas(false);
      }
      if (Date.now() - lastProgressT > 5000) {
        // Stalled on a slope: roll back (brake = reverse) further each time to gain a longer run-up, then full throttle.
        stalls++;
        if (stalls > 5) break;
        const backMs = 800 + stalls * 700;
        ctx.say(`no distance progress for 5s at ${d}m (pitch ${pitch.toFixed(0)}°) — reversing ${backMs}ms for a run-up (#${stalls})`);
        await setGas(false); await setBrake(true); await sleep(backMs); await setBrake(false);
        await sleep(300);
        lastProgressT = Date.now();
      }
      await sleep(40);
    }
  } finally {
    await setGas(false); await setBrake(false);
  }
  return { status: 'STUCK', summary: `car stopped at ${lastDist}m`, stagnationType: 'MICRO_STUCK' };
}

/** M32: steer the plane's altitude to intercept the next star ahead (clouds are harmless per GDD). */
export async function glider(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { plane: string; starRole: string; container: string };
  let taps = 0;
  while (!ctx.expired()) {
    const snap = await ctx.snapshot();
    const mod = snap.get(ctx.moduleId);
    if (mod?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `${taps} altitude commands` };
    if (!mod) return { status: 'DISCOVERY_REQUIRED', summary: 'module missing' };
    const parts = snap.within(mod);
    const plane = parts.find(b => b.testId === p.plane || role(b) === p.plane);
    const box = parts.find(b => b.testId === p.container);
    const stars = parts.filter(b => role(b) === p.starRole && b.visible);
    if (!plane || !box?.rect) { await sleep(50); continue; }
    const next = stars.filter(s => s.center.x > plane.center.x - 10).sort((a, b) => a.center.x - b.center.x)[0];
    if (next && Math.abs(next.center.y - plane.center.y) > 10) {
      const y = Math.min(box.rect.y + box.rect.h - 20, Math.max(box.rect.y + 20, next.center.y));
      await ctx.motor.tap({ point: { x: plane.center.x + 120, y } }, 30);
      taps++;
    }
    await sleep(60);
  }
  return { status: 'TIMEOUT', summary: `${taps} altitude commands`, stagnationType: 'TIMEOUT' };
}
