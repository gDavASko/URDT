/**
 * Timing & reaction playbooks. The agent measures its own actuation latency (request → input applied in
 * the engine) and leads moving targets by that amount, predicting motion from consecutive beacon samples.
 */

import { Beacon, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';

/** Rolling estimate of click actuation latency (ms), measured from real round-trips. */
class LatencyModel {
  private samples: number[] = [];
  constructor(private prior = 70) {}
  add(ms: number): void { this.samples.push(ms); if (this.samples.length > 12) this.samples.shift(); }
  get ms(): number {
    if (this.samples.length === 0) return this.prior;
    const s = [...this.samples].sort((a, b) => a - b);
    return s[Math.floor(s.length / 2)];
  }
}

async function timedTap(ctx: PlaybookContext, testId: string, lat: LatencyModel): Promise<boolean> {
  const t0 = Date.now();
  const r = await ctx.motor.tap({ testId }, 16);
  lat.add(Date.now() - t0);
  return r.ok;
}

function isReddish(hex: unknown): boolean {
  const s = String(hex ?? '');
  if (s.length < 6) return false;
  const r = parseInt(s.slice(0, 2), 16), g = parseInt(s.slice(2, 4), 16), b = parseInt(s.slice(4, 6), 16);
  return r > 200 && g < 140 && b < 140;
}

/** M15: strike when the (non-decoy) ball will be at the intercept line after our actuation latency. */
export async function timingIntercept(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { ball: string; line: string; button: string; tolerancePx: number };
  const lat = new LatencyModel(75);
  let strikes = 0, skippedDecoys = 0;
  const line = await ctx.world.inspect(p.line);
  if (!line) return { status: 'DISCOVERY_REQUIRED', summary: 'intercept line not observable' };
  let prev: { x: number; t: number } | null = null;
  let armed = true;
  while (!ctx.expired()) {
    const ball = await ctx.world.inspect(p.ball);
    const now = Date.now();
    if (!ball) { await sleep(20); continue; }
    if (strikes % 1 === 0 && (await ctx.module()).completed) return { status: 'COMPLETED', summary: `${strikes} strikes, ${skippedDecoys} decoys ignored` };
    const x = ball.center.x;
    const v = prev ? (x - prev.x) / Math.max(1, now - prev.t) * 1000 : 0; // px/s (negative = approaching)
    prev = { x, t: now };
    if (v > 50) { armed = true; await sleep(15); continue; }          // ball deflected / reset → re-arm
    if (!armed || v > -80) { await sleep(10); continue; }
    const decoy = isReddish(ball.props.ColorHex);
    const eta = (x - line.center.x) / -v * 1000;                        // ms until the ball reaches the line
    if (eta <= lat.ms + 12) {
      armed = false;
      if (decoy) { skippedDecoys++; ctx.say(`decoy ball (colour ${ball.props.ColorHex}) — holding fire`); continue; }
      const h = ctx.hypothesize(`strike now: ball ${x.toFixed(0)}px at ${v.toFixed(0)}px/s reaches the line in ${eta.toFixed(0)}ms ≈ latency ${lat.ms}ms`);
      const before = (await ctx.module()).progress;
      await timedTap(ctx, p.button, lat);
      strikes++;
      const st = await ctx.awaitProgress(before, 300);
      ctx.resolve(h, st.progress > before || st.completed);
      if (!(st.progress > before || st.completed)) ctx.progressed(false);
      if (ctx.stuckCounter > 6) return { status: 'STUCK', summary: 'strikes keep missing the window', stagnationType: 'MICRO_STUCK' };
    }
  }
  return { status: 'TIMEOUT', summary: `timeout after ${strikes} strikes`, stagnationType: 'TIMEOUT' };
}

/** M16: alternate two levers at a steady tempo inside the accepted interval window. */
export async function rhythmAlternate(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { left: string; right: string; periodMs: number; beats: number };
  const lat = new LatencyModel(70);
  const seq = [p.left, p.right];
  let next = performance.now();
  for (let i = 0; i < p.beats * 2 && !ctx.expired(); i++) {
    const wait = next - performance.now() - lat.ms;
    if (wait > 0) await sleep(wait);
    await timedTap(ctx, seq[i % 2], lat);
    next += p.periodMs;
    const st = await ctx.module();
    if (st.completed) return { status: 'COMPLETED', summary: `${i + 1} alternating presses at ${p.periodMs}ms` };
  }
  const st = await ctx.awaitCompleted(1500);
  return st.completed ? { status: 'COMPLETED', summary: 'rhythm held' } : { status: 'STUCK', summary: `rhythm not accepted (progress ${st.progress.toFixed(2)})`, stagnationType: 'MICRO_STUCK' };
}

/**
 * M17: out-pace a decaying accumulator by sustained taps at a human-plausible rate from the GDD, stopping
 * the instant the game reports completion (so post-conditions are judged at the moment of victory).
 */
export async function mashButton(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { button: string; tapsPerSecond?: number; readout?: string; approachWindow?: [number, number] };
  const btn = await ctx.world.inspect(p.button);
  if (!btn) return { status: 'DISCOVERY_REQUIRED', summary: `${p.button} not observable` };
  const period = 1000 / (p.tapsPerSecond ?? 10);
  if (p.readout && p.approachWindow) {
    // Boundary probe: bring the gauge into the approach window, then place the final tap so the game has to
    // decide completion just above its real threshold but below the goal promised to the player.
    const [lo, hi] = p.approachWindow;
    const read = async () => {
      const m = PlaybookContext.textOf(await ctx.world.inspect(p.readout!)).match(/(\d+)\s*%/);
      return m ? Number(m[1]) : NaN;
    };
    const hp = ctx.hypothesize(`boundary probe: final tap from a ${lo}–${hi}% gauge must not complete below the displayed goal`);
    for (let guard = 0; guard < 400 && !ctx.expired(); guard++) {
      const v = await read();
      if ((await ctx.module()).completed) break;
      if (v >= lo && v <= hi) {
        ctx.say(`gauge ${v}% inside the approach window → final tap`);
        await ctx.motor.tap({ testId: p.button }, 0);
        await sleep(40);
        const after = await read();
        const st = await ctx.module();
        ctx.say(`after final tap: gauge ${after}%, completed=${st.completed}`);
        if (st.completed) { ctx.resolve(hp, true); return { status: 'COMPLETED', summary: `boundary probe: completed at ${after}%` }; }
      } else if (!(v > hi)) {
        await ctx.motor.tap({ testId: p.button }, 0); // below the window: climb at the paced rate
        await sleep(period);
      } else {
        await sleep(15);                               // above the window: let the decay bring it down
      }
    }
    ctx.say('boundary probe inconclusive — falling back to sustained tapping');
  }
  const h = ctx.hypothesize(`tapping ${p.button} at ${(1000 / period).toFixed(0)} taps/s out-paces the decay and reaches the goal`);
  let taps = 0;
  const started = Date.now();
  while (!ctx.expired()) {
    const t = Date.now();
    await ctx.motor.tap({ testId: p.button }, 0);
    taps++;
    const st = await ctx.module();
    if (st.completed) {
      ctx.say(`${taps} taps in ${((Date.now() - started) / 1000).toFixed(1)}s → completed`);
      ctx.resolve(h, true);
      return { status: 'COMPLETED', summary: `${taps} taps at ${(1000 / period).toFixed(0)}/s` };
    }
    if (taps % 10 === 0) ctx.say(`${taps} taps → progress ${st.progress.toFixed(2)}`);
    await sleep(Math.max(0, period - (Date.now() - t)));
  }
  ctx.resolve(h, false);
  return { status: 'TIMEOUT', summary: `${taps} taps without reaching the threshold`, stagnationType: 'TIMEOUT' };
}

/** M20: release swinging blocks when their predicted x (after latency) is over the stack axis. */
export async function stackDrop(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { block: string; button: string; base: string; tolerancePx: number; blocks: number };
  const lat = new LatencyModel(75);
  const base = await ctx.world.inspect(p.base);
  if (!base) return { status: 'DISCOVERY_REQUIRED', summary: 'base not observable' };
  for (let placed = 0; placed < p.blocks + 4 && !ctx.expired();) {
    const st0 = await ctx.module();
    if (st0.completed) return { status: 'COMPLETED', summary: `${placed} drops` };
    // The landing check is relative to the top of the tower: the highest already-placed block, else the base.
    // Placed blocks are clones of the swinging block (they inherit a copy of its beacon: ActiveBlock_1, …).
    const live = await ctx.world.inspect(p.block);
    const stacked = (await ctx.parts()).filter(b => b.testId !== p.block && b.testId.startsWith(p.block) && (!live || b.center.y < live.center.y - 20));
    const top = stacked.sort((a, b) => b.center.y - a.center.y)[0];
    const axis = top ? top.center.x : base.center.x;
    ctx.say(`tower axis ${axis.toFixed(0)}px (${top ? `top block ${top.testId}` : 'base platform'})`);
    let prev: { x: number; t: number } | null = null;
    let released = false;
    const deadline = Date.now() + 8000;
    while (!released && Date.now() < deadline) {
      const blk = await ctx.world.inspect(p.block);
      const now = Date.now();
      if (!blk || !blk.visible) { await sleep(30); continue; }
      if (prev) {
        const v = (blk.center.x - prev.x) / Math.max(1, now - prev.t) * 1000;
        const predicted = blk.center.x + v * (lat.ms / 1000);
        if (Math.abs(predicted - axis) <= Math.min(10, p.tolerancePx / 3) && Math.abs(v) > 30) {
          const h = ctx.hypothesize(`drop: block at ${blk.center.x.toFixed(0)}px moving ${v.toFixed(0)}px/s will be over axis ${axis.toFixed(0)} after ${lat.ms}ms`);
          await timedTap(ctx, p.button, lat);
          released = true;
          const st = await ctx.awaitProgress(st0.progress, 2500);
          ctx.resolve(h, st.progress > st0.progress || st.completed);
          placed++;
          if (st.completed) return { status: 'COMPLETED', summary: `${placed} drops` };
          await sleep(500);
        }
      }
      prev = { x: blk.center.x, t: now };
      await sleep(8);
    }
    if (!released && ctx.progressed(false)) break;
  }
  const st = await ctx.awaitCompleted(1500);
  return st.completed ? { status: 'COMPLETED', summary: 'tower built' } : { status: 'STUCK', summary: `tower incomplete (progress ${st.progress.toFixed(2)})`, stagnationType: 'MICRO_STUCK' };
}

/** M21: wait for the bite signal (indicator appears / bobber dips) and strike inside the window. */
export async function reactionStrike(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { bobber: string; button: string; indicatorRole?: string; dipPx: number; catches: number };
  const lat = new LatencyModel(70);
  let catches = 0;
  while (!ctx.expired()) {
    const st0 = await ctx.module();
    if (st0.completed) return { status: 'COMPLETED', summary: `${catches} catches` };
    // Learn the idle band of the bobber first (it bobs ±6px while waiting).
    const ys: number[] = [];
    for (let i = 0; i < 12; i++) { const b = await ctx.world.inspect(p.bobber); if (b) ys.push(b.center.y); await sleep(25); }
    const idle = ys.reduce((a, b) => a + b, 0) / Math.max(1, ys.length);
    const deadline = Date.now() + 6000;
    let bite: Beacon | null = null;
    while (Date.now() < deadline) {
      const b = await ctx.world.inspect(p.bobber);
      if (b && idle - b.center.y > p.dipPx) { bite = b; break; }
      await sleep(12);
    }
    if (!bite) { if (ctx.progressed(false)) break; continue; }
    const h = ctx.hypothesize(`bobber dipped ${(idle - bite.center.y).toFixed(0)}px below its idle band → striking inside the reaction window`);
    await timedTap(ctx, p.button, lat);
    const st = await ctx.awaitProgress(st0.progress, 600);
    ctx.resolve(h, st.progress > st0.progress || st.completed);
    if (st.progress > st0.progress || st.completed) catches++;
    if (st.completed) return { status: 'COMPLETED', summary: `${catches} catches (latency ≈${lat.ms}ms)` };
    await sleep(1300); // result display phase
  }
  return { status: 'STUCK', summary: `${catches} catches`, stagnationType: 'MICRO_STUCK' };
}

/** M24: pop rising targets, never touching hazard bombs. */
export async function popTargets(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { role: string };
  let pops = 0, misses = 0;
  while (!ctx.expired()) {
    const st = await ctx.module();
    if (st.completed) return { status: 'COMPLETED', summary: `${pops} pops, ${misses} misses` };
    const parts = await ctx.parts();
    const bombs = parts.filter(b => b.props.AreaType === p.role && b.game?.IsHazardBomb === true && b.visible);
    const live = parts.filter(b => b.props.AreaType === p.role && b.game?.IsHazardBomb === false && b.game?.IsPopped === false && b.visible)
      .filter(b => !bombs.some(bomb => Math.hypot(bomb.center.x - b.center.x, bomb.center.y - b.center.y) < 90))
      .sort((a, b) => b.center.y - a.center.y);
    if (live.length === 0) { await sleep(60); continue; }
    const t = live[0];
    const r = await ctx.motor.tap({ testId: t.testId }, 16);
    const after = await ctx.world.inspect(t.testId);
    if (r.ok && (after?.game?.IsPopped === true || !after?.visible)) pops++; else misses++;
  }
  return { status: 'TIMEOUT', summary: `${pops} pops`, stagnationType: 'TIMEOUT' };
}
