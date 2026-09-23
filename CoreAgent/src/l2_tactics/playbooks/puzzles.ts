/**
 * Discrete puzzle playbooks: search over observable state (pipes, map colouring, grid navigation).
 */

import { Beacon, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';

/**
 * M18: tile orientations are hidden; only water propagation is observable. Depth-first search along the
 * flow chain with backtracking: the chain head tries orientations that keep it wet, each unvisited
 * neighbour tries its 4 orientations, and a branch is kept only when water is observed to reach it.
 * Four clicks restore a tile, so every failed branch is undone exactly.
 */
export async function pipePuzzle(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tilePrefix: string; sinkType?: string };
  const sinkType = p.sinkType ?? 'Sink';
  let clicks = 0;
  const tiles = async () => (await ctx.parts()).filter(b => b.testId.startsWith(p.tilePrefix) && 'HasWater' in (b.game ?? {}));
  const all = await tiles();
  const key = (t: Beacon) => `${t.game.GridX},${t.game.GridY}`;
  const byKey = new Map(all.map(t => [key(t), t]));
  const rotatable = (t: Beacon) => t.game.Type === 'Straight' || t.game.Type === 'Corner';
  const neighbours = (t: Beacon) => [[1, 0], [-1, 0], [0, 1], [0, -1]]
    .map(([dx, dy]) => byKey.get(`${Number(t.game.GridX) + dx},${Number(t.game.GridY) + dy}`))
    .filter((n): n is Beacon => !!n && n.game.Type !== 'BrokenJunk');
  const wet = async (): Promise<Set<string>> => new Set((await tiles()).filter(t => t.game.HasWater === true).map(key));
  const done = async () => (await ctx.module()).completed || (await tiles()).some(t => t.game.Type === sinkType && t.game.HasWater === true);
  const rotate = async (t: Beacon) => { await ctx.motor.tap({ testId: t.testId }, 16); clicks++; await sleep(40); };

  const source = all.find(t => t.game.Type === 'Source');
  if (!source) return { status: 'DISCOVERY_REQUIRED', summary: 'source tile not observable' };
  ctx.hypothesize(`DFS over the flow chain from ${source.testId}; a branch survives only if water is observed to reach it`);

  const dfs = async (path: Beacon[], depth: number): Promise<boolean> => {
    if (ctx.expired() || depth > 12) return false;
    if (await done()) return true;
    const head = path[path.length - 1];
    const headTurns = rotatable(head) && path.length > 1 ? 4 : 1;
    for (let hr = 0; hr < headTurns; hr++) {
      if (hr > 0) { await rotate(head); if (!(await wet()).has(key(head))) continue; }
      for (const n of neighbours(head)) {
        if (path.some(q => key(q) === key(n))) continue;
        if (n.game.Type === sinkType) { if (await done()) return true; continue; }
        if (!rotatable(n)) continue;
        for (let r = 0; r < 4; r++) {
          if ((await wet()).has(key(n))) {
            if (await dfs([...path, n], depth + 1)) return true;
          }
          await rotate(n);            // after the 4th click the tile is back in its original orientation
        }
      }
    }
    if (headTurns === 4) await rotate(head); // 3 trial clicks + 1 → head back in its original orientation
    return false;
  };

  const solved = await dfs([source], 0);
  if (solved || await done()) return { status: 'COMPLETED', summary: `circuit closed after ${clicks} rotations` };
  return { status: 'STUCK', summary: `no flow chain to the sink found (${clicks} rotations)`, stagnationType: 'DEADLOCK_TOPOLOGY' };
}

/** M19: paint every segment with the colour its reference (etalon) requires. */
export async function paintByKey(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { segmentPrefix: string; palette: Record<string, string> };
  for (let round = 0; round < 3 && !ctx.expired(); round++) {
    const segs = (await ctx.parts()).filter(b => b.testId.startsWith(p.segmentPrefix) && 'ExpectedColorId' in (b.game ?? {}) && b.game.IsCorrect !== true);
    if ((await ctx.module()).completed) return { status: 'COMPLETED', summary: 'map coloured per etalon' };
    if (segs.length === 0) break;
    for (const s of segs) {
      const swatch = p.palette[String(s.game.ExpectedColorId)];
      if (!swatch) return { status: 'FAILED', summary: `GDD palette lacks colour id ${s.game.ExpectedColorId}` };
      const h = ctx.hypothesize(`select ${swatch} then paint ${s.testId} (etalon colour ${s.game.ExpectedColorId})`);
      await ctx.motor.tap({ testId: swatch });
      await ctx.motor.tap({ testId: s.testId });
      await sleep(80);
      ctx.resolve(h, (await ctx.world.inspect(s.testId))?.game?.IsCorrect === true);
    }
  }
  const st = await ctx.awaitCompleted(1500);
  return st.completed ? { status: 'COMPLETED', summary: 'map coloured per etalon' } : { status: 'STUCK', summary: 'colouring not accepted', stagnationType: 'MICRO_STUCK' };
}

/** M22: BFS over the observed tile grid, then walk the path cell by cell. */
export async function gridPath(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { tilePrefix: string; avatar: string; blocked: string[]; avoid?: string[]; goalType: string };
  let stepsDone = 0;
  for (let replan = 0; replan < 40 && !ctx.expired(); replan++) {
    const modB = await ctx.world.inspect(ctx.moduleId);
    if (modB?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `goal reached (${stepsDone} steps over all stages)` };
    if (modB?.game?.IsInTransition === true) { await sleep(200); continue; }   // stage change / fail restart
    const parts = await ctx.parts();
    const tiles = parts.filter(b => b.testId.startsWith(p.tilePrefix) && 'CellType' in (b.game ?? {}));
    const avatar = parts.find(b => b.testId === p.avatar || b.props.AreaType === p.avatar);
    if (!avatar || tiles.length === 0) { await sleep(200); continue; }
    const key = (x: number, y: number) => `${x},${y}`;
    const cell = new Map(tiles.map(t => [key(Number(t.game.CellX), Number(t.game.CellY)), t]));
    const here = tiles.reduce((a, b) => (Math.hypot(a.center.x - avatar.center.x, a.center.y - avatar.center.y) < Math.hypot(b.center.x - avatar.center.x, b.center.y - avatar.center.y) ? a : b));
    const goal = tiles.find(t => t.game.CellType === p.goalType);
    if (!goal) return { status: 'DISCOVERY_REQUIRED', summary: 'goal cell not observable' };
    const blocked = (t: Beacon, soft: boolean) => p.blocked.includes(String(t.game.CellType)) || (soft && (p.avoid ?? []).includes(String(t.game.CellType)));
    const bfs = (soft: boolean): Beacon[] | null => {
      const start = key(Number(here.game.CellX), Number(here.game.CellY));
      const prev = new Map<string, string>([[start, '']]);
      const q = [start];
      while (q.length) {
        const cur = q.shift()!;
        if (cur === key(Number(goal.game.CellX), Number(goal.game.CellY))) {
          const path: Beacon[] = [];
          for (let k = cur; k !== start; k = prev.get(k)!) path.unshift(cell.get(k)!);
          return path;
        }
        const [cx, cy] = cur.split(',').map(Number);
        for (const [dx, dy] of [[1, 0], [-1, 0], [0, 1], [0, -1]]) {
          const nk = key(cx + dx, cy + dy);
          const n = cell.get(nk);
          if (n && !prev.has(nk) && !blocked(n, soft)) { prev.set(nk, cur); q.push(nk); }
        }
      }
      return null;
    };
    // Avoid traps when the step budget allows the detour; otherwise take the shortest path through them.
    const remaining = Number(modB?.game?.StepsRemaining ?? Infinity);
    const safe = bfs(true), any = bfs(false);
    const path = safe && safe.length <= remaining ? safe : any ?? safe;
    if (!path) return { status: 'FAILED', summary: 'no path to goal exists in the observed grid', stagnationType: 'DEADLOCK_TOPOLOGY' };
    ctx.hypothesize(`BFS path of ${path.length} steps (budget ${remaining}): ${path.map(t => `(${t.game.CellX},${t.game.CellY})`).join('→')}`);
    const stageAtStart = Number(modB?.game?.CurrentStage ?? 1), failsAtStart = Number(modB?.game?.FailCount ?? 0);
    for (const step of path) {
      const before = Number((await ctx.world.inspect(ctx.moduleId))?.game?.StepsTaken ?? 0);
      await ctx.motor.tap({ testId: step.testId });
      // Wait for the move to register (animation, 1 s trap freeze) — re-tapping would spend another step.
      let moved = false;
      for (let w = 0; w < 14 && !moved; w++) {
        await sleep(120);
        const m = await ctx.world.inspect(ctx.moduleId);
        if (Number(m?.game?.StepsTaken ?? 0) > before || m?.props.IsCompleted === true || m?.game?.IsInTransition === true) moved = true;
      }
      if (!moved) break;                          // the move was refused: replan from the observed position
      stepsDone++;
      const m = await ctx.world.inspect(ctx.moduleId);
      if (m?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `goal reached (${stepsDone} steps over all stages)` };
      if (m?.game?.IsInTransition === true || Number(m?.game?.CurrentStage ?? 1) !== stageAtStart || Number(m?.game?.FailCount ?? 0) !== failsAtStart) break;
      // Wait out a trap freeze before the next tap.
      for (let w = 0; w < 12; w++) {
        const av = await ctx.world.inspect(avatar.testId);
        if (av && Math.hypot(av.center.x - step.center.x, av.center.y - step.center.y) < 20) break;
        await sleep(120);
      }
    }
    await sleep(250);
  }
  const done = (await ctx.world.inspect(ctx.moduleId))?.props.IsCompleted === true;
  return done ? { status: 'COMPLETED', summary: `goal reached (${stepsDone} steps)` } : { status: 'STUCK', summary: 'goal not reached', stagnationType: 'MICRO_STUCK' };
}
