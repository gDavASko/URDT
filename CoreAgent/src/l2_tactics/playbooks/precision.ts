/**
 * Precision-motor playbooks: continuous trajectories, dwell, holds and rotations.
 * All geometry is read from live beacons; the GDD only names roles and thresholds.
 */

import { Beacon, Point, dist, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';
import { FRAME_MS } from '../../l1_kinematics/motor_cortex.js';

const byNumericId = (a: Beacon, b: Beacon) => a.testId.localeCompare(b.testId, undefined, { numeric: true });

function find(parts: Beacon[], idOrRole: string): Beacon | undefined {
  return parts.find(b => b.testId === idOrRole) ?? parts.find(b => b.props.AreaType === idOrRole);
}

/** Pushes path samples out of circular hazard zones (keeps a safety margin). */
export function avoidHazards(path: Point[], hazards: Array<{ c: Point; r: number }>): Point[] {
  return path.map(p => {
    let q = { ...p };
    for (const h of hazards) {
      const d = dist(q, h.c);
      if (d < h.r) {
        const k = d < 1e-3 ? { x: 0, y: 1 } : { x: (q.x - h.c.x) / d, y: (q.y - h.c.y) / d };
        q = { x: h.c.x + k.x * h.r, y: h.c.y + k.y * h.r };
      }
    }
    return q;
  });
}

function densify(points: Point[], stepPx: number): Point[] {
  const out: Point[] = [points[0]];
  for (let i = 1; i < points.length; i++) {
    const a = points[i - 1], b = points[i];
    const n = Math.max(1, Math.ceil(dist(a, b) / stepPx));
    for (let k = 1; k <= n; k++) out.push({ x: a.x + (b.x - a.x) * k / n, y: a.y + (b.y - a.y) * k / n });
  }
  return out;
}

/** M08 / M29: drag a tool through ordered waypoints while staying clear of hazards. */
export async function tracePath(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tool: string; waypointPrefix: string; hazardRole?: string; hazardRadius?: number; closeLoop?: boolean; speedPxPerS?: number; tolerancePx?: number };
  for (let attempt = 1; attempt <= 4 && !ctx.expired(); attempt++) {
    const parts = await ctx.parts();
    const tool = find(parts, p.tool);
    const wps = parts.filter(b => b.testId.startsWith(p.waypointPrefix) && /\d+$/.test(b.testId)).sort(byNumericId);
    if (!tool || wps.length === 0) return { status: 'DISCOVERY_REQUIRED', summary: `tool/waypoints not observable (${wps.length})` };
    const hazards = p.hazardRole ? parts.filter(b => b.props.AreaType === p.hazardRole || b.testId === p.hazardRole).map(h => ({ c: h.center, r: (p.hazardRadius ?? 75) + 12 })) : [];
    const route = [tool.center, ...wps.map(w => w.center)];
    if (p.closeLoop) route.push(wps[0].center);
    const path = avoidHazards(densify(route, 12), hazards);
    const before = await ctx.module();
    const h = ctx.hypothesize(`tracing ${wps.length} waypoints${hazards.length ? ` around ${hazards.length} hazard(s)` : ''} completes the mechanic (attempt ${attempt})`);
    await ctx.motor.slice(path, { speedPxPerS: p.speedPxPerS ?? 380, dwellEndMs: 120, tremorPx: 0.4 });
    const st = await ctx.awaitCompleted(1500);
    ctx.resolve(h, st.completed);
    if (st.completed) return { status: 'COMPLETED', summary: `path traced in ${attempt} attempt(s)` };
    ctx.say(`progress ${before.progress.toFixed(2)} → ${st.progress.toFixed(2)}`);
    if (ctx.progressed(st.progress > before.progress)) break;
  }
  return { status: 'STUCK', summary: 'tracing did not complete', stagnationType: 'MICRO_STUCK' };
}

/** M09: serpentine scrub over every dirty cell with the brush tool. */
export async function scrubCoverage(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tool: string; cellPrefix: string };
  for (let pass = 1; pass <= 5 && !ctx.expired(); pass++) {
    const parts = await ctx.parts();
    const tool = find(parts, p.tool);
    const dirty = parts.filter(b => b.testId.startsWith(p.cellPrefix) && b.game?.IsCleaned === false && b.game?.IsPermanentHazard !== true);
    const st0 = await ctx.module();
    if (st0.completed) return { status: 'COMPLETED', summary: `clean after ${pass - 1} pass(es)` };
    if (!tool || dirty.length === 0) return { status: 'STUCK', summary: 'no dirty cells observable but not complete', stagnationType: 'MICRO_STUCK' };
    // Boustrophedon order: rows by y (top→bottom), alternating x direction.
    const rows = new Map<number, Beacon[]>();
    for (const c of dirty) {
      const key = Math.round(c.center.y / 20);
      rows.set(key, [...(rows.get(key) ?? []), c]);
    }
    const ordered: Point[] = [tool.center];
    [...rows.entries()].sort((a, b) => b[0] - a[0]).forEach(([, cells], i) => {
      cells.sort((a, b) => (i % 2 === 0 ? a.center.x - b.center.x : b.center.x - a.center.x));
      for (const c of cells) {
        const w = (c.rect?.w ?? 60) * 0.3;
        ordered.push({ x: c.center.x - w, y: c.center.y }, { x: c.center.x + w, y: c.center.y });
      }
    });
    const h = ctx.hypothesize(`scrubbing ${dirty.length} dirty cells in a serpentine pass raises coverage (pass ${pass})`);
    await ctx.motor.slice(ordered, { speedPxPerS: 600 });
    const st = await ctx.awaitProgress(st0.progress, 800);
    ctx.resolve(h, st.progress > st0.progress || st.completed);
    if (st.completed) return { status: 'COMPLETED', summary: `clean after ${pass} pass(es)` };
    if (ctx.progressed(st.progress > st0.progress)) break;
  }
  return { status: 'STUCK', summary: 'coverage stalled', stagnationType: 'MICRO_STUCK' };
}

/** M10: hold a pump and release inside the target band, compensating for observed latency. */
export async function holdInBand(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { button: string; bandMin: number; bandMax: number; fillRatePerS?: number };
  let lead = 0.02;
  for (let attempt = 1; attempt <= 5 && !ctx.expired(); attempt++) {
    const btn = await ctx.world.inspect(p.button);
    if (!btn) return { status: 'DISCOVERY_REQUIRED', summary: `${p.button} not observable` };
    // Let the gauge decay first so every attempt starts from a known low value.
    await ctx.world.waitFor(ctx.moduleId, b => Number(b.props.ProgressNormalized) < 0.05, 6000, 100);
    const aim = p.bandMin + (p.bandMax - p.bandMin) * 0.45 - lead;
    const h = ctx.hypothesize(`holding ${p.button} until gauge ≥ ${aim.toFixed(3)} lands in [${p.bandMin}, ${p.bandMax}]`);
    let lastSeen = 0;
    await ctx.motor.hold(btn.center, {
      maxMs: 8000, pollMs: 15,
      until: async () => { lastSeen = (await ctx.module()).progress; return lastSeen >= aim; },
    });
    await sleep(150);
    const st = await ctx.module();
    ctx.say(`released at ≈${lastSeen.toFixed(3)} → gauge settled; completed=${st.completed}`);
    ctx.resolve(h, st.completed);
    if (st.completed) return { status: 'COMPLETED', summary: `released inside band on attempt ${attempt}` };
    lead += 0.015;
    if (ctx.progressed(false)) break;
  }
  return { status: 'STUCK', summary: 'could not release inside the band', stagnationType: 'MICRO_STUCK' };
}

/** M11: hold the spray on each live target until it is extinguished. */
export async function sprayTargets(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { targetRole: string };
  while (!ctx.expired()) {
    const parts = await ctx.parts();
    const targets = parts.filter(b => b.props.AreaType === p.targetRole && b.game?.IsExtinguished === false);
    const st = await ctx.module();
    if (st.completed) return { status: 'COMPLETED', summary: 'all targets extinguished' };
    if (targets.length === 0) return { status: 'STUCK', summary: 'no burning targets observable', stagnationType: 'MICRO_STUCK' };
    const t = targets.sort((a, b) => Number(a.game.CurrentHp) - Number(b.game.CurrentHp))[0];
    // Keep the cone off hazards: aim at the target's side facing away from the nearest hazard, rotated as far as
    // the cone half-angle allows while the target stays inside the cone.
    const nozzle = parts.find(b => /nozzle|emitter|hose/i.test(b.testId));
    const hazards = parts.filter(b => /hazard|electric|panel|shield/i.test(b.testId) || /hazard/i.test(String(b.props.AreaType ?? '')));
    const modB = await ctx.world.inspect(ctx.moduleId);
    const half = Number(modB?.game?.ConeHalfAngle ?? 15);
    let aim = t.center;
    if (nozzle && hazards.length) {
      const n = nozzle.center;
      const angT = Math.atan2(t.center.y - n.y, t.center.x - n.x);
      const dT = Math.hypot(t.center.x - n.x, t.center.y - n.y);
      const hz = hazards.map(hb => ({ hb, ang: Math.atan2(hb.center.y - n.y, hb.center.x - n.x) }))
        .sort((a, b) => Math.abs(angDiff(a.ang, angT)) - Math.abs(angDiff(b.ang, angT)))[0];
      const gap = angDiff(angT, hz.ang) * 180 / Math.PI;          // + : target is clockwise of the hazard
      if (Math.abs(gap) < half * 2.2) {
        const shift = Math.sign(gap || 1) * Math.min(half * 0.85, Math.max(0, half * 2.2 - Math.abs(gap))) * Math.PI / 180;
        aim = { x: n.x + Math.cos(angT + shift) * dT, y: n.y + Math.sin(angT + shift) * dT };
        ctx.say(`${t.testId} is ${gap.toFixed(0)}° from ${hz.hb.testId} (cone ±${half}°) — aiming ${(shift * 180 / Math.PI).toFixed(0)}° away from it`);
      }
    }
    const h = ctx.hypothesize(`aiming the nozzle at ${t.testId} (hp ${t.game.CurrentHp}) and holding extinguishes it`);
    const r = await ctx.motor.hold(aim, {
      maxMs: 5000, pollMs: 80,
      until: async () => (await ctx.world.inspect(t.testId))?.game?.IsExtinguished === true,
    });
    ctx.resolve(h, r.conditionMet);
    if (ctx.progressed(r.conditionMet)) return { status: 'STUCK', summary: `${t.testId} does not take damage`, stagnationType: 'MICRO_STUCK' };
  }
  return { status: 'TIMEOUT', summary: 'timeout', stagnationType: 'TIMEOUT' };
}

/** M12: rotate the working valve (skipping jammed decoys) in circles until the angle budget is met. */
export async function rotateWheel(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { turnsPerGesture?: number };
  const parts = await ctx.parts();
  const wheel = parts.find(b => b.game?.IsJammed === false && 'AccumulatedAngle' in (b.game ?? {}));
  if (!wheel) return { status: 'DISCOVERY_REQUIRED', summary: 'no free (non-jammed) wheel observable' };
  const radius = Math.min(wheel.rect?.w ?? 200, wheel.rect?.h ?? 200) * 0.32;
  for (let g = 1; g <= 6 && !ctx.expired(); g++) {
    const turns = p.turnsPerGesture ?? 1.25;
    const pts: Point[] = [];
    const steps = Math.round(36 * turns);
    for (let i = 0; i <= steps; i++) {
      const a = (i / 36) * 2 * Math.PI;
      pts.push({ x: wheel.center.x + radius * Math.cos(a), y: wheel.center.y + radius * Math.sin(a) });
    }
    const before = await ctx.module();
    const h = ctx.hypothesize(`${turns} circular turn(s) on ${wheel.testId} add ≈${Math.round(turns * 360)}° to the accumulator`);
    await ctx.motor.slice(pts, { speedPxPerS: 520, dwellEndMs: 40, tremorPx: 0.3 });
    const st = await ctx.awaitProgress(before.progress, 500);
    const w = await ctx.world.inspect(wheel.testId);
    ctx.say(`accumulated ${Number(w?.game?.AccumulatedAngle ?? 0).toFixed(0)}°, progress ${st.progress.toFixed(2)}`);
    ctx.resolve(h, st.progress > before.progress);
    if (st.completed) return { status: 'COMPLETED', summary: `${g} rotation gesture(s)` };
    if (ctx.progressed(st.progress > before.progress)) break;
  }
  return { status: 'STUCK', summary: 'wheel does not accumulate', stagnationType: 'MICRO_STUCK' };
}

/** M07: attach the tool, wobble to fatigue the spring, then pull out in the same gesture. */
export async function wobbleExtract(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tool: string; item: string; amplitudePx?: number };
  let parts = await ctx.parts();
  const tool = find(parts, p.tool);
  let item = find(parts, p.item);
  if (!tool || !item) return { status: 'DISCOVERY_REQUIRED', summary: 'tool/item not observable' };
  if (item.game?.IsForcepsAttached !== true) {
    const h = ctx.hypothesize(`dropping ${tool.testId} onto ${item.testId} attaches it`);
    await ctx.motor.drag(tool.center, item.center, { dwellEndMs: 150 }, `DRAG ${tool.testId}`);
    await sleep(300);
    item = (await ctx.world.inspect(item.testId)) ?? item;
    ctx.resolve(h, item.game?.IsForcepsAttached === true);
    if (item.game?.IsForcepsAttached !== true) return { status: 'STUCK', summary: 'tool did not attach', stagnationType: 'MICRO_STUCK' };
  }
  const amp = p.amplitudePx ?? 90;
  for (let cycles = 4; cycles <= 16 && !ctx.expired(); cycles += 4) {
    item = (await ctx.world.inspect(item.testId)) ?? item;
    const c = item.center;
    const pts: Point[] = [c];
    for (let i = 0; i < cycles; i++) pts.push({ x: c.x - amp, y: c.y - 10 }, { x: c.x + amp, y: c.y - 10 });
    pts.push({ x: c.x, y: c.y }, { x: c.x, y: c.y + 220 });
    const h = ctx.hypothesize(`${cycles} wobble cycles (±${amp}px) saturate fatigue, then an upward pull extracts ${item.testId}`);
    await ctx.motor.slice(pts, { speedPxPerS: 900, dwellEndMs: 60 });
    const st = await ctx.awaitCompleted(1200);
    const after = await ctx.world.inspect(item.testId);
    ctx.say(`fatigue ${Number(after?.game?.Fatigue ?? 0).toFixed(2)}, completed=${st.completed}`);
    ctx.resolve(h, st.completed);
    if (st.completed) return { status: 'COMPLETED', summary: `extracted with ${cycles} wobble cycles` };
  }
  return { status: 'STUCK', summary: 'extraction did not trigger', stagnationType: 'MICRO_STUCK' };
}

/** M23: move the lens over each real hidden target and dwell until it is collected. */
export async function lensDwell(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { lens: string; dwellMs: number };
  for (let guard = 0; guard < 8 && !ctx.expired(); guard++) {
    const parts = await ctx.parts();
    const lens = find(parts, p.lens);
    const targets = parts.filter(b => b.game?.IsJunkDust === false && b.game?.IsCollected === false);
    if ((await ctx.module()).completed) return { status: 'COMPLETED', summary: 'all targets revealed' };
    if (!lens || targets.length === 0) return { status: 'STUCK', summary: 'lens/targets not observable', stagnationType: 'MICRO_STUCK' };
    const t = targets.sort((a, b) => dist(a.center, lens.center) - dist(b.center, lens.center))[0];
    const h = ctx.hypothesize(`dwelling the lens ${p.dwellMs}ms over ${t.testId} collects it`);
    await ctx.motor.drag(lens.center, t.center, { dwellEndMs: p.dwellMs, durationMs: 400 }, `LENS→${t.testId}`);
    const after = await ctx.world.inspect(t.testId);
    ctx.resolve(h, after?.game?.IsCollected === true);
    if (ctx.progressed(after?.game?.IsCollected === true)) break;
  }
  const st = await ctx.awaitCompleted(1000);
  return st.completed ? { status: 'COMPLETED', summary: 'all targets revealed' } : { status: 'STUCK', summary: 'lens dwell did not collect', stagnationType: 'MICRO_STUCK' };
}

/** M31: carry the dispenser over each well in order and hold until that well reports full. */
export async function fillWells(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tool: string; wellPrefix: string; percentPrefix: string; holdMs: number };
  let parts = await ctx.parts();
  const wells = parts.filter(b => b.testId.startsWith(p.wellPrefix)).sort(byNumericId);
  const percents = parts.filter(b => b.testId.startsWith(p.percentPrefix)).sort((a, b) => a.center.x - b.center.x);
  if (wells.length === 0) return { status: 'DISCOVERY_REQUIRED', summary: 'wells not observable' };
  for (let i = 0; i < wells.length && !ctx.expired(); i++) {
    for (let attempt = 1; attempt <= 3; attempt++) {
      if ((await ctx.module()).completed) return { status: 'COMPLETED', summary: `${wells.length} wells filled in order` };
      parts = await ctx.parts();
      const tool = find(parts, p.tool);
      if (!tool) return { status: 'DISCOVERY_REQUIRED', summary: 'dispenser not observable' };
      const well = wells[i];
      const pct = percents.find(t => Math.abs(t.center.x - well.center.x) < 40);
      const readPct = async () => parseInt(PlaybookContext.textOf(pct ? await ctx.world.inspect(pct.testId) : null), 10) || 0;
      if (await readPct() >= 100) break;
      const h = ctx.hypothesize(`holding the dispenser over ${well.testId} for ${p.holdMs}ms fills it (attempt ${attempt})`);
      await ctx.motor.drag(tool.center, { x: well.center.x, y: tool.center.y }, { dwellEndMs: p.holdMs * attempt, durationMs: 350 }, `POUR→${well.testId}`);
      const full = (await readPct()) >= 100 || (await ctx.module()).completed;
      ctx.resolve(h, full);
      if (full) break;
    }
  }
  const st = await ctx.awaitCompleted(2000);
  return st.completed ? { status: 'COMPLETED', summary: `${wells.length} wells filled in order` } : { status: 'STUCK', summary: `wells not complete (progress ${st.progress.toFixed(2)})`, stagnationType: 'MICRO_STUCK' };
}

export const precisionFrame = FRAME_MS;

function angDiff(a: number, b: number): number {
  let d = a - b;
  while (d > Math.PI) d -= 2 * Math.PI;
  while (d < -Math.PI) d += 2 * Math.PI;
  return d;
}
