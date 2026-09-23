/**
 * Universal explorer — plays an unknown mechanic without a playbook.
 *
 * 1. Action space is generated from the live beacons in scope: taps, item→receptacle drags, dwell drags,
 *    holds of increasing duration, traces through ordered families (Waypoint_0..n), serpentine scrubs over
 *    cell families, circular rotations, alternating taps, mashing.
 * 2. Every action is scored by its observed effect (reward): success predicates, module progress, "positive"
 *    state flags (snapped/connected/collected/…), counters in texts ("3 / 5"), minus penalties.
 * 3. Rules are induced from successes: after a rewarded drag the explorer finds which attributes of item and
 *    receptacle were equal and ranks all remaining pairs that satisfy the same relation first. Rewarded taps
 *    are repeated; rewarded families are exhausted.
 * 4. Tiers escalate only when cheaper tiers stall; everything learned is reported as skills.
 *
 * Generic priors used (and nothing else): a small lexicon of positive/negative state words, junk/hazard words,
 * navigation words to avoid, geometric structure of beacons, and numeric suffix ordering.
 */

import { Beacon, Point, WorldModel, WorldSnapshot, dist, rectContains, sleep } from '../perception/world_model.js';
import { MotorCortex } from '../l1_kinematics/motor_cortex.js';
import { compare, readPath, firstNumber } from '../l3_gdd/invariant_checker.js';
import { Predicate } from './contract.js';
import { Briefing } from './briefing.js';

const POSITIVE = /(snapped|locked|installed|connected|equipped|deposited|correct|collected|cleaned|extinguished|hit|popped|haswater|attached|full|completed|solved|filled|done)/i;
const NEGATIVE = /(broken|jammed|failed|crash|dead|lost|burn)/i;
const JUNK = /(junk|broken|defect|glitch|mud|stone|dust|brak|fake|decoy|\[x\])/i;
const HAZARD = /(hazard|obstacle|bomb|knot|lava|trap|spike|barrier|electric|pillar)/i;
const NAV = /(back|main_menu|mainmenu|catalog|prev|next|reset|restart|menu|run_sequential|full_cycle|title|statusbadge|launch)/i;
const BAD_TEXT = /(ошибк|брак|неверн|не подходит|сброс|опоздал|рано|wrong|error|fail|invalid|short circuit|замыкан)/i;
// Structural/decorative beacons: tapping them is almost never the mechanic.
const DECOR = /(container|label|fill|progress|bar$|root$|background|^bg|panel|title|caption|text$|hud|frame$|border)/i;
const TOKEN_STOP = new Set(['item', 'slot', 'pin', 'src', 'tgt', 'btn', 'button', 'target', 'the', 'area', 'm', 'ui']);

export interface ExplorerOptions {
  scopeId: string;
  success: Predicate[];
  forbid: Predicate[];
  doNotTouch: string[];
  deadline: number;
  maxActions: number;
  log: (s: string) => void;
  briefing?: Briefing;
  /** Stagnation consult: fresh screenshot + failed attempts → vision plan (rate-limited by the explorer). */
  consult?: (failedAttempts: string[]) => Promise<import('./briefing.js').Plan | null>;
  /** Action keys that made this module fail in earlier runs (game knowledge): never tried again. */
  knownFailCauses?: string[];
}

export interface Observation {
  snap: WorldSnapshot;
  scope: Beacon | null;
  parts: Beacon[];
  progress: number;
  completed: boolean;
  positives: number;
  negatives: number;
  counters: number;
  badText: boolean;
  successHits: number;
  successAll: boolean;
  /** Multi-stage levels: current stage, fails so far, transition pause (board being rebuilt). */
  stage: number;
  fails: number;
  inTransition: boolean;
  failReason: string;
}

export interface ActionCandidate {
  key: string;
  tier: number;
  kind: 'tap' | 'drag' | 'dwell_drag' | 'hold' | 'trace' | 'scrub' | 'rotate' | 'alternate' | 'mash' | 'repeat' | 'plan' | 'pull';
  prior: number;
  label: string;
  run: () => Promise<void>;
  meta?: Record<string, unknown>;
}

export interface ExplorerOutcome {
  solved: boolean;
  actions: number;
  steps: Array<{ t: number; step: string; outcome: string }>;
  learned: string[];
  last: Observation | null;
}

function tokens(s: string): string[] {
  return s.replace(/([a-z])([A-Z])/g, '$1_$2').toLowerCase().split(/[^a-zа-я0-9]+/).filter(t => t.length > 1 && !TOKEN_STOP.has(t) && !/^\d+$/.test(t));
}

function familyKey(id: string): { base: string; index: number } | null {
  // Grid ids (DirtCell_3_2) belong to one family; index is row-major over the numeric suffixes.
  const m = id.match(/^(.*?)((?:[_#]?\d+)+)$/);
  if (!m || m[1].length === 0) return null;
  const nums = m[2].split(/[_#]/).filter(Boolean).map(Number);
  return { base: m[1].replace(/[_#]$/, ''), index: nums.reduce((a, n) => a * 100 + n, 0) };
}

function isJunkish(b: Beacon): boolean {
  return b.props.IsJunk === true || Object.entries(b.game ?? {}).some(([k, v]) => /junk|broken|hazardbomb/i.test(k) && v === true) || JUNK.test(b.testId);
}

function isHazard(b: Beacon): boolean {
  return HAZARD.test(b.testId) || HAZARD.test(String(b.props.AreaType ?? '')) || b.game?.IsHazardBomb === true || b.game?.IsPermanentHazard === true || b.game?.IsObstacle === true;
}

export class UniversalExplorer {
  public readonly steps: Array<{ t: number; step: string; outcome: string }> = [];
  public readonly learned: string[] = [];
  private readonly tried = new Map<string, { n: number; best: number }>();
  private readonly rules: Array<{ itemKey: string; targetKey: string }> = [];
  private readonly rewardedTaps = new Set<string>();
  /** Items whose drag was rewarded (placed); cleared when progress drops back (fail/reset). */
  private readonly committed = new Set<string>();
  private actions = 0;
  private consults = 0;
  private lastConsult = 0;
  private readonly failed: string[] = [];
  /** Actions that made the level fail: never repeated in the same form. */
  private readonly failCauses = new Set<string>();
  /** Items whose grab point is covered by another object (topmost raycast hit is not the item): item → coverer. */
  public readonly occlusions = new Map<string, string>();

  constructor(private readonly world: WorldModel, private readonly motor: MotorCortex, private readonly o: ExplorerOptions) {
    for (const k of o.knownFailCauses ?? []) this.failCauses.add(k);
  }

  /** Fail causes known now (earlier runs + this run), for the game knowledge store. */
  get failCauseKeys(): string[] { return [...this.failCauses]; }

  private note(step: string, outcome: string): void {
    this.steps.push({ t: Date.now(), step, outcome });
    this.o.log(`  explore: ${step} → ${outcome}`);
  }

  // ── Perception ────────────────────────────────────────────────────────────
  async observe(): Promise<Observation> {
    const snap = await this.world.snapshot();
    const scope = snap.get(this.o.scopeId) ?? null;
    const parts = scope ? snap.within(scope).filter(b => b.testId !== scope.testId) : [];
    let positives = 0, negatives = 0, counters = 0, badText = false;
    for (const b of parts) {
      for (const [k, v] of Object.entries(b.game ?? {})) {
        if (v === true && POSITIVE.test(k)) positives++;
        if (v === true && NEGATIVE.test(k)) negatives++;
      }
      if (b.props.IsSnapped === true) positives++;
      const text = String(b.props.Text ?? '');
      const m = text.match(/(\d+)\s*\/\s*(\d+)/);
      if (m) counters += Number(m[1]);
      if (BAD_TEXT.test(text)) badText = true;
    }
    const lookup = (id: string) => (id === '@scope' ? scope : snap.get(id) ?? null);
    const successHits = this.o.success.filter(p => compare(readPath(lookup(p.beacon), p.path), p.op, p.value)).length;
    return {
      snap, scope, parts,
      progress: Number(scope?.props.ProgressNormalized ?? 0),
      completed: scope?.props.IsCompleted === true,
      positives, negatives, counters, badText, successHits,
      successAll: this.o.success.length > 0 && successHits === this.o.success.length,
      stage: Number(scope?.game?.CurrentStage ?? 1),
      fails: Number(scope?.game?.FailCount ?? 0),
      inTransition: scope?.game?.IsInTransition === true,
      failReason: String(scope?.game?.LastFailReason ?? ''),
    };
  }

  reward(a: Observation, b: Observation): number {
    return 100 * (b.successHits - a.successHits)
      + 50 * (Number(b.completed) - Number(a.completed))
      + 20 * (b.progress - a.progress)
      + 3 * (b.positives - a.positives)
      + 2 * (b.counters - a.counters)
      - 3 * (b.negatives - a.negatives)
      - (b.badText && !a.badText ? 1 : 0)
      - 30 * Math.max(0, b.fails - a.fails)
      + 60 * Math.max(0, b.stage - a.stage);
  }

  private allowed(id: string): boolean {
    if (NAV.test(id)) return false;
    return !this.o.doNotTouch.some(p => id === p || (p.endsWith('*') && id.startsWith(p.slice(0, -1))));
  }

  // ── Action generation ─────────────────────────────────────────────────────
  candidates(obs: Observation): ActionCandidate[] {
    const out: ActionCandidate[] = [];
    const parts = obs.parts.filter(b => b.visible && b.center.y > 0 && b.center.y < 1080 && this.allowed(b.testId));
    const scopeRect = obs.scope?.rect;
    const gameplay = parts.filter(b => Object.keys(b.game ?? {}).length > 0);
    const big = (b: Beacon) => scopeRect && b.rect && b.rect.w * b.rect.h > 0.35 * scopeRect.w * scopeRect.h;
    const hazards = obs.parts.filter(isHazard).map(h => ({ c: h.center, r: Math.max(h.rect?.w ?? 40, h.rect?.h ?? 40) / 2 + 55 }));

    // Tier 1: taps on buttons / interactive areas.
    for (const b of parts.filter(p => (p.kind === 'button' || p.kind === 'area' || p.kind === 'slot') && !big(p))) {
      const junk = isJunkish(b);
      const decor = b.kind !== 'button' && DECOR.test(b.testId) && Object.keys(b.game ?? {}).length === 0;
      out.push({ key: `tap:${b.testId}`, tier: decor ? 3 : 1, kind: 'tap', prior: (this.rewardedTaps.has(b.testId) ? 30 : 0) + (b.kind === 'button' ? 4 : 1) - (junk ? 8 : 0) - (decor ? 10 : 0), label: `tap ${b.testId}`,
        run: async () => { await this.motor.tap({ testId: b.testId }, 30); } });
    }

    // Tier 1: item → receptacle drags ranked by similarity and induced rules.
    // An item whose drop was rewarded stays where it is: moving a placed item again only undoes progress.
    const items = parts.filter(p => p.kind === 'draggable' && !big(p) && Object.keys(p.game ?? {}).length > 0 && !this.isPlaced(p) && !this.committed.has(p.testId));
    const receptacles = parts.filter(p => (p.kind === 'slot' || p.kind === 'draggable' || p.kind === 'area') && !big(p) && !this.isPlaced(p, true)
      && !(p.kind !== 'slot' && DECOR.test(p.testId) && Object.keys(p.game ?? {}).length === 0));
    for (const it of items) {
      for (const r of receptacles) {
        if (r === it || dist(r.center, it.center) < 30) continue;
        const ruleHit = this.rules.some(rule => eq(val(it, rule.itemKey), val(r, rule.targetKey)));
        // Two items of the same family (Plank_1, Plank_2) share name tokens — that is kinship, not a match.
        const sameFamily = r.kind === 'draggable' && familyKey(it.testId)?.base === familyKey(r.testId)?.base;
        // Name tokens of one family (Plank_1/Plank_2) are kinship, not a match; equal game values still count.
        const valueSim = this.valueSimilarity(it, r);
        const sim = sameFamily ? valueSim : this.similarity(it, r);
        // An item dropped onto another item is the exception (merge/stack games): unless their state matches
        // (equal level/type), try it only after real receptacles.
        const itemOnItem = r.kind === 'draggable' && !ruleHit && valueSim === 0 ? -12 : 0;
        // A receptacle that already holds some other item (inventory cell) is not where the item should go.
        const holdsOther = r.rect && items.some(o => o !== it && rectContains(r.rect!, o.center, 0)) ? -10 : 0;
        const prior = sim + (ruleHit ? 40 : 0) + (r.kind === 'slot' ? 6 : 0) + itemOnItem + holdsOther - (isJunkish(it) ? 25 : 0) - (isJunkish(r) ? 15 : 0);
        out.push({ key: `drag:${it.testId}>${r.testId}`, tier: 1, kind: 'drag', prior: prior - (this.occlusions.has(it.testId) ? 30 : 0), label: `drag ${it.testId} → ${r.testId}`, meta: { item: it.testId, target: r.testId },
          run: async () => {
            // Grab where no other draggable overlaps the item (two overlapping objects: the engine may pick either).
            const from = await this.grabPoint(it.testId);
            if (!from) return;
            await this.motor.drag(from, r.center, { dwellEndMs: 150 }, `EXPLORE ${it.testId}`);
          } });
        out.push({ key: `dwell:${it.testId}>${r.testId}`, tier: 2, kind: 'dwell_drag', prior: prior - 5, label: `drag ${it.testId} → ${r.testId} and dwell 2.5s`, meta: { item: it.testId, target: r.testId },
          run: async () => { await this.motor.drag(it.center, r.center, { dwellEndMs: 2500, durationMs: 400 }, `DWELL ${it.testId}`); } });
      }
    }
    // Lens-like tools: a draggable/area moved over hidden targets that are not receptacles.
    for (const tool of parts.filter(p => (p.kind === 'draggable' || p.kind === 'area') && !big(p))) {
      for (const t of gameplay.filter(g => g !== tool && !isJunkish(g) && Object.entries(g.game).some(([k, v]) => v === false && POSITIVE.test(k)))) {
        out.push({ key: `dwell:${tool.testId}>${t.testId}`, tier: 2, kind: 'dwell_drag', prior: this.similarity(tool, t) - 2, label: `move ${tool.testId} over ${t.testId} and dwell`,
          run: async () => { await this.motor.drag(tool.center, t.center, { dwellEndMs: 2500, durationMs: 400 }, `DWELL ${tool.testId}`); } });
      }
    }

    // Tier 2: slingshot-style pull — drag a movable object away from the other gameplay objects and release.
    for (const p of parts.filter(q => q.kind === 'draggable' && !big(q) && !isJunkish(q))) {
      const others = gameplay.filter(g => g !== p && !isHazard(g) && dist(g.center, p.center) > 60);
      if (!others.length) continue;
      const cx = others.reduce((a, g) => a + g.center.x, 0) / others.length, cy = others.reduce((a, g) => a + g.center.y, 0) / others.length;
      const d = Math.max(1, Math.hypot(p.center.x - cx, p.center.y - cy));
      for (const len of [160, 230]) {
        out.push({ key: `pull:${p.testId}:${len}`, tier: 2, kind: 'pull', prior: 1 - len / 400, label: `pull ${p.testId} back ${len}px and release`,
          run: async () => {
            const to = { x: p.center.x + (p.center.x - cx) / d * len, y: p.center.y + (p.center.y - cy) / d * len };
            await this.motor.drag(p.center, to, { dwellEndMs: 120, durationMs: 350 }, `PULL ${p.testId}`);
            await sleep(1800);
          } });
      }
    }

    // Tier 2: holds of increasing duration on press-able things.
    for (const b of parts.filter(p => (p.kind === 'area' || p.kind === 'button') && !big(p))) {
      for (const ms of [800, 1600, 2200, 2500, 3200]) {
        out.push({ key: `hold:${b.testId}:${ms}`, tier: 2, kind: 'hold', prior: ('IsPressed' in (b.game ?? {}) ? 6 : 0) - ms / 1000, label: `hold ${b.testId} ${ms}ms`,
          run: async () => {
            await this.waitSettle();
            await this.motor.hold(b.center, { maxMs: ms });
          } });
      }
    }

    // Tier 2: traces through ordered families with a draggable tool.
    const families = new Map<string, Beacon[]>();
    for (const b of obs.parts) {
      const f = familyKey(b.testId);
      if (f && !isHazard(b)) families.set(f.base, [...(families.get(f.base) ?? []), b]);
    }
    const tools = parts.filter(p => p.kind === 'draggable' && !big(p));
    for (const [base, fam] of families) {
      if (fam.length < 3) continue;
      const ordered = [...fam].sort((a, b) => familyKey(a.testId)!.index - familyKey(b.testId)!.index);
      const tool = tools.filter(t => !fam.includes(t)).sort((a, b) => dist(a.center, ordered[0].center) - dist(b.center, ordered[0].center))[0];
      if (!tool) continue;
      for (const closed of [false, true]) {
        out.push({ key: `trace:${base}:${closed}`, tier: 2, kind: 'trace', prior: 8 - (closed ? 1 : 0), label: `trace ${tool.testId} through ${base}_0..${ordered.length - 1}${closed ? ' (closed)' : ''}`,
          run: async () => {
            const route = [tool.center, ...ordered.map(o => o.center), ...(closed ? [ordered[0].center] : [])];
            await this.motor.slice(pushOut(densify(route, 12), hazards), { speedPxPerS: 320, dwellEndMs: 120, tremorPx: 0.3 });
          } });
      }
      if (fam.length >= 6) {
        out.push({ key: `scrub:${base}`, tier: 2, kind: 'scrub', prior: 6, label: `scrub ${tool.testId} over ${base}*`,
          run: async () => {
            const rows = new Map<number, Beacon[]>();
            for (const c of fam) rows.set(Math.round(c.center.y / 20), [...(rows.get(Math.round(c.center.y / 20)) ?? []), c]);
            const pts: Point[] = [tool.center];
            [...rows.entries()].sort((a, b) => b[0] - a[0]).forEach(([, cells], i) => {
              cells.sort((a, b) => (i % 2 ? b.center.x - a.center.x : a.center.x - b.center.x));
              for (const c of cells) pts.push({ x: c.center.x - 30, y: c.center.y }, { x: c.center.x + 30, y: c.center.y });
            });
            await this.motor.slice(pts, { speedPxPerS: 600 });
          } });
      }
    }

    // Tier 2: rotations on roughly square areas.
    for (const b of parts.filter(p => p.kind === 'area' && p.rect && Math.abs(p.rect.w - p.rect.h) < 0.25 * p.rect.w && p.rect.w >= 110 && !big(p))) {
      out.push({ key: `rotate:${b.testId}`, tier: 2, kind: 'rotate', prior: (isJunkish(b) ? -10 : 2) + (Object.keys(b.game).some(k => /angle|rot/i.test(k)) ? 8 : 0), label: `rotate ${b.testId} 2 turns`,
        run: async () => {
          const r = b.rect!.w * 0.32;
          const pts: Point[] = [];
          for (let i = 0; i <= 72; i++) pts.push({ x: b.center.x + r * Math.cos(i * Math.PI / 18), y: b.center.y + r * Math.sin(i * Math.PI / 18) });
          await this.motor.slice(pts, { speedPxPerS: 520 });
        } });
    }

    // Tier 3: alternating two buttons (rhythm) and mashing one button (accumulators).
    const buttons = parts.filter(p => p.kind === 'button' && !isJunkish(p) && !big(p));
    for (let i = 0; i < buttons.length; i++) {
      out.push({ key: `mash:${buttons[i].testId}`, tier: 3, kind: 'mash', prior: this.rewardedTaps.has(buttons[i].testId) ? 20 : 0, label: `mash ${buttons[i].testId} while progress grows`,
        run: async () => { await this.adaptiveMash(buttons[i].testId); } });
      out.push({ key: `holdlong:${buttons[i].testId}`, tier: 2, kind: 'hold', prior: 1, label: `hold ${buttons[i].testId} while progress grows`,
        run: async () => { await this.adaptiveHold(buttons[i].testId); } });
      for (let j = i + 1; j < buttons.length; j++) {
        const [a, c] = [buttons[i], buttons[j]];
        if (Math.abs(a.center.y - c.center.y) > 40) continue;
        out.push({ key: `alt:${a.testId}|${c.testId}`, tier: 3, kind: 'alternate', prior: 2, label: `alternate ${a.testId}/${c.testId} ×16 @550ms`,
          run: async () => { for (let k = 0; k < 16; k++) { await this.motor.tap({ testId: (k % 2 ? c : a).testId }, 16); await sleep(520); } } });
      }
    }
    return this.applyBriefing(out, obs);
  }

  /** Briefing first: explicit plans become tier-0 actions; verbs and "avoid" words re-rank everything else. */
  private applyBriefing(out: ActionCandidate[], obs: Observation): ActionCandidate[] {
    const b = this.o.briefing;
    if (!b) return out;
    const verbKinds = new Set<string>(b.verbs);
    const avoidWords = b.avoid.flatMap(a => a.toLowerCase().split(/[^a-zа-яё0-9]+/).filter(w => w.length > 3));
    for (const c of out) {
      if (verbKinds.has(c.kind) && !(b.designOnlyVerbs ?? []).includes(c.kind as any)) {
        // The level text names this action explicitly ("wipe with the sponge", "pull back and release", "hold the
        // pedal"): it is the first hypothesis to test, before generic taps. Taps and drags keep their tier —
        // "нажмите"/"перетащите" are too common to reorder everything — but rank higher inside it.
        if (c.kind !== 'tap' && c.kind !== 'drag') { c.tier = 0; c.prior += 40; } else c.prior += 8;
      } else if (c.kind === 'dwell_drag' && verbKinds.has('drag')) c.prior += 8;
      const label = c.label.toLowerCase();
      if (avoidWords.some(w => label.includes(w.slice(0, 5)))) c.prior -= 20;
    }
    const run = b.plans.find(p => p.kind === 'press_run')?.button;
    // Vision plan (hypothesis from the screenshot, restricted to live beacon ids): every step is its own tier-0
    // candidate, so its effect is observed and logged separately and a useless step does not hide a good one.
    for (const plan of b.plans.filter(p => p.kind === 'vision_plan')) {
      const avoidIds = new Set(b.vision?.avoid ?? []);
      plan.visionSteps!.forEach((st, i) => {
        const obj = st.item ?? st.button;
        // The model sometimes lists a trap as a plan step ("the broken bridge is a trap") — never act on it.
        if (obj && avoidIds.has(obj) || /(avoid|trap|decoy|do not|don't)/i.test(st.why)) return;
        const objB = obj ? obs.snap.get(obj) : null;
        // A marked defect ([X], IsJunk) is never carried into a receptacle, whatever the vision model proposes.
        if (objB && isJunkish(objB) && (st.action === 'drag' || st.action === 'move_over')) return;
        if ((st.action === 'trace' || st.action === 'drag' || st.action === 'move_over' || st.action === 'pull') && objB && objB.kind !== 'draggable') return;
        out.push({ key: `plan:vision:${i}:${st.action}:${obj}>${st.target ?? ''}`, tier: 0, kind: 'plan', prior: 150 - i,
          label: `vision step ${i + 1}: ${st.action} ${obj ?? ''}${st.target ? ' → ' + st.target : ''}${st.durationMs ? ` ${st.durationMs}ms` : ''} (${st.why.slice(0, 60)})`,
          run: async () => { await this.runVisionStep(st, obs); } });
      });
      for (const id of b.vision?.avoid ?? []) for (const c of out) if (c.label.includes(id)) c.prior -= 25;
    }
    for (const plan of b.plans.filter(p => p.kind === 'ordered_placement')) {
      out.push({ key: `plan:${plan.steps!.map(s => s.item).join(',')}`, tier: 0, kind: 'plan', prior: 200, label: `briefing plan: ${plan.why}${run ? ` then press ${run}` : ''}`,
        run: async () => {
          for (const st of plan.steps!) {
            const it = await this.world.inspect(st.item), tg = await this.world.inspect(st.target);
            if (it && tg) await this.motor.drag(it.center, tg.center, { dwellEndMs: 150 }, `PLAN ${st.item}→${st.target}`);
            await sleep(200);
          }
          if (run) { await this.motor.tap({ testId: run }); await sleep(2500); }
        } });
    }
    if (run) {
      const t = out.find(c => c.key === `tap:${run}`);
      if (t) t.prior += 15;
    }
    void obs;
    return out;
  }

  private async runVisionStep(st: import('./vision.js').VisionPlanStep, obs: Observation): Promise<void> {
    const id = st.item ?? st.button;
    const it = id ? await this.world.inspect(id) : null;
    const tg = st.target ? await this.world.inspect(st.target) : null;
    switch (st.action) {
      case 'tap':
        if (!id) return;
        if ((st.repeat ?? 1) > 3) await this.adaptiveMash(id, Math.min(250, st.repeat! * 3));
        else for (let k = 0; k < Math.max(1, st.repeat ?? 1); k++) { await this.motor.tap({ testId: id }); await sleep(120); }
        return;
      case 'drag': {
        const from = it ? await this.grabPoint(it.testId) : null;
        if (from && tg && it!.kind === 'draggable') await this.motor.drag(from, tg.center, { dwellEndMs: 150 }, `VISION ${id}→${st.target}`);
        return;
      }
      case 'move_over':
        if (it && tg) await this.motor.drag(it.center, tg.center, { dwellEndMs: Math.min(6000, Math.max(1500, st.durationMs ?? 2500)), durationMs: 400 }, `VISION over ${st.target}`);
        return;
      case 'pull': {
        if (!it) return;
        const others = obs.parts.filter(g => g.testId !== id && Object.keys(g.game ?? {}).length > 0 && !isHazard(g));
        const ref = tg?.center ?? (others.length ? { x: others.reduce((a, g) => a + g.center.x, 0) / others.length, y: others.reduce((a, g) => a + g.center.y, 0) / others.length } : { x: it.center.x + 1, y: it.center.y });
        const d = Math.max(1, dist(it.center, ref));
        await this.motor.drag(it.center, { x: it.center.x + (it.center.x - ref.x) / d * 200, y: it.center.y + (it.center.y - ref.y) / d * 200 }, { dwellEndMs: 120, durationMs: 350 }, `VISION pull ${id}`);
        await sleep(1800);
        return;
      }
      case 'hold':
        if (!it) return;
        // "hold nozzle → target": the press point is what aims (spray, laser, steering) — press at the target.
        if (tg) { await this.motor.hold(tg.center, { maxMs: Math.min(6000, st.durationMs || 1500) }); return; }
        if ((st.durationMs ?? 0) > 5000) await this.adaptiveHold(id!, Math.min(90000, st.durationMs!));
        else await this.motor.hold(it.center, { maxMs: st.durationMs || 1500 });
        return;
      case 'trace': {
        // Only a draggable tool can be moved across an area; "trace the car along the track" is not an input.
        if (!it || it.kind !== 'draggable') return;
        const r = (tg ?? obs.scope)?.rect;
        if (!r) return;
        // Serpentine sweep across the target area (wipe / steer / scan).
        const pts: Point[] = [it.center];
        for (let k = 0; k < 5; k++) {
          const y = r.y + r.h * (k + 0.5) / 5;
          pts.push({ x: k % 2 ? r.x + r.w * 0.9 : r.x + r.w * 0.1, y }, { x: k % 2 ? r.x + r.w * 0.1 : r.x + r.w * 0.9, y });
        }
        await this.motor.slice(pts, { speedPxPerS: 600 });
        return;
      }
      case 'rotate': {
        if (!it) return;
        const rad = Math.max(40, (it.rect?.w ?? 120) * 0.32);
        const pts: Point[] = [];
        for (let i = 0; i <= 72; i++) pts.push({ x: it.center.x + rad * Math.cos(i * Math.PI / 18), y: it.center.y + rad * Math.sin(i * Math.PI / 18) });
        await this.motor.slice(pts, { speedPxPerS: 520 });
        return;
      }
      case 'wait':
        await sleep(Math.min(3000, st.durationMs ?? 800));
    }
  }

  /** Tap repeatedly while the observed effect keeps improving (accumulators, tug-of-war, pumps). */
  async adaptiveMash(testId: string, maxTaps = 200, periodMs = 110): Promise<void> {
    let best = (await this.observe()).progress, stale = 0;
    for (let k = 0; k < maxTaps && Date.now() < this.o.deadline; k++) {
      await this.motor.tap({ testId }, 0);
      await sleep(periodMs);
      if (k % 4 === 3) {
        const o = await this.observe();
        if (o.successAll || o.completed) return;
        if (o.progress > best + 1e-3) { best = o.progress; stale = 0; } else if (++stale >= 6) return;
      }
    }
  }

  /** Hold while progress keeps growing (pedals, charge); release on stall, success or maxMs. */
  async adaptiveHold(testId: string, maxMs = 60000): Promise<void> {
    const b = await this.world.inspect(testId);
    if (!b) return;
    let best = (await this.observe()).progress, lastGain = Date.now();
    await this.motor.hold(b.center, { maxMs, pollMs: 250, until: async () => {
      const o = await this.observe();
      if (o.successAll || o.completed) return true;
      if (o.progress > best + 1e-3) { best = o.progress; lastGain = Date.now(); }
      return Date.now() - lastGain > 4000;
    } });
  }

  private isPlaced(b: Beacon, receptacle = false): boolean {
    const g = b.game ?? {};
    if (receptacle) return g.IsOccupied === true || g.IsFull === true || g.IsInstalled === true;
    return b.props.IsSnapped === true || Object.entries(g).some(([k, v]) => v === true && /snapped|locked|installed|connected|equipped|deposited/i.test(k));
  }

  /** Equal non-boolean game values (Level 2 == Level 2, ItemType red == red), ignoring position fields. */
  private valueSimilarity(a: Beacon, b: Beacon): number {
    let s = 0;
    for (const [k, v] of Object.entries(a.game ?? {})) {
      if (typeof v === 'boolean' || v === '' || v === null || /^(row|col|x|y|index|id)$/i.test(k)) continue;
      if (eq(v, b.game?.[k])) s += 6;
    }
    return s;
  }

  private similarity(a: Beacon, b: Beacon): number {
    let s = 0;
    for (const [k, v] of Object.entries(a.game ?? {})) {
      if (typeof v === 'boolean' || v === '' || v === null) continue;
      for (const [k2, v2] of Object.entries(b.game ?? {})) if (eq(v, v2) && typeof v2 !== 'boolean') s += (k === k2 ? 6 : 4);
    }
    const ta = new Set(tokens(a.testId)), tb = tokens(b.testId);
    for (const t of tb) if (ta.has(t)) s += 3;
    return s;
  }

  /** After a rewarded drag, record which attribute relations held — reused to rank remaining pairs. */
  private induceRule(item: Beacon, target: Beacon): void {
    const pairs: Array<{ itemKey: string; targetKey: string }> = [];
    const keysA = [...Object.keys(item.game ?? {}).map(k => `game.${k}`), 'id:suffix', 'id:tokens'];
    const keysB = [...Object.keys(target.game ?? {}).map(k => `game.${k}`), 'id:suffix', 'id:tokens'];
    for (const ka of keysA) for (const kb of keysB) {
      const va = val(item, ka), vb = val(target, kb);
      if (va === undefined || typeof va === 'boolean' || va === '' || !eq(va, vb)) continue;
      if (!this.rules.some(r => r.itemKey === ka && r.targetKey === kb)) pairs.push({ itemKey: ka, targetKey: kb });
    }
    for (const p of pairs) {
      this.rules.push(p);
      this.learned.push(`match rule: item.${p.itemKey} == receptacle.${p.targetKey} (from ${item.testId} → ${target.testId})`);
    }
  }

  private async waitSettle(maxMs = 4000): Promise<void> {
    let last = (await this.observe()).progress;
    const start = Date.now();
    while (Date.now() - start < maxMs) {
      await sleep(250);
      const p = (await this.observe()).progress;
      if (Math.abs(p - last) < 0.005) return;
      last = p;
    }
  }

  // ── Main loop ─────────────────────────────────────────────────────────────
  async run(): Promise<ExplorerOutcome> {
    let obs = await this.observe();
    let tier = 0;
    let fruitlessInTier = 0;
    while (Date.now() < this.o.deadline && this.actions < this.o.maxActions) {
      if (obs.successAll) return this.done(true, obs);
      if (!obs.scope?.active) { this.note('scope disappeared', 'checking whether the goal was reached before teardown'); return this.done(false, obs); }
      if (obs.inTransition) { await sleep(300); obs = await this.observe(); continue; }
      const cands = this.candidates(obs)
        .filter(c => c.tier <= tier)
        .map(c => ({ c, t: this.tried.get(c.key) }))
        .filter(({ c, t }) => !t || (t.best > 0 && t.n < 25 && (c.kind === 'tap' || c.kind === 'mash')))
        .filter(({ c }) => !(c.kind === 'tap' && this.deadFamily(c.key.slice(4))))
        .filter(({ c }) => !this.failCauses.has(c.key))
        .sort((a, b) => (b.c.prior - (b.t?.n ?? 0) * 2) - (a.c.prior - (a.t?.n ?? 0) * 2));
      // Stuck: look at the screen again (vision) with the list of attempts that did nothing.
      if (this.o.consult && this.o.briefing && fruitlessInTier >= 6 && this.consults < 3 && Date.now() - this.lastConsult > 20000) {
        this.consults++;
        this.lastConsult = Date.now();
        const plan = await this.o.consult([...this.failed]);
        this.note(`stagnation consult #${this.consults} (screenshot + ${this.failed.length} failed attempts)`, plan ? plan.why : 'no new plan');
        if (plan) {
          this.o.briefing.plans = [plan, ...this.o.briefing.plans.filter(p => p.kind !== 'vision_plan')];
          for (const k of [...this.tried.keys()]) if (k.startsWith('plan:vision')) this.tried.delete(k);
          tier = 0;
          fruitlessInTier = 0;
          continue;
        }
      }
      if (cands.length === 0 || fruitlessInTier > (tier === 0 ? 8 : 24)) {
        if (tier >= 3) break;
        tier++;
        fruitlessInTier = 0;
        this.note(`escalate to tier ${tier}`, tier === 1 ? 'taps and drags' : tier === 2 ? 'holds, dwell drags, traces, scrubs, rotations' : 'rhythm, mashing');
        continue;
      }
      const { c } = cands[0];
      const before = obs;
      await c.run();
      this.actions++;
      await sleep(250);
      obs = await this.observe();
      // Drops often resolve after an animation (item flies into the bucket, then is accepted or rejected): if
      // nothing visible changed yet, look again before blaming or crediting the next action.
      if ((c.kind === 'drag' || c.kind === 'dwell_drag' || c.kind === 'plan') && !obs.inTransition && obs.fails === before.fails
          && Math.abs(obs.progress - before.progress) < 1e-3) {
        await sleep(450);
        obs = await this.observe();
      }
      // A transition (stage cleared or fail restart) is in progress: wait until the new board is ready.
      for (let w = 0; w < 20 && obs.inTransition; w++) { await sleep(250); obs = await this.observe(); }
      const r = this.reward(before, obs);
      if (obs.fails > before.fails) {
        // The level punished this action and rebuilt the board: remember the cause, forget attempts on the old board.
        this.failCauses.add(c.key);
        this.note(`FAIL after "${c.label}"`, obs.failReason || 'level restarted');
        this.learned.push(`fail cause: ${c.label}${obs.failReason ? ` — "${obs.failReason}"` : ''}`);
        this.resetBoardMemory();
        tier = 0; fruitlessInTier = 0;
      } else if (obs.stage > before.stage) {
        this.note(`stage ${before.stage} cleared`, `stage ${obs.stage} starts — keeping ${this.rules.length} learned rules`);
        this.resetBoardMemory();
        this.consults = 0;
        tier = 0; fruitlessInTier = 0;
      }
      // Progress fell (a fail reset the board or a stage restarted): placements are gone, everything is movable again.
      if (obs.progress < before.progress - 1e-3) this.committed.clear();
      const t = this.tried.get(c.key) ?? { n: 0, best: -Infinity };
      this.tried.set(c.key, { n: t.n + 1, best: Math.max(t.best, r) });
      this.note(c.label, r > 0 ? `reward +${r.toFixed(1)} (progress ${before.progress.toFixed(2)}→${obs.progress.toFixed(2)})` : r < 0 ? `penalty ${r.toFixed(1)}` : 'no effect');
      if (r > 0) {
        fruitlessInTier = 0;
        // Repeat a tap only if it moved real progress; a tap that just changes a counter (spawn, draw) is not a strategy.
        if ((c.kind === 'tap' || c.kind === 'mash') && obs.progress > before.progress + 1e-3) this.rewardedTaps.add(c.key.split(':')[1]);
        if ((c.kind === 'drag' || c.kind === 'dwell_drag') && c.meta && obs.progress > before.progress) this.committed.add(String(c.meta.item));
        if ((c.kind === 'drag' || c.kind === 'dwell_drag') && c.meta) {
          const it = before.snap.get(String(c.meta.item)), tg = before.snap.get(String(c.meta.target));
          if (it && tg) this.induceRule(it, tg);
        }
        if (!this.learned.includes(`primitive ${c.kind} is effective here`)) this.learned.push(`primitive ${c.kind} is effective here`);
      } else {
        fruitlessInTier++;
        this.failed.push(c.label);
        if (this.failed.length > 40) this.failed.shift();
      }
      if (!obs.scope?.active && before.progress > 0) break;
    }
    return this.done(obs.successAll, obs);
  }

  /**
   * A point on the item that no other draggable covers (sampled over the item's rect, nearest to the centre first).
   * Overlapping draggables at equal depth make the engine's choice ambiguous, so the overlap itself is avoided;
   * if every point is covered the item is reported as occluded and not dragged.
   */
  private async grabPoint(testId: string): Promise<Point | null> {
    const snap = await this.world.snapshot();
    const it = snap.get(testId);
    if (!it) return null;
    if (!it.rect) return (await this.occluded(testId)) ? null : it.center;
    const others = snap.beacons.filter(b => b.testId !== testId && b.kind === 'draggable' && b.visible && b.rect
      && !b.testId.startsWith(testId + '_label') && rectsOverlap(b.rect, it.rect!));
    const inside = (p: Point) => others.some(o => rectContains(o.rect!, p, 4));
    if (!others.length) return (await this.occluded(testId)) ? null : it.center;
    const r = it.rect;
    const pts: Point[] = [];
    for (const fx of [0.5, 0.3, 0.7, 0.18, 0.82]) for (const fy of [0.5, 0.3, 0.7, 0.18, 0.82]) pts.push({ x: r.x + r.w * fx, y: r.y + r.h * fy });
    pts.sort((a, b) => dist(a, it.center) - dist(b, it.center));
    const free = pts.find(p => !inside(p));
    if (free) {
      if (dist(free, it.center) > 1) this.note(`grab ${testId}`, `centre overlapped by ${others.map(o => o.testId).join(', ')} — grabbing at an uncovered point`);
      if (!this.occlusions.has(testId)) this.occlusions.set(testId, others.map(o => o.testId).join(', '));
      return free;
    }
    if (!this.occlusions.has(testId)) {
      this.occlusions.set(testId, others.map(o => o.testId).join(', '));
      this.note(`occlusion: ${testId}`, `fully covered by ${others.map(o => o.testId).join(', ')} — not dragging`);
    }
    return null;
  }

  /** True when the topmost UI hit at the item's centre is another object (its grab would take that object). */
  private async occluded(testId: string): Promise<boolean> {
    const r: any = await (this.world as any).client.call('hit_test', { testId }, 3000).catch(() => null);
    const top = String(r?.data?.topHit?.path ?? ''), mine = String(r?.data?.targetDiagnostics?.path ?? '');
    if (!top || !mine || top === mine || top.startsWith(mine + '/') || mine.startsWith(top + '/')) return false;
    const coverer = String(r?.data?.topHit?.name ?? top);
    if (!this.occlusions.has(testId)) {
      this.occlusions.set(testId, coverer);
      this.note(`occlusion: ${testId}`, `grab point covered by "${coverer}" — not dragging`);
    }
    return true;
  }

  /** New board (next stage or fail restart): positions and object sets changed; learned rules and fail causes stay. */
  private resetBoardMemory(): void {
    this.tried.clear();
    this.committed.clear();
    this.rewardedTaps.clear();
    this.failed.length = 0;
  }

  /** True when ≥3 members of this id's family were tapped without any effect — the family is not tappable. */
  private deadFamily(id: string): boolean {
    const f = familyKey(id);
    if (!f) return false;
    let dead = 0;
    for (const [k, t] of this.tried) {
      if (!k.startsWith('tap:') || t.best > 0) continue;
      const g = familyKey(k.slice(4));
      if (g && g.base === f.base && ++dead >= 3) return true;
    }
    return false;
  }

  private done(solved: boolean, last: Observation): ExplorerOutcome {
    return { solved, actions: this.actions, steps: this.steps, learned: this.learned, last };
  }

  get actionCount(): number { return this.actions; }
}

function val(b: Beacon, key: string): unknown {
  if (key === 'id:suffix') return b.testId.split('_').pop()?.toLowerCase();
  if (key === 'id:tokens') return tokens(b.testId).sort().join(' ');
  if (key.startsWith('game.')) return b.game?.[key.slice(5)];
  return b.props[key];
}

function eq(a: unknown, b: unknown): boolean {
  if (a === undefined || b === undefined || a === null || b === null) return false;
  return String(a).trim().toLowerCase() === String(b).trim().toLowerCase();
}

function densify(points: Point[], step: number): Point[] {
  const out = [points[0]];
  for (let i = 1; i < points.length; i++) {
    const n = Math.max(1, Math.ceil(dist(points[i - 1], points[i]) / step));
    for (let k = 1; k <= n; k++) out.push({ x: points[i - 1].x + (points[i].x - points[i - 1].x) * k / n, y: points[i - 1].y + (points[i].y - points[i - 1].y) * k / n });
  }
  return out;
}

function pushOut(path: Point[], hazards: Array<{ c: Point; r: number }>): Point[] {
  return path.map(p => {
    let q = p;
    for (const h of hazards) {
      const d = dist(q, h.c);
      if (d < h.r && d > 1e-3) q = { x: h.c.x + (q.x - h.c.x) / d * h.r, y: h.c.y + (q.y - h.c.y) / d * h.r };
    }
    return q;
  });
}

export { rectContains, firstNumber };

function rectsOverlap(a: { x: number; y: number; w: number; h: number }, b: { x: number; y: number; w: number; h: number }): boolean {
  return a.x < b.x + b.w && b.x < a.x + a.w && a.y < b.y + b.h && b.y < a.y + a.h;
}
