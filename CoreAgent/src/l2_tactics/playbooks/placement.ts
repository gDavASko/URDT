/**
 * Placement playbooks: drag-and-drop mechanics (snap-to-slot, sorting, layered assembly, wiring,
 * program sequencing, dress-up, bridge building, scale balancing).
 *
 * The GDD states the matching rule (e.g. item.ItemId == slot.SlotId); every coordinate, identity and
 * state comes from live beacons. When the GDD gives no rule the playbook learns one by trial: it tries an
 * untested (item, target) pair and keeps the pairing only if the game rewarded it.
 */

import { Beacon, Point, dist, rectContains, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';

type Key = string;

export function prop(b: Beacon, key: Key): unknown {
  if (key === 'id:suffix') return b.testId.split('_').pop();
  if (key === 'id') return b.testId;
  if (key.startsWith('game.')) return b.game?.[key.slice(5)];
  if (b.game && key in b.game) return b.game[key];
  return b.props[key];
}

function norm(v: unknown): string {
  return String(v ?? '').trim().toLowerCase();
}

function matches(item: Beacon, target: Beacon, rule: Array<[Key, Key]>): boolean {
  return rule.every(([ik, tk]) => norm(prop(item, ik)) !== '' && norm(prop(item, ik)) === norm(prop(target, tk)));
}

function filterBy(b: Beacon, filter?: Record<string, unknown>): boolean {
  if (!filter) return true;
  return Object.entries(filter).every(([k, v]) => {
    const actual = prop(b, k);
    return typeof v === 'string' && v.startsWith('~') ? norm(actual).includes(v.slice(1).toLowerCase()) : actual === v;
  });
}

function isJunk(b: Beacon): boolean {
  return b.props.IsJunk === true || b.game?.IsJunk === true || b.game?.IsBroken === true;
}

/** Receptacle capacity: multi-item containers expose Current/RequiredCount; single slots are used once. */
function hasRoom(t: Beacon, used: Set<string>): boolean {
  const req = t.game?.RequiredCount;
  const cur = t.game?.CurrentCount;
  if (typeof req === 'number' && typeof cur === 'number' && req > 1) return cur < req && t.game?.IsFull !== true;
  return !used.has(t.testId) && t.game?.IsOccupied !== true && t.game?.IsFull !== true;
}

function isPlaced(b: Beacon, placedKeys: string[]): boolean {
  return placedKeys.some(k => prop(b, k) === true);
}

export interface PlacementParams {
  itemKind?: 'draggable' | 'area';
  itemFilter?: Record<string, unknown>;
  targetKind?: 'slot' | 'draggable' | 'area';
  targetFilter?: Record<string, unknown>;
  targetIds?: string[];              // explicit receptacles (when they carry no gameplay component)
  match?: Array<[Key, Key]>;
  order?: Key;                       // ascending sort key for items (layer / stage order)
  sequence?: string[];               // explicit program: sequence[i] is placed into the i-th target
  sequenceKey?: Key;                 // item key compared against `sequence`
  placedKeys?: string[];             // item flags meaning "placed"
  dwellEndMs?: number;
  settleMs?: number;
  finalTap?: string;                 // e.g. Execute / Start button once everything is placed
  requiredPlacements?: number;       // stop condition when progress does not track placements
}

export async function matchAndPlace(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as PlacementParams;
  const placedKeys = p.placedKeys ?? ['IsSnapped', 'IsLocked', 'IsInstalled', 'IsConnected', 'IsEquipped', 'IsDeposited'];
  const refuted = new Set<string>();
  const placedItems = new Set<string>();
  const usedTargets = new Set<string>();
  let placements = 0;

  while (!ctx.expired()) {
    const mod = await ctx.module();
    if (mod.completed) return { status: 'COMPLETED', summary: `completed after ${placements} placements` };

    const parts = await ctx.parts();
    const items = parts
      .filter(b => b.kind === (p.itemKind ?? 'draggable') && PlaybookContext.isGameplay(b) && b.visible && b.interactable)
      .filter(b => !isJunk(b) && filterBy(b, p.itemFilter) && !isPlaced(b, placedKeys) && !placedItems.has(b.testId));
    const targets = parts
      .filter(b => p.targetIds ? p.targetIds.includes(b.testId) : b.kind === (p.targetKind ?? 'slot') && PlaybookContext.isGameplay(b))
      .filter(b => b.visible)
      .filter(b => !isJunk(b) && filterBy(b, p.targetFilter) && hasRoom(b, usedTargets) && !isPlaced(b, placedKeys))
      .sort((a, b) => a.testId.localeCompare(b.testId, undefined, { numeric: true }));

    if (items.length === 0 || targets.length === 0) {
      if (p.finalTap) {
        ctx.say(`all placements done → pressing ${p.finalTap}`);
        const h = ctx.hypothesize(`pressing ${p.finalTap} with the program in place completes the mechanic`);
        await ctx.motor.tap({ testId: p.finalTap });
        const st = await ctx.awaitCompleted(Math.min(8000, ctx.timeLeft()));
        ctx.resolve(h, st.completed);
        if (st.completed) return { status: 'COMPLETED', summary: `completed via ${p.finalTap} after ${placements} placements` };
        return { status: 'FAILED', summary: `${p.finalTap} did not complete the mechanic (progress ${st.progress.toFixed(2)})`, stagnationType: 'SOFT_LOCK' };
      }
      // Items may be in a transition (e.g. next stage tray appearing) — wait briefly for new stock.
      const st = await ctx.awaitProgress(mod.progress, 1500);
      if (st.completed) return { status: 'COMPLETED', summary: `completed after ${placements} placements` };
      if (ctx.progressed(false)) return { status: 'STUCK', summary: `no placeable items/targets left, progress ${st.progress.toFixed(2)}`, stagnationType: 'MICRO_STUCK' };
      continue;
    }

    // ── Decide (item, target) ─────────────────────────────────────────────
    let item: Beacon | undefined;
    let target: Beacon | undefined;
    let reason = '';
    if (p.sequence && p.sequenceKey) {
      const index = placements;
      const want = norm(p.sequence[index]);
      target = targets[0];
      item = items.find(i => norm(prop(i, p.sequenceKey!)) === want);
      reason = `program step ${index + 1}: ${p.sequence[index]}`;
    } else {
      const ordered = p.order ? [...items].sort((a, b) => Number(prop(a, p.order!)) - Number(prop(b, p.order!))) : items;
      for (const it of ordered) {
        const cand = p.match
          ? targets.filter(t => matches(it, t, p.match!))
          : targets.filter(t => !refuted.has(`${it.testId}->${t.testId}`));
        const t = cand.find(c => !refuted.has(`${it.testId}->${c.testId}`));
        if (t) { item = it; target = t; reason = p.match ? `rule ${p.match.map(([a, b]) => `${a}=${b}`).join('&')}` : 'trial (no GDD rule)'; break; }
      }
    }
    if (!item || !target) {
      if (ctx.progressed(false)) return { status: 'STUCK', summary: 'no matching (item,target) pair among live beacons', stagnationType: 'DEADLOCK_TOPOLOGY' };
      await sleep(400);
      continue;
    }

    // ── Act ───────────────────────────────────────────────────────────────
    const h = ctx.hypothesize(`drag ${item.testId} → ${target.testId} (${reason}) is accepted`);
    const home: Point = { ...item.center };
    await ctx.motor.drag(item.center, target.center, { dwellEndMs: p.dwellEndMs ?? 120 }, `DRAG ${item.testId}`);
    await sleep(p.settleMs ?? 250);

    // ── Verify by effect ─────────────────────────────────────────────────
    const after = await ctx.world.inspect(item.testId);
    const st = await ctx.awaitProgress(mod.progress, 600);
    const inTarget = after && target.rect ? rectContains(target.rect, after.center, 12) : false;
    const accepted = st.completed || st.progress > mod.progress + 1e-4 || (after ? isPlaced(after, placedKeys) : false)
      || (!!p.sequence && inTarget && (!after || dist(after.center, home) > 20));
    ctx.resolve(h, accepted);
    if (accepted) {
      placements++;
      placedItems.add(item.testId);
      usedTargets.add(target.testId);
      ctx.progressed(true);
      if (p.requiredPlacements && placements >= p.requiredPlacements && !p.finalTap) {
        const done = await ctx.awaitCompleted(3000);
        if (done.completed) return { status: 'COMPLETED', summary: `completed after ${placements} placements` };
      }
    } else {
      refuted.add(`${item.testId}->${target.testId}`);
      if (ctx.progressed(false)) return { status: 'STUCK', summary: `placement of ${item.testId} repeatedly rejected`, stagnationType: 'MICRO_STUCK' };
    }
  }
  return { status: 'TIMEOUT', summary: `timeout after ${placements} placements`, stagnationType: 'TIMEOUT' };
}

/** Balance scale (M03): pick non-junk weights whose masses sum to the reference and place them. */
export async function balanceScale(ctx: PlaybookContext): Promise<PlaybookResult> {
  const parts = await ctx.parts();
  const weights = parts.filter(b => b.kind === 'draggable' && typeof b.game?.Mass === 'number' && !isJunk(b));
  const pans = parts.filter(b => b.kind === 'slot' && ('CurrentMass' in (b.game ?? {}) || 'IsReferencePan' in (b.game ?? {})));
  const reference = pans.find(b => b.game?.IsReferencePan === true);
  const target = pans.find(b => b.game?.IsReferencePan !== true);
  if (!reference || !target) return { status: 'DISCOVERY_REQUIRED', summary: 'scale pans not observable' };
  const goal = Number(reference.game?.CurrentMass ?? ctx.params.referenceMass);
  const masses = weights.map(w => Number(w.game!.Mass));
  // Subset-sum over live masses (n is tiny: exhaustive search).
  let best: number[] | null = null;
  for (let mask = 1; mask < 1 << masses.length; mask++) {
    const idx = masses.map((_, i) => i).filter(i => mask & (1 << i));
    const sum = idx.reduce((a, i) => a + masses[i], 0);
    if (Math.abs(sum - goal) <= 0.5 && (!best || idx.length < best.length)) best = idx;
  }
  if (!best) return { status: 'FAILED', summary: `no subset of ${masses.join(',')} balances ${goal}` };
  ctx.hypothesize(`weights ${best.map(i => `${weights[i].testId}(${masses[i]})`).join(' + ')} = ${goal} balance the scale`);
  for (const i of best) {
    const w = await ctx.world.inspect(weights[i].testId);
    const pan = await ctx.world.inspect(target.testId);
    if (!w || !pan) continue;
    await ctx.motor.drag(w.center, pan.center, { dwellEndMs: 150 }, `DRAG ${w.testId}`);
    await sleep(400);
    const after = await ctx.world.inspect(target.testId);
    ctx.say(`right pan mass now ${after?.game?.CurrentMass}`);
  }
  ctx.say('holding still for the stability window');
  const st = await ctx.awaitCompleted(Math.min(6000, ctx.timeLeft()));
  return st.completed ? { status: 'COMPLETED', summary: `balanced at ${goal}` } : { status: 'STUCK', summary: `scale not completed, progress ${st.progress}`, stagnationType: 'MICRO_STUCK' };
}
