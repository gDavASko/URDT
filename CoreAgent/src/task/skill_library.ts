/**
 * Skill library with structural recognition: reuses the playbooks learned on the polygon WITHOUT a GDD.
 * Each recognizer inspects the observed scene (beacon kinds, roles, game-state keys, entity motion) and, if
 * its signature matches, derives the playbook parameters from what it sees. Reports label these solutions
 * `skill:<name>` so they are never confused with pure exploration.
 */

import { Beacon, WorldSnapshot, sleep } from '../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../l2_tactics/playbook_context.js';
import { PLAYBOOKS } from '../l2_tactics/playbooks/index.js';

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
  if (parts.some(b => has(b, 'ExpectedColorId'))) out.push({ skill: 'paint_learned_palette', params: {}, why: 'segments with a reference colour id', custom: paintWithLearnedPalette });
  const wheel = parts.find(b => has(b, 'AccumulatedAngle') && b.game.IsJammed === false);
  if (wheel) out.push({ skill: 'rotate_wheel', params: { turnsPerGesture: 1.25 }, why: 'free wheel accumulating angle' });
  void motion;
  return out;
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
