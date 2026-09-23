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
}

export interface ActionCandidate {
  key: string;
  tier: number;
  kind: 'tap' | 'drag' | 'dwell_drag' | 'hold' | 'trace' | 'scrub' | 'rotate' | 'alternate' | 'mash' | 'repeat' | 'plan';
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
  const m = id.match(/^(.*?)[_#]?(\d+)$/);
  return m && m[1].length > 0 ? { base: m[1].replace(/[_#]$/, ''), index: Number(m[2]) } : null;
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
  private actions = 0;
  private consults = 0;
  private lastConsult = 0;
  private readonly failed: string[] = [];

  constructor(private readonly world: WorldModel, private readonly motor: MotorCortex, private readonly o: ExplorerOptions) {}

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
    };
  }

  reward(a: Observation, b: Observation): number {
    return 100 * (b.successHits - a.successHits)
      + 50 * (Number(b.completed) - Number(a.completed))
      + 20 * (b.progress - a.progress)
      + 3 * (b.positives - a.positives)
      + 2 * (b.counters - a.counters)
      - 3 * (b.negatives - a.negatives)
      - (b.badText && !a.badText ? 1 : 0);
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
      out.push({ key: `tap:${b.testId}`, tier: 1, kind: 'tap', prior: (this.rewardedTaps.has(b.testId) ? 30 : 0) + (b.kind === 'button' ? 4 : 1) - (junk ? 8 : 0), label: `tap ${b.testId}`,
        run: async () => { await this.motor.tap({ testId: b.testId }, 30); } });
    }

    // Tier 1: item → receptacle drags ranked by similarity and induced rules.
    const items = parts.filter(p => p.kind === 'draggable' && !big(p) && Object.keys(p.game ?? {}).length > 0 && !this.isPlaced(p));
    const receptacles = parts.filter(p => (p.kind === 'slot' || p.kind === 'draggable' || p.kind === 'area') && !big(p) && !this.isPlaced(p, true));
    for (const it of items) {
      for (const r of receptacles) {
        if (r === it || dist(r.center, it.center) < 30) continue;
        const sim = this.similarity(it, r);
        const ruleHit = this.rules.some(rule => eq(val(it, rule.itemKey), val(r, rule.targetKey)));
        const prior = sim + (ruleHit ? 40 : 0) + (r.kind === 'slot' ? 1 : 0) - (isJunkish(it) ? 25 : 0) - (isJunkish(r) ? 15 : 0);
        out.push({ key: `drag:${it.testId}>${r.testId}`, tier: 1, kind: 'drag', prior, label: `drag ${it.testId} → ${r.testId}`, meta: { item: it.testId, target: r.testId },
          run: async () => { await this.motor.drag(it.center, r.center, { dwellEndMs: 150 }, `EXPLORE ${it.testId}`); } });
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
      if (verbKinds.has(c.kind) || (c.kind === 'dwell_drag' && verbKinds.has('drag'))) {
        c.prior += 8;
        // The level text names this action explicitly ("tap fast", "hold the pedal") — do not bury it in tier 3.
        if (c.kind === 'mash' || c.kind === 'hold' || c.kind === 'alternate' || c.kind === 'rotate' || c.kind === 'trace' || c.kind === 'scrub') c.tier = Math.min(c.tier, 1);
      }
      const label = c.label.toLowerCase();
      if (avoidWords.some(w => label.includes(w.slice(0, 5)))) c.prior -= 20;
    }
    const run = b.plans.find(p => p.kind === 'press_run')?.button;
    // Vision plan (hypothesis from the screenshot, already restricted to live beacon ids).
    for (const plan of b.plans.filter(p => p.kind === 'vision_plan')) {
      out.push({ key: 'plan:vision', tier: 0, kind: 'plan', prior: 150, label: `vision plan: ${plan.why}`,
        run: async () => {
          for (const st of plan.visionSteps!) {
            const it = st.item ? await this.world.inspect(st.item) : null;
            const tg = st.target ? await this.world.inspect(st.target) : null;
            if (st.action === 'drag' && it && tg) await this.motor.drag(it.center, tg.center, { dwellEndMs: 150 }, `VISION ${st.item}→${st.target}`);
            else if (st.action === 'tap' && (st.button || st.item)) {
              if ((st.repeat ?? 1) > 3) await this.adaptiveMash((st.button ?? st.item)!, Math.min(250, st.repeat! * 3));
              else await this.motor.tap({ testId: (st.button ?? st.item)! });
            }
            else if (st.action === 'hold' && (st.item || st.button)) {
              if ((st.durationMs ?? 0) > 5000) await this.adaptiveHold((st.button ?? st.item)!, Math.min(90000, st.durationMs!));
              else { const h = it ?? await this.world.inspect(st.button!); if (h) await this.motor.hold(h.center, { maxMs: st.durationMs ?? 1500 }); }
            }
            else if (st.action === 'wait') await sleep(Math.min(3000, st.durationMs ?? 800));
            await sleep(250);
          }
        } });
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
    let tier = this.o.briefing?.plans.some(p => p.kind === 'ordered_placement' || p.kind === 'vision_plan') ? 0 : 1;
    let fruitlessInTier = 0;
    while (Date.now() < this.o.deadline && this.actions < this.o.maxActions) {
      if (obs.successAll) return this.done(true, obs);
      if (!obs.scope?.active) { this.note('scope disappeared', 'checking whether the goal was reached before teardown'); return this.done(false, obs); }
      const cands = this.candidates(obs)
        .filter(c => c.tier <= tier)
        .map(c => ({ c, t: this.tried.get(c.key) }))
        .filter(({ c, t }) => !t || (t.best > 0 && t.n < 25 && (c.kind === 'tap' || c.kind === 'mash')))
        .sort((a, b) => (b.c.prior - (b.t?.n ?? 0) * 2) - (a.c.prior - (a.t?.n ?? 0) * 2));
      // Stuck: look at the screen again (vision) with the list of attempts that did nothing.
      if (this.o.consult && this.o.briefing && fruitlessInTier >= 10 && this.consults < 3 && Date.now() - this.lastConsult > 20000) {
        this.consults++;
        this.lastConsult = Date.now();
        const plan = await this.o.consult([...this.failed]);
        this.note(`stagnation consult #${this.consults} (screenshot + ${this.failed.length} failed attempts)`, plan ? plan.why : 'no new plan');
        if (plan) {
          this.o.briefing.plans = [plan, ...this.o.briefing.plans.filter(p => p.kind !== 'vision_plan')];
          this.tried.delete('plan:vision');
          tier = 0;
          fruitlessInTier = 0;
          continue;
        }
      }
      if (cands.length === 0 || fruitlessInTier > 24) {
        if (tier >= 3) break;
        tier++;
        fruitlessInTier = 0;
        this.note(`escalate to tier ${tier}`, tier === 2 ? 'holds, dwell drags, traces, scrubs, rotations' : 'rhythm, mashing');
        continue;
      }
      const { c } = cands[0];
      const before = obs;
      await c.run();
      this.actions++;
      await sleep(250);
      obs = await this.observe();
      const r = this.reward(before, obs);
      const t = this.tried.get(c.key) ?? { n: 0, best: -Infinity };
      this.tried.set(c.key, { n: t.n + 1, best: Math.max(t.best, r) });
      this.note(c.label, r > 0 ? `reward +${r.toFixed(1)} (progress ${before.progress.toFixed(2)}→${obs.progress.toFixed(2)})` : r < 0 ? `penalty ${r.toFixed(1)}` : 'no effect');
      if (r > 0) {
        fruitlessInTier = 0;
        if (c.kind === 'tap' || c.kind === 'mash') this.rewardedTaps.add(c.key.split(':')[1]);
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
