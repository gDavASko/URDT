/**
 * Shared runtime for L2 playbooks (HTN methods). A playbook turns one GDD scenario into live motor
 * primitives, re-perceiving the world before every decision and verifying every action by its effect.
 */

import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { Beacon, BeaconKind, Point, WorldModel, WorldSnapshot, sleep, rectContains } from '../perception/world_model.js';
import { MotorCortex } from '../l1_kinematics/motor_cortex.js';
import { MicroSlmArbiter } from './micro_slm_arbiter.js';
import { ScenarioSpec } from '../l3_gdd/gdd_loader.js';

export type PlaybookStatus = 'COMPLETED' | 'STUCK' | 'FAILED' | 'TIMEOUT' | 'DISCOVERY_REQUIRED';

export interface PlaybookResult {
  status: PlaybookStatus;
  summary: string;
  stagnationType?: 'MICRO_STUCK' | 'DEADLOCK_TOPOLOGY' | 'SOFT_LOCK' | 'CRASH_EXCEPTION' | 'TIMEOUT';
}

export interface HypothesisRecord { t: number; text: string; outcome?: 'confirmed' | 'refuted' | 'open' }

export interface ModuleState { progress: number; completed: boolean; beacon: Beacon | null }

export class PlaybookContext {
  public readonly hypotheses: HypothesisRecord[] = [];
  public readonly log: string[] = [];
  public readonly deadline: number;
  public stuckCounter = 0;
  private readonly recentActions: string[] = [];
  /** Latched observations: a mechanic may be torn down right after completion (victory → catalog). */
  public readonly latched = { completed: false, progress: 0, completedAt: 0, beacons: new Map<string, Beacon>() };
  private watchTimer: NodeJS.Timeout | null = null;

  constructor(
    public readonly client: UrdtWireClient,
    public readonly world: WorldModel,
    public readonly motor: MotorCortex,
    public readonly arbiter: MicroSlmArbiter,
    public readonly scenario: ScenarioSpec,
    public readonly verbose = true,
  ) {
    this.deadline = Date.now() + scenario.timeoutMs;
  }

  get moduleId(): string { return this.scenario.module ?? this.scenario.id; }
  get params(): Record<string, any> { return this.scenario.params ?? {}; }
  timeLeft(): number { return this.deadline - Date.now(); }
  expired(): boolean { return Date.now() > this.deadline; }

  /** Defects a skill noticed in the game's response (e.g. one control command moved two lanes). Collected by L3. */
  readonly reports: Array<{ severity: 'CRITICAL' | 'MAJOR' | 'MINOR' | 'INFO'; kind: string; message: string; evidence?: unknown }> = [];

  report(severity: 'CRITICAL' | 'MAJOR' | 'MINOR' | 'INFO', kind: string, message: string, evidence?: unknown): void {
    if (this.reports.some(r => r.kind === kind && r.message === message)) return;
    this.reports.push({ severity, kind, message, evidence });
    this.say(`REPORT ${severity} ${kind}: ${message}`);
  }

  say(msg: string): void {
    const line = `[${new Date().toISOString().slice(11, 23)}] ${this.scenario.id} | ${msg}`;
    this.log.push(line);
    if (this.verbose) console.log(line);
  }

  /** Micro-level GDD tier: a hypothesis formed from live state right before acting. */
  hypothesize(text: string): HypothesisRecord {
    const h: HypothesisRecord = { t: Date.now(), text, outcome: 'open' };
    this.hypotheses.push(h);
    this.say(`HYPOTHESIS: ${text}`);
    return h;
  }

  resolve(h: HypothesisRecord, ok: boolean): void {
    h.outcome = ok ? 'confirmed' : 'refuted';
    this.say(`  → ${ok ? 'confirmed' : 'refuted'}`);
  }

  async snapshot(): Promise<WorldSnapshot> { return this.world.snapshot(); }

  async module(): Promise<ModuleState> {
    const b = await this.world.inspect(this.moduleId);
    if (b) this.latch(b);
    const live = { progress: Number(b?.props.ProgressNormalized ?? 0), completed: b?.props.IsCompleted === true };
    return {
      progress: Math.max(live.progress, this.latched.completed ? 1 : 0, b ? 0 : this.latched.progress),
      completed: live.completed || this.latched.completed,
      beacon: b,
    };
  }

  private latch(b: Beacon): void {
    this.latched.progress = Math.max(this.latched.progress, Number(b.props.ProgressNormalized ?? 0));
    if (b.props.IsCompleted === true && !this.latched.completed) {
      this.latched.completed = true;
      this.latched.completedAt = Date.now();
      this.say('WATCH: module reported IsCompleted=true');
    }
  }

  /** Background watcher: latches completion and keeps the last full snapshot of the mechanic's beacons. */
  startWatch(periodMs = 150): void {
    if (this.watchTimer) return;
    let busy = false;
    this.watchTimer = setInterval(async () => {
      if (busy || !this.client.isReady) return;
      busy = true;
      try {
        const snap = await this.world.snapshot();
        const mod = snap.get(this.moduleId);
        if (mod && mod.active) {
          this.latch(mod);
          this.latched.beacons.clear();
          this.latched.beacons.set(mod.testId, mod);
          for (const b of snap.within(mod)) this.latched.beacons.set(b.testId, b);
        }
      } catch { /* transient */ }
      busy = false;
    }, periodMs);
  }

  stopWatch(): void {
    if (this.watchTimer) clearInterval(this.watchTimer);
    this.watchTimer = null;
  }

  /** Beacons belonging to the mechanic (inside the module rect), optionally filtered by kind. */
  async parts(kinds?: BeaconKind[], snap?: WorldSnapshot): Promise<Beacon[]> {
    const s = snap ?? await this.snapshot();
    const mod = s.get(this.moduleId);
    if (!mod) return [];
    return s.within(mod, kinds).filter(b => b.testId !== this.moduleId);
  }

  /** Beacons whose live gameplay state is non-empty (filters decorative containers misdetected by name). */
  static isGameplay(b: Beacon): boolean {
    return Object.keys(b.game ?? {}).length > 0;
  }

  static textOf(b: Beacon | undefined | null): string {
    return String(b?.props.Text ?? b?.props.text ?? '');
  }

  /** Waits for module progress to move past `from` (or completion). */
  async awaitProgress(from: number, timeoutMs: number): Promise<ModuleState> {
    const start = Date.now();
    let st = await this.module();
    while (Date.now() - start < timeoutMs) {
      if (st.completed || st.progress > from + 1e-4) return st;
      await sleep(60);
      st = await this.module();
    }
    return st;
  }

  async awaitCompleted(timeoutMs: number): Promise<ModuleState> {
    const start = Date.now();
    let st = await this.module();
    while (!st.completed && Date.now() - start < timeoutMs) {
      await sleep(80);
      st = await this.module();
    }
    return st;
  }

  /** Anti-loop ring buffer (spec §3.7 three-stage protection, stage 1). */
  noteAction(key: string): number {
    this.recentActions.push(key);
    if (this.recentActions.length > 12) this.recentActions.shift();
    return this.recentActions.filter(k => k === key).length;
  }

  /**
   * Stagnation bookkeeping: call after every attempted step with whether the world progressed.
   * Returns true when the playbook must escalate (stuckCounter > 3 → DEADLOCK per spec §3.2).
   */
  progressed(ok: boolean): boolean {
    if (ok) { this.stuckCounter = 0; return false; }
    this.stuckCounter++;
    this.say(`no progress (stuckCounter=${this.stuckCounter})`);
    return this.stuckCounter > 3;
  }

  /** Brings a beacon inside the screen by real mouse-wheel scrolling over its scroll container. */
  async ensureOnScreen(testId: string, scrollAnchor?: Point, maxSteps = 40): Promise<Beacon | null> {
    for (let i = 0; i < maxSteps; i++) {
      const b = await this.world.inspect(testId);
      if (!b) return null;
      const screen = { x: 0, y: 0, w: 1920, h: 1080 };
      // Headers and footers sit at the screen edges: a scrolled control counts as reachable only in the middle
      // band, and only when it is its own top hit (read-only hit test) — an empty hit test is not a confirmation.
      const mx = 40, my = 170;
      if (rectContains({ x: screen.x + mx, y: screen.y + my, w: screen.w - 2 * mx, h: screen.h - 2 * my }, b.center)) {
        const hits = await this.world.hitTest(b.center);
        const top = String(hits[0]?.path ?? '');
        if (top.endsWith(`/${b.name}`) || top.includes(`/${b.name}/`)) return b;
        if (!top && i > 0) return b;     // hit test unavailable: accept after at least one adjustment
      }
      const anchor = scrollAnchor ?? { x: b.center.x, y: 540 };
      // Wheel delta sign: content below the screen centre needs a negative wheel delta (scroll down).
      // Magnitude is proportional to the remaining distance (≈9 px per wheel unit observed), so far-away
      // items are reached in a few real wheel events and near ones are approached gently.
      const distance = Math.abs(b.center.y - 540);
      const delta = Math.sign(540 - b.center.y) * -1 * Math.min(30, Math.max(2, Math.round(distance / 18)));
      await this.client.call('scroll', { x: anchor.x, y: anchor.y, delta_y: delta });
      await sleep(80);
    }
    return this.world.inspect(testId);
  }

  /** Clicks a control and confirms the click produced an observable effect; retries (real input only). */
  async tapUntil(testId: string, effect: () => Promise<boolean>, attempts = 4, settleMs = 250): Promise<boolean> {
    for (let a = 0; a < attempts; a++) {
      const r = await this.motor.tap({ testId });
      if (!r.ok) this.say(`tap ${testId} rejected: ${r.error}`);
      await sleep(settleMs);
      if (await effect()) return true;
    }
    return false;
  }
}
