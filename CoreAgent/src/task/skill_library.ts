/**
 * Skill library with structural recognition: reuses the playbooks learned on the polygon WITHOUT a GDD.
 * Each recognizer inspects the observed scene (beacon kinds, roles, game-state keys, entity motion) and, if
 * its signature matches, derives the playbook parameters from what it sees. Reports label these solutions
 * `skill:<name>` so they are never confused with pure exploration.
 */

import { Beacon, WorldSnapshot, sleep } from '../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../l2_tactics/playbook_context.js';
import { PLAYBOOKS } from '../l2_tactics/playbooks/index.js';
import { paddleBall, gridSnake, mergeBoard, match3 } from '../l2_tactics/playbooks/arcade.js';

/** Built-in skills registered as core in the knowledge store (their applicability lives in recognizeSkills). */
export const BUILTIN_SKILLS: Array<{ name: string; description: string; requires: { scopeGame?: string[]; beacons?: string[]; buttons?: string[] } }> = [
  ['glider', 'avatar with collectibles scrolling horizontally'], ['lane_runner', 'lane runner (swipe / tap halves / drag)'],
  ['slingshot', 'pull-and-release projectile with fitted ballistics'], ['timing_intercept', 'press when a mover crosses a line'],
  ['stack_drop', 'drop a swinging block over a tower'], ['reaction_strike', 'react to a cue within a window'],
  ['car_drive', 'throttle/brake physics car'], ['pipe_puzzle', 'rotate tiles to connect flow'], ['grid_path', 'move an avatar across a grid'],
  ['balance_scale', 'balance weights'], ['lens_dwell', 'hover a lens over hidden targets'], ['fill_wells', 'fill wells in order with a dispenser'],
  ['wobble_extract', 'wobble then extract'], ['spray_targets', 'spray targets while avoiding hazards'], ['pop_targets', 'pop spawned targets'],
  ['paint_learned_palette', 'learn a palette and paint by reference'], ['rotate_wheel', 'rotate a wheel by degrees'],
  ['gauge_band', 'hold and release a gauge inside a band'], ['paddle_ball', 'breakout paddle with aiming'],
  ['grid_snake', 'grid snake path-finding'], ['merge_board', 'merge equal-level items'], ['match3', 'swap adjacent gems to match 3+'],
].map(([name, description]) => ({ name, description, requires: {} }));

export interface SkillMatch { skill: string; params: Record<string, any>; why: string; custom?: (ctx: PlaybookContext) => Promise<PlaybookResult> }

const role = (b: Beacon) => String(b.props.AreaType ?? '');
const has = (b: Beacon, key: string) => Object.prototype.hasOwnProperty.call(b.game ?? {}, key);
const any = (parts: Beacon[], re: RegExp) => parts.find(b => re.test(b.testId) || re.test(role(b)));
const all = (parts: Beacon[], re: RegExp) => parts.filter(b => re.test(b.testId) || re.test(role(b)));

export function recognizeSkills(scope: Beacon, parts: Beacon[], motion: Map<string, { dx: number; dy: number }>): SkillMatch[] {
  const out: SkillMatch[] = [];
  const buttons = parts.filter(b => b.kind === 'button');
  const spawned = parts.filter(b => role(b).startsWith('spawned:'));
  const avatar = any(parts, /avatar|player/i);

  // Moving collectibles vs. a steerable avatar: lanes (things fall, avatar moves sideways) or glider.
  const plane = any(parts, /airplane|plane/i);
  const stars = all(parts, /star/i).filter(b => b.kind === 'area');
  if (plane && stars.length) {
    const container = parts.filter(b => b.kind === 'slot' && b.rect && plane.rect && b.rect.w > 300).sort((a, b) => (a.rect!.w * a.rect!.h) - (b.rect!.w * b.rect!.h))[0];
    if (container) out.push({ skill: 'glider', params: { plane: plane.testId, starRole: role(stars[0]), container: container.testId }, why: 'avatar with collectibles scrolling horizontally' });
  }
  if (avatar && spawned.length) {
    const families = [...new Set(spawned.map(role))];
    const coin = families.find(f => /coin|star|gem|bonus|fruit/i.test(f));
    const hazard = families.find(f => /barrier|obstacle|bomb|spike|rock/i.test(f));
    if (coin) {
      const mode = String(scope.game?.ControlMode ?? '');
      const control = /tap/i.test(mode) ? 'tap_halves' : /drag/i.test(mode) ? 'direct_drag' : 'swipe';
      out.push({ skill: 'lane_runner', params: { control, avatar: avatar.testId, coinRole: coin, hazardRole: hazard ?? '__none__', dangerPx: 170 }, why: `avatar + spawned ${families.join(', ')}; control from GameState.ControlMode="${mode || 'unknown'}"` });
    }
  }

  const ball = any(parts, /projectile|ball/i);
  const anchor = any(parts, /anchor|sling/i);
  const cans = parts.filter(b => has(b, 'IsHit'));
  if (ball && anchor && cans.length) {
    const prefix = cans[0].testId.replace(/\d+$/, '');
    out.push({ skill: 'slingshot', params: { ball: ball.testId, anchor: anchor.testId, targetPrefix: prefix, obstacleRole: role(any(parts, /pillar|obstacle/i) ?? ball) || '__none__', maxPullPx: 132 }, why: 'projectile + anchor + hittable targets' });
  }

  const line = any(parts, /line|intercept/i);
  if (ball && line && buttons.length === 1) out.push({ skill: 'timing_intercept', params: { ball: ball.testId, line: line.testId, button: buttons[0].testId, tolerancePx: 45 }, why: 'moving ball, intercept line, one action button' });

  const block = any(parts, /block/i);
  const base = any(parts, /base|platform/i);
  if (block && base && buttons.length) out.push({ skill: 'stack_drop', params: { block: block.testId, button: buttons.find(b => /drop/i.test(b.testId))?.testId ?? buttons[0].testId, base: base.testId, tolerancePx: 52, blocks: 3 }, why: 'swinging block over a base with a release button' });

  const bobber = any(parts, /bobber|float/i);
  if (bobber && buttons.length) out.push({ skill: 'reaction_strike', params: { bobber: bobber.testId, button: buttons.find(b => /strike|pull|hook/i.test(b.testId))?.testId ?? buttons[0].testId, dipPx: 22, catches: 2 }, why: 'bobber that dips on a bite + strike button' });

  const gas = buttons.find(b => /gas|throttle|accel/i.test(b.testId));
  const brake = buttons.find(b => /brake/i.test(b.testId));
  if (gas && brake) {
    const body = any(parts, /car|chassis|vehicle/i);
    const dtext = parts.find(b => b.kind === 'text' && /distance/i.test(b.testId));
    out.push({ skill: 'car_drive', params: { gas: gas.testId, brake: brake.testId, body: body?.testId ?? scope.testId, distanceText: dtext?.testId ?? '', pitchLimitDeg: 25 }, why: 'throttle and brake pedals' });
  }

  if (parts.some(b => has(b, 'HasWater') && has(b, 'Type'))) {
    const t = parts.find(b => has(b, 'HasWater'))!;
    out.push({ skill: 'pipe_puzzle', params: { tilePrefix: t.testId.replace(/\d+_\d+$/, '') }, why: 'tiles exposing water flow' });
  }
  if (parts.some(b => has(b, 'CellType'))) {
    const t = parts.find(b => has(b, 'CellType'))!;
    const types = [...new Set(parts.filter(b => has(b, 'CellType')).map(b => String(b.game.CellType)))];
    const robot = any(parts, /robot|avatar|player/i);
    if (robot) out.push({ skill: 'grid_path', params: { tilePrefix: t.testId.replace(/\d+_\d+$/, ''), avatar: robot.testId, blocked: types.filter(x => /wall|obstacle|block/i.test(x)), avoid: types.filter(x => /trap|hazard|lava/i.test(x)), goalType: types.find(x => /goal|exit|finish/i.test(x)) ?? 'Goal' }, why: `grid of cells ${types.join('/')}` });
  }
  if (parts.some(b => has(b, 'Mass')) && parts.some(b => has(b, 'IsReferencePan'))) out.push({ skill: 'balance_scale', params: {}, why: 'weights with mass and a reference pan' });
  if (parts.some(b => has(b, 'IsJunkDust'))) {
    const lens = any(parts, /lens/i);
    if (lens) out.push({ skill: 'lens_dwell', params: { lens: lens.testId, dwellMs: 2500 }, why: 'hidden targets revealed by a lens' });
  }
  const wells = parts.filter(b => /^well_\d+$/i.test(b.testId));
  const tool = any(parts, /dispenser|nozzle.*tool|tool/i);
  if (wells.length >= 2 && tool) out.push({ skill: 'fill_wells', params: { tool: tool.testId, wellPrefix: wells[0].testId.replace(/\d+$/, ''), percentPrefix: 'PercentText', holdMs: 2400 }, why: 'ordered wells + dispenser' });
  if (parts.some(b => has(b, 'Fatigue'))) {
    const item = parts.find(b => has(b, 'Fatigue'))!;
    const forceps = parts.find(b => b !== item && b.kind === 'draggable' && has(b, 'IsAttached'));
    if (forceps) out.push({ skill: 'wobble_extract', params: { tool: forceps.testId, item: item.testId, amplitudePx: 90 }, why: 'fatigue-based extraction with an attachable tool' });
  }
  const fires = parts.filter(b => has(b, 'IsExtinguished'));
  if (fires.length) out.push({ skill: 'spray_targets', params: { targetRole: role(fires[0]) }, why: 'targets with hit points that can be extinguished' });
  const bubbles = parts.filter(b => has(b, 'IsPopped'));
  if (bubbles.length) out.push({ skill: 'pop_targets', params: { role: role(bubbles[0]) }, why: 'poppable targets (bombs flagged)' });
  // Arcade genres, recognised from the state the game exposes.
  const sg = scope.game ?? {};
  const field = parts.find(b => /^field$/i.test(b.testId));
  const paddle = any(parts, /paddle/i), ballB = any(parts, /^ball$|ball$/i);
  if (paddle && ballB && field && has(scope, 'BallVX') && has(scope, 'PaddleX')) {
    out.push({ skill: 'paddle_ball', params: { field: field.testId, paddle: paddle.testId, ball: ballB.testId }, why: 'paddle + ball with exposed flight state', custom: paddleBall });
  }
  const dirBtn = (re: RegExp) => buttons.find(b => re.test(b.testId))?.testId;
  if (has(scope, 'HeadCol') && has(scope, 'FoodCol') && has(scope, 'BodyCells')) {
    const up = dirBtn(/up$/i), down = dirBtn(/down$/i), left = dirBtn(/left$/i), right = dirBtn(/right$/i);
    if (up && down && left && right) out.push({ skill: 'grid_snake', params: { up, down, left, right }, why: 'grid snake with head/food/body state and direction buttons', custom: gridSnake });
  }
  const leveled = parts.filter(b => typeof b.game?.Level === 'number' && b.kind === 'draggable');
  const spawn = buttons.find(b => /spawn|create|new|add/i.test(b.testId));
  // The board may start empty (items appear only after "create"): cells + a create button + a level goal suffice.
  const cells = parts.filter(b => /^cell_\d+_\d+$/i.test(b.testId));
  if (spawn && (leveled.length || (cells.length >= 4 && (has(scope, 'TargetLevel') || has(scope, 'MaxLevel'))))) {
    const prefix = leveled.length ? leveled[0].testId.replace(/\d+$/, '') : 'MergeItem_';
    out.push({ skill: 'merge_board', params: { spawn: spawn.testId, itemPrefix: prefix }, why: 'levelled items + spawn button (merge)', custom: mergeBoard });
  }
  const gems = parts.filter(b => typeof b.game?.ColorId === 'number' && typeof b.game?.Row === 'number' && typeof b.game?.Col === 'number');
  if (gems.length >= 9) {
    const prefix = gems[0].testId.replace(/\d+_\d+$/, '');
    out.push({ skill: 'match3', params: { gemPrefix: prefix }, why: `${gems.length} coloured cells on a grid (swap to match)`, custom: match3 });
  }
  void sg;

  // Gauge: the game exposes a live value and a target band (pump, charge, pressure) — hold and release inside it.
  const gaugeHost = [scope, ...parts].find(b => has(b, 'CurrentValue') && (has(b, 'TargetMin') || has(b, 'BandMin')) && (has(b, 'TargetMax') || has(b, 'BandMax')));
  if (gaugeHost && buttons.length) {
    const btn = buttons.find(b => /pump|hold|charge|press|gas|fill/i.test(b.testId)) ?? buttons[0];
    out.push({ skill: 'gauge_band', params: { host: gaugeHost.testId, button: btn.testId }, why: `live gauge ${gaugeHost.testId}.CurrentValue with a target band`, custom: gaugeBand });
  }
  if (parts.some(b => has(b, 'ExpectedColorId'))) out.push({ skill: 'paint_learned_palette', params: {}, why: 'segments with a reference colour id', custom: paintWithLearnedPalette });
  const wheel = parts.find(b => has(b, 'AccumulatedAngle') && b.game.IsJammed === false);
  if (wheel) out.push({ skill: 'rotate_wheel', params: { turnsPerGesture: 1.25 }, why: 'free wheel accumulating angle' });
  void motion;
  return out;
}

/**
 * Hold a control while a live gauge rises and release inside the target band. The release lead (reaction +
 * transport latency, in gauge units) is learned from each attempt: overshoot → release earlier, undershoot →
 * later. Works across stages (band and speed may change) and after fail restarts.
 */
async function gaugeBand(ctx: PlaybookContext): Promise<PlaybookResult> {
  const p = ctx.params as { host: string; button: string };
  let lead = 0.02;
  for (let attempt = 1; attempt <= 30 && !ctx.expired(); attempt++) {
    const mod = await ctx.world.inspect(ctx.moduleId);
    if (mod?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `gauge released in band (${attempt - 1} attempts)` };
    if (mod?.game?.IsInTransition === true) { await sleep(300); attempt--; continue; }
    const host = await ctx.world.inspect(p.host);
    const btn = await ctx.world.inspect(p.button);
    if (!host || !btn) return { status: 'DISCOVERY_REQUIRED', summary: 'gauge or control not observable' };
    const g = host.game ?? {};
    const lo = Number(g.TargetMin ?? g.BandMin), hi = Number(g.TargetMax ?? g.BandMax);
    const scale = hi > 1.5 ? 100 : 1;                                   // percent or 0..1
    // Wait for the gauge to drain so each attempt starts low.
    await ctx.world.waitFor(p.host, b => Number(b.game?.CurrentValue ?? 0) < lo * 0.5, 6000, 100);
    const aim = (lo + (hi - lo) * 0.5) - lead * scale;
    const stageBefore = Number(mod?.game?.CurrentStage ?? 1), failsBefore = Number(mod?.game?.FailCount ?? 0);
    let peak = 0;
    await ctx.motor.hold(btn.center, { maxMs: 12000, pollMs: 15, until: async () => {
      const v = Number((await ctx.world.inspect(p.host))?.game?.CurrentValue ?? 0);
      peak = Math.max(peak, v);
      return v >= aim;
    } });
    await sleep(400);
    const after = await ctx.world.inspect(ctx.moduleId);
    const settled = Number((await ctx.world.inspect(p.host))?.game?.CurrentValue ?? peak);
    const fails = Number(after?.game?.FailCount ?? 0) > failsBefore;
    const advanced = after?.props.IsCompleted === true || Number(after?.game?.CurrentStage ?? 1) > stageBefore;
    const landed = Math.max(settled, peak);
    ctx.say(`band [${lo}, ${hi}] aim ${aim.toFixed(3)} → landed ≈${landed.toFixed(3)}${fails ? ' (FAIL)' : ''}${advanced ? ' (stage/level cleared)' : ''}`);
    if (after?.props.IsCompleted === true) return { status: 'COMPLETED', summary: `gauge released in band on attempt ${attempt}` };
    // Overshoot → positive error → release earlier; undershoot → negative error → release later.
    if (!advanced && (landed > hi || landed < lo)) lead += ((landed - (lo + hi) / 2) / scale) * 0.8;
  }
  return { status: 'STUCK', summary: 'could not release inside the band', stagnationType: 'MICRO_STUCK' };
}

/** Palette mapping is not given: learn it by trying each swatch on a segment and reading CurrentColorId. */
async function paintWithLearnedPalette(ctx: PlaybookContext): Promise<PlaybookResult> {
  const parts = await ctx.parts();
  const segs = parts.filter(b => 'ExpectedColorId' in (b.game ?? {}));
  const swatches = parts.filter(b => b.kind === 'button' && !/junk|mud/i.test(b.testId));
  const palette: Record<string, string> = {};
  const probe = segs[0];
  for (const sw of swatches) {
    await ctx.motor.tap({ testId: sw.testId });
    await ctx.motor.tap({ testId: probe.testId });
    await sleep(60);
    const id = (await ctx.world.inspect(probe.testId))?.game?.CurrentColorId;
    if (id !== undefined && id !== 0 && !(String(id) in palette)) palette[String(id)] = sw.testId;
  }
  ctx.say(`learned palette ${JSON.stringify(palette)}`);
  (ctx.scenario as any).params = { segmentPrefix: probe.testId.replace(/\d+$/, ''), palette };
  return PLAYBOOKS.paint_by_key(ctx);
}

/** Tracks entity motion between two snapshots (for recognizers and reports). */
export function motionBetween(a: WorldSnapshot, b: WorldSnapshot): Map<string, { dx: number; dy: number }> {
  const m = new Map<string, { dx: number; dy: number }>();
  for (const x of b.beacons) {
    const y = a.get(x.testId);
    if (y) m.set(x.testId, { dx: x.center.x - y.center.x, dy: x.center.y - y.center.y });
  }
  return m;
}
