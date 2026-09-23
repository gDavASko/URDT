/**
 * Arcade genre skills: closed-loop control (paddle & ball, grid snake) and move search (merge, match-3).
 * Each reads only what the game exposes (beacons + public GameState) and acts with honest pointer input.
 * All of them survive stage transitions and fail restarts: they wait while the board is rebuilt and continue
 * until the module reports completion.
 */

import { Beacon, Point, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';

const num = (v: unknown, d = 0) => (typeof v === 'number' && isFinite(v) ? v : d);

async function moduleState(ctx: PlaybookContext): Promise<{ mod: Beacon | null; done: boolean; transition: boolean }> {
  const mod = await ctx.world.inspect(ctx.moduleId);
  return { mod, done: mod?.props.IsCompleted === true, transition: mod?.game?.IsInTransition === true };
}

/**
 * Paddle & ball (breakout). Board-local state (BallX/Y, BallVX/VY, PaddleY, FieldWidth) gives the ball's
 * flight; the landing x at the paddle line is predicted with wall reflections and the paddle is placed there by
 * pressing the field at that x (the game moves the paddle to the pointer). A resting ball is launched by a tap.
 */
export async function paddleBall(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { field: string; paddle: string; ball: string };
  let taps = 0;
  while (!ctx.expired()) {
    const { mod, done, transition } = await moduleState(ctx);
    if (done) return { status: 'COMPLETED', summary: `all bricks cleared (${taps} paddle commands)` };
    if (!mod || transition) { await sleep(150); continue; }
    const g = mod.game ?? {};
    const field = await ctx.world.inspect(p.field);
    const paddle = await ctx.world.inspect(p.paddle);
    if (!field?.rect || !paddle) { await sleep(100); continue; }
    const W = num(g.FieldWidth, 1100), H = num(g.FieldHeight, 600);
    const sx = field.rect.w / W, sy = field.rect.h / H;
    const toScreenX = (x: number) => field.center.x + x * sx;
    const paddleScreenY = paddle.center.y;
    const pressY = paddleScreenY + Math.max(20, 30 * sy);          // URDT screen space is y-up: just above the paddle
    const bx = num(g.BallX), by = num(g.BallY), vx = num(g.BallVX), vy = num(g.BallVY), py = num(g.PaddleY);
    if (g.BallLaunched !== true) {
      // Aim the launch a little off-centre, then tap to launch.
      await ctx.motor.tap({ point: { x: toScreenX(num(g.PaddleX)), y: pressY } }, 16);
      taps++;
      await sleep(120);
      continue;
    }
    let targetX = bx;
    if (vy < -1) {
      // Falling: predict where it crosses the paddle line, folding reflections off the side walls.
      const t = (by - py) / -vy;
      const half = W / 2 - 14;
      let x = bx + vx * t;
      const span = 2 * half;
      x = ((x + half) % (2 * span) + 2 * span) % (2 * span);
      x = x > span ? 2 * span - x : x;
      const landX = x - half;
      targetX = landX;
      // Aim: the bounce angle grows with the hit offset from the paddle centre (classic breakout, up to ~60°).
      // Pick the nearest intact brick and offset the paddle so the ball leaves toward it.
      const bricks = (await ctx.parts()).filter(b => /^brick_/i.test(b.testId) && b.game?.IsDestroyed === false && b.visible);
      if (bricks.length) {
        const toLocal = (bb: Beacon) => ({ x: (bb.center.x - field.center.x) / sx, y: (bb.center.y - field.center.y) / sy });
        const aimAt = bricks.map(toLocal).sort((a, b) => Math.abs(a.x - landX) - Math.abs(b.x - landX))[0];
        const theta = Math.max(-55, Math.min(55, Math.atan2(aimAt.x - landX, Math.max(40, aimAt.y - py)) * 180 / Math.PI));
        const halfW = num(g.PaddleWidth, 160) / 2;
        // ball hits at offset o from centre → outgoing angle = o/halfW * 60°; so paddle centre = landX - o.
        targetX = landX - (theta / 60) * halfW * 0.9;
      }
    }
    const cur = num(g.PaddleX);
    if (Math.abs(targetX - cur) > 6) {
      await ctx.motor.tap({ point: { x: toScreenX(targetX), y: pressY } }, 16);
      taps++;
    }
    await sleep(25);
  }
  return { status: 'TIMEOUT', summary: `${taps} paddle commands`, stagnationType: 'TIMEOUT' };
}

/**
 * Grid snake. Each time the head enters a new cell: breadth-first path from the head to the food avoiding the
 * body (the tail cell frees up as the snake moves), falling back to the move with the most free space; the
 * matching direction button is pressed when the direction must change.
 */
export async function gridSnake(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { up: string; down: string; left: string; right: string };
  const btn: Record<string, string> = { up: p.up, down: p.down, left: p.left, right: p.right };
  const D: Record<string, [number, number]> = { up: [0, -1], down: [0, 1], left: [-1, 0], right: [1, 0] };
  const opposite: Record<string, string> = { up: 'down', down: 'up', left: 'right', right: 'left' };
  let lastHead = '', presses = 0, started = false;
  while (!ctx.expired()) {
    const { mod, done, transition } = await moduleState(ctx);
    if (done) return { status: 'COMPLETED', summary: `food collected (${presses} turns)` };
    if (!mod || transition) { await sleep(100); lastHead = ''; started = false; continue; }
    const g = mod.game ?? {};
    const cols = num(g.GridCols), rows = num(g.GridRows);
    const hc = num(g.HeadCol), hr = num(g.HeadRow), fc = num(g.FoodCol), fr = num(g.FoodRow);
    const head = `${hc},${hr}`;
    if (head === lastHead && started) { await sleep(15); continue; }
    lastHead = head;
    const body = String(g.BodyCells ?? '').split(';').filter(Boolean);
    const blocked = new Set(body.slice(0, Math.max(0, body.length - 1)));   // tail moves away this tick
    const free = (c: number, r: number) => c >= 0 && r >= 0 && c < cols && r < rows && !blocked.has(`${c},${r}`);
    const dir = String(g.Direction ?? 'right');
    // BFS from head to food.
    const prev = new Map<string, [string, string]>();
    const q: Array<[number, number]> = [];
    for (const [d, [dx, dy]] of Object.entries(D)) {
      if (started && d === opposite[dir]) continue;
      const c = hc + dx, r = hr + dy;
      if (free(c, r) && !prev.has(`${c},${r}`)) { prev.set(`${c},${r}`, [d, head]); q.push([c, r]); }
    }
    let first: string | null = null;
    while (q.length) {
      const [c, r] = q.shift()!;
      if (c === fc && r === fr) { first = prev.get(`${c},${r}`)![0]; break; }   // entries carry their first step
      for (const [dx, dy] of Object.values(D)) {
        const k = `${c + dx},${r + dy}`;
        if (free(c + dx, r + dy) && !prev.has(k) && k !== head) { prev.set(k, [prev.get(`${c},${r}`)![0], `${c},${r}`]); q.push([c + dx, r + dy]); }
      }
    }
    if (!first) {
      // No path: take the move with the largest reachable area.
      let best = -1;
      for (const [d, [dx, dy]] of Object.entries(D)) {
        if (started && d === opposite[dir]) continue;
        if (!free(hc + dx, hr + dy)) continue;
        const seen = new Set<string>([`${hc + dx},${hr + dy}`]);
        const st: Array<[number, number]> = [[hc + dx, hr + dy]];
        while (st.length && seen.size < 200) {
          const [c, r] = st.pop()!;
          for (const [ex, ey] of Object.values(D)) { const k = `${c + ex},${r + ey}`; if (free(c + ex, r + ey) && !seen.has(k)) { seen.add(k); st.push([c + ex, r + ey]); } }
        }
        if (seen.size > best) { best = seen.size; first = d; }
      }
    }
    if (first && (first !== dir || !started)) {
      await ctx.motor.tap({ testId: btn[first] }, 16);
      presses++;
      started = true;
    }
    await sleep(15);
  }
  return { status: 'TIMEOUT', summary: `${presses} turns`, stagnationType: 'TIMEOUT' };
}

/**
 * Merge board: merge the highest pair of equal-level items; when no pair exists, create a new item. Merging
 * into the item that is further from empty space keeps the board compact.
 */
export async function mergeBoard(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { spawn: string; itemPrefix: string };
  let moves = 0;
  while (!ctx.expired()) {
    const { mod, done, transition } = await moduleState(ctx);
    if (done) return { status: 'COMPLETED', summary: `target level reached in ${moves} moves` };
    if (!mod || transition) { await sleep(150); continue; }
    const parts = await ctx.parts();
    const items = parts.filter(b => b.testId.startsWith(p.itemPrefix) && b.visible && typeof b.game?.Level === 'number');
    const byLevel = new Map<number, Beacon[]>();
    for (const it of items) byLevel.set(it.game.Level, [...(byLevel.get(it.game.Level) ?? []), it]);
    const pairLevel = [...byLevel.entries()].filter(([, v]) => v.length >= 2).map(([l]) => l).sort((a, b) => b - a)[0];
    if (pairLevel !== undefined) {
      const [a, b] = byLevel.get(pairLevel)!;
      await ctx.motor.drag(a.center, b.center, { dwellEndMs: 60, durationMs: 220 }, `MERGE L${pairLevel}`);
    } else if (num(mod.game?.SpawnsLeft, 1) > 0) {
      await ctx.motor.tap({ testId: p.spawn }, 16);
    } else {
      return { status: 'STUCK', summary: 'no pair and no spawns left', stagnationType: 'MICRO_STUCK' };
    }
    moves++;
    await sleep(180);
  }
  return { status: 'TIMEOUT', summary: `${moves} moves`, stagnationType: 'TIMEOUT' };
}

/**
 * Match-3: read the board colours, try every adjacent swap in a local copy, pick the one that clears the most
 * gems (ties: lower on the board, which tends to cascade), and perform it as a directional drag.
 */
export async function match3(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { gemPrefix: string };
  let moves = 0;
  while (!ctx.expired()) {
    const { mod, done, transition } = await moduleState(ctx);
    if (done) return { status: 'COMPLETED', summary: `combos reached in ${moves} moves` };
    if (!mod || transition) { await sleep(150); continue; }
    const parts = await ctx.parts();
    const gems = parts.filter(b => b.testId.startsWith(p.gemPrefix) && typeof b.game?.ColorId === 'number');
    if (!gems.length) { await sleep(150); continue; }
    const R = Math.max(...gems.map(g => g.game.Row)) + 1, C = Math.max(...gems.map(g => g.game.Col)) + 1;
    const grid: number[][] = Array.from({ length: R }, () => Array(C).fill(-1));
    const at = new Map<string, Beacon>();
    for (const g of gems) { grid[g.game.Row][g.game.Col] = g.game.ColorId; at.set(`${g.game.Row},${g.game.Col}`, g); }
    const cleared = (b: number[][]) => {
      let n = 0;
      for (let r = 0; r < R; r++) for (let c = 0; c < C; c++) {
        const v = b[r][c];
        if (v < 0) continue;
        const h = c + 2 < C && b[r][c + 1] === v && b[r][c + 2] === v;
        const vv = r + 2 < R && b[r + 1][c] === v && b[r + 2][c] === v;
        n += (h ? 3 : 0) + (vv ? 3 : 0);
      }
      return n;
    };
    let best: { r: number; c: number; dr: number; dc: number; score: number } | null = null;
    for (let r = 0; r < R; r++) for (let c = 0; c < C; c++) for (const [dr, dc] of [[0, 1], [1, 0]]) {
      const r2 = r + dr, c2 = c + dc;
      if (r2 >= R || c2 >= C) continue;
      const b = grid.map(row => [...row]);
      [b[r][c], b[r2][c2]] = [b[r2][c2], b[r][c]];
      const s = cleared(b);
      if (s > 0 && (!best || s > best.score || (s === best.score && r > best.r))) best = { r, c, dr, dc, score: s };
    }
    if (!best) { await sleep(400); continue; }   // the game reshuffles when no move exists
    const g1 = at.get(`${best.r},${best.c}`)!, g2 = at.get(`${best.r + best.dr},${best.c + best.dc}`)!;
    const dx = g2.center.x - g1.center.x, dy = g2.center.y - g1.center.y;
    const len = Math.hypot(dx, dy) || 1;
    const to: Point = { x: g1.center.x + dx / len * Math.max(60, len), y: g1.center.y + dy / len * Math.max(60, len) };
    const movesBefore = num(mod.game?.MovesLeft, 0);
    await ctx.motor.drag(g1.center, to, { dwellEndMs: 40, durationMs: 200 }, `SWAP ${g1.testId}→${g2.testId}`);
    moves++;
    // Wait for the cascade to settle (moves counter changed and the board stopped changing).
    let last = '';
    for (let i = 0; i < 30; i++) {
      await sleep(150);
      const m2 = await ctx.world.inspect(ctx.moduleId);
      const colors = String(m2?.game?.BoardColors ?? '');
      if (num(m2?.game?.MovesLeft, movesBefore) !== movesBefore && colors === last) break;
      last = colors;
    }
  }
  return { status: 'TIMEOUT', summary: `${moves} moves`, stagnationType: 'TIMEOUT' };
}
