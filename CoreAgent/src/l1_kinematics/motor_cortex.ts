/**
 * L1 Motor Cortex — maps the 9 motor primitives of the Game & Testing spec onto real URDT wire commands.
 *
 * Kinematics are planned agent-side (Flash & Hogan minimum-jerk profile + Gaussian micro-tremor + dwell
 * frames) and executed in-engine one pointer state per Unity frame (`drag {path, steps:1}`), which is the
 * "in-engine batch execution" branch of spec §2.5: the game observes a human-like per-frame trajectory
 * without network jitter. Holds use pointer_down / pointer_up spanning real time.
 */

import { UrdtWireClient, UrdtCallError } from '../protocol/urdt_wire_client.js';
import { FlashHoganTicker } from './flash_hogan.js';
import { Point, sleep } from '../perception/world_model.js';

export const FRAME_MS = 1000 / 60;

export interface DragOptions {
  durationMs?: number;          // travel time (min-jerk)
  dwellStartMs?: number;        // hold still after press before moving (pick-up)
  dwellEndMs?: number;          // hold still at destination before release (settle / snap)
  via?: Point[];                // intermediate waypoints (obstacle avoidance, curved paths)
  tremorPx?: number;            // physiological micro-jitter sigma
  pointerId?: number;           // 0 = mouse, 1..10 = touch
  fromTestId?: string;          // lets the server verify the source is the top hit
}

export interface MotorTraceEntry {
  t: number;
  primitive: string;
  target?: string;
  from?: Point;
  to?: Point;
  frames?: number;
  ok: boolean;
  error?: string;
  detail?: any;
}

export class MotorCortex {
  public readonly trace: MotorTraceEntry[] = [];
  /** Measured engine frame time: frame-paced gestures are sampled per real game frame. */
  public frameMs = FRAME_MS;
  private lastCalibration = 0;

  constructor(private readonly client: UrdtWireClient) {}

  /** Median of several input_status samples (the Editor often runs uncapped, e.g. 600 FPS). */
  public async calibrate(force = false): Promise<number> {
    if (!force && Date.now() - this.lastCalibration < 15000) return this.frameMs;
    const samples: number[] = [];
    for (let i = 0; i < 5; i++) {
      const st = await this.client.call('input_status', {}, 3000).catch(() => null);
      if (st?.status === 'ok' && st.data?.frame_ms > 0) samples.push(st.data.frame_ms);
      await sleep(20);
    }
    if (samples.length) {
      samples.sort((a, b) => a - b);
      this.frameMs = Math.min(50, Math.max(1, samples[Math.floor(samples.length / 2)]));
    }
    this.lastCalibration = Date.now();
    return this.frameMs;
  }

  private record(entry: Omit<MotorTraceEntry, 't'>): MotorTraceEntry {
    const e = { t: Date.now(), ...entry };
    this.trace.push(e);
    if (this.trace.length > 400) this.trace.shift();
    return e;
  }

  /** Minimum-jerk samples between two points, one per frame, with tremor on interior samples. */
  public static minimumJerkPath(from: Point, to: Point, durationMs: number, tremorPx = 0.8, frameMs = FRAME_MS): Point[] {
    const n = Math.max(3, Math.round(durationMs / frameMs));
    const pts: Point[] = [];
    for (let i = 0; i <= n; i++) {
      const s = FlashHoganTicker.calculateMinimumJerk(i / n);
      const jitter = i > 0 && i < n && tremorPx > 0 ? FlashHoganTicker.generateTremor(tremorPx) : { dx: 0, dy: 0 };
      pts.push({ x: from.x + (to.x - from.x) * s + jitter.dx, y: from.y + (to.y - from.y) * s + jitter.dy });
    }
    return pts;
  }

  /** Fitts's-law movement time (ms) for a target of width w at distance d (a=120, b=110). */
  public static fittsMs(d: number, w: number): number {
    return 120 + 110 * Math.log2(d / Math.max(8, w) + 1);
  }

  public buildDragPath(from: Point, to: Point, opts: DragOptions = {}): Point[] {
    const stops = [from, ...(opts.via ?? []), to];
    const total = stops.slice(1).reduce((acc, p, i) => acc + Math.hypot(p.x - stops[i].x, p.y - stops[i].y), 0);
    const duration = opts.durationMs ?? Math.min(900, Math.max(250, MotorCortex.fittsMs(total, 60)));
    const path: Point[] = [];
    const hold = (p: Point, ms: number) => { for (let i = 0; i < Math.round(ms / this.frameMs); i++) path.push({ ...p }); };
    path.push({ ...from });
    hold(from, opts.dwellStartMs ?? 50);
    for (let i = 1; i < stops.length; i++) {
      const segLen = Math.hypot(stops[i].x - stops[i - 1].x, stops[i].y - stops[i - 1].y);
      const segMs = total > 0 ? duration * (segLen / total) : duration;
      path.push(...MotorCortex.minimumJerkPath(stops[i - 1], stops[i], Math.max(this.frameMs * 3, segMs), opts.tremorPx ?? 0.8, this.frameMs).slice(1));
    }
    hold(to, opts.dwellEndMs ?? 80);
    return path.map(p => ({ x: Math.round(p.x * 10) / 10, y: Math.round(p.y * 10) / 10 }));
  }

  /** Compact a path for the wire: identical consecutive samples still count as frames (dwell). */
  private static round(path: Point[]): Point[] {
    return path.map(p => ({ x: Math.round(p.x * 10) / 10, y: Math.round(p.y * 10) / 10 }));
  }

  /** TAP / click on a beacon (preferred: server resolves the live, top-hit point) or a raw point. */
  public async tap(target: { testId?: string; point?: Point }, holdMs = 50, pointerId = 0): Promise<MotorTraceEntry> {
    const payload: Record<string, unknown> = target.testId ? { testId: target.testId } : { x: target.point!.x, y: target.point!.y };
    payload.hold_ms = holdMs;
    if (pointerId > 0) payload.pointer_id = pointerId;
    try {
      const data = await this.client.request('click', payload, 8000);
      return this.record({ primitive: 'TAP', target: target.testId, to: target.point ?? data?.screenPosition, ok: true, detail: data });
    } catch (err) {
      return this.record({ primitive: 'TAP', target: target.testId, to: target.point, ok: false, error: (err as Error).message });
    }
  }

  public async doubleTap(testId: string): Promise<MotorTraceEntry> {
    try {
      const data = await this.client.request('double_click', { testId }, 8000);
      return this.record({ primitive: 'DOUBLE_TAP', target: testId, ok: true, detail: data });
    } catch (err) {
      return this.record({ primitive: 'DOUBLE_TAP', target: testId, ok: false, error: (err as Error).message });
    }
  }

  /** DRAG / SLICE: a full press → trajectory → release gesture executed frame by frame in Unity. */
  public async drag(from: Point, to: Point, opts: DragOptions = {}, label?: string): Promise<MotorTraceEntry> {
    await this.calibrate();
    const path = this.buildDragPath(from, to, opts);
    return this.tracePath(path, { ...opts, label: label ?? 'DRAG', from, to });
  }

  public async slice(waypoints: Point[], opts: DragOptions & { speedPxPerS?: number } = {}): Promise<MotorTraceEntry> {
    await this.calibrate();
    const path: Point[] = [waypoints[0]];
    const speed = opts.speedPxPerS ?? 700;
    for (let i = 1; i < waypoints.length; i++) {
      const d = Math.hypot(waypoints[i].x - waypoints[i - 1].x, waypoints[i].y - waypoints[i - 1].y);
      const n = Math.max(1, Math.round((d / speed) * 1000 / this.frameMs));
      for (let k = 1; k <= n; k++) {
        const t = k / n;
        const j = FlashHoganTicker.generateTremor(opts.tremorPx ?? 0.5);
        path.push({ x: waypoints[i - 1].x + (waypoints[i].x - waypoints[i - 1].x) * t + j.dx, y: waypoints[i - 1].y + (waypoints[i].y - waypoints[i - 1].y) * t + j.dy });
      }
    }
    for (let i = 0; i < Math.round((opts.dwellEndMs ?? 60) / this.frameMs); i++) path.push({ ...waypoints[waypoints.length - 1] });
    return this.tracePath(path, { ...opts, label: 'SLICE', from: waypoints[0], to: waypoints[waypoints.length - 1] });
  }

  private async tracePath(path: Point[], o: DragOptions & { label: string; from: Point; to: Point }): Promise<MotorTraceEntry> {
    // pacing:"realtime" → the engine plays one pointer state per game frame (human-like timing: velocities,
    // dwell and hold timers advance in game time). Without it URDT compresses the gesture into one frame.
    const payload: Record<string, unknown> = { path: MotorCortex.round(path), steps: 1, pacing: 'realtime' };
    if (o.pointerId && o.pointerId > 0) payload.pointer_id = o.pointerId;
    try {
      const started = Date.now();
      const data = await this.client.request('drag', payload, 20000);
      const frames = await this.awaitGestureDone(data?.queued_frames ?? path.length, data?.frame_ms);
      return this.record({ primitive: o.label, from: o.from, to: o.to, frames: path.length, ok: true, detail: { queued: data?.queued_frames, pacing: data?.input_transaction, gameFrames: frames, wallMs: Date.now() - started } });
    } catch (err) {
      return this.record({ primitive: o.label, from: o.from, to: o.to, frames: path.length, ok: false, error: (err as Error).message });
    }
  }

  /** Waits until a frame-paced gesture has been fully consumed by the engine (polls input_status). */
  private async awaitGestureDone(queued: number, frameMs?: number): Promise<number> {
    const perFrame = frameMs && frameMs > 0 ? frameMs : this.frameMs;
    await sleep(Math.max(0, queued * perFrame * 0.85));
    for (let i = 0; i < 400; i++) {
      const st = await this.client.call('input_status', {}, 3000).catch(() => null);
      if (!st || st.status !== 'ok') return queued;        // older URDT build: fall back to the time estimate
      if ((st.data?.pending_frames ?? 0) === 0) return queued;
      await sleep(Math.max(16, (st.data.pending_frames as number) * perFrame * 0.5));
    }
    return queued;
  }

  /** HOLD_EVENT_CONDITIONED: press, keep holding until `until()` is true or timeout, release. */
  public async hold(point: Point, opts: { maxMs: number; until?: () => Promise<boolean>; pollMs?: number; pointerId?: number; testId?: string }): Promise<MotorTraceEntry & { heldMs: number; conditionMet: boolean }> {
    const pointerId = opts.pointerId ?? 0;
    const down: Record<string, unknown> = opts.testId ? { testId: opts.testId } : { x: point.x, y: point.y };
    if (pointerId > 0) down.pointerId = pointerId;
    const started = Date.now();
    let conditionMet = false;
    try {
      await this.client.request('pointer_down', down, 5000);
      while (Date.now() - started < opts.maxMs) {
        if (opts.until && await opts.until()) { conditionMet = true; break; }
        await sleep(opts.pollMs ?? 40);
      }
    } catch (err) {
      await this.release(point, pointerId);
      const e = this.record({ primitive: 'HOLD', target: opts.testId, to: point, ok: false, error: (err as Error).message });
      return { ...e, heldMs: Date.now() - started, conditionMet };
    }
    await this.release(point, pointerId);
    const e = this.record({ primitive: 'HOLD', target: opts.testId, to: point, ok: true, detail: { heldMs: Date.now() - started, conditionMet } });
    return { ...e, heldMs: Date.now() - started, conditionMet };
  }

  public async press(point: Point, pointerId = 0): Promise<void> {
    const p: Record<string, unknown> = { x: point.x, y: point.y };
    if (pointerId > 0) p.pointerId = pointerId;
    await this.client.request('pointer_down', p, 5000);
    this.record({ primitive: 'POINTER_DOWN', to: point, ok: true });
  }

  public async release(point: Point, pointerId = 0): Promise<void> {
    const p: Record<string, unknown> = { x: point.x, y: point.y };
    if (pointerId > 0) p.pointerId = pointerId;
    try {
      await this.client.request('pointer_up', p, 5000);
      this.record({ primitive: 'POINTER_UP', to: point, ok: true });
    } catch (err) {
      this.record({ primitive: 'POINTER_UP', to: point, ok: false, error: (err as UrdtCallError).message });
    }
  }

  public async key(key: string): Promise<MotorTraceEntry> {
    try {
      await this.client.request('key_press', { key }, 5000);
      return this.record({ primitive: 'KEY', target: key, ok: true });
    } catch (err) {
      return this.record({ primitive: 'KEY', target: key, ok: false, error: (err as Error).message });
    }
  }

  public async typeText(text: string): Promise<MotorTraceEntry> {
    try {
      await this.client.request('type_text', { text }, 8000);
      return this.record({ primitive: 'TYPE', target: text, ok: true });
    } catch (err) {
      return this.record({ primitive: 'TYPE', target: text, ok: false, error: (err as Error).message });
    }
  }

  public async scroll(testId: string, deltaY: number): Promise<MotorTraceEntry> {
    try {
      await this.client.request('scroll', { testId, delta_y: deltaY }, 5000);
      return this.record({ primitive: 'SCROLL', target: testId, ok: true });
    } catch (err) {
      return this.record({ primitive: 'SCROLL', target: testId, ok: false, error: (err as Error).message });
    }
  }

  public async swipe(testId: string, direction: 'up' | 'down' | 'left' | 'right', distance: number): Promise<MotorTraceEntry> {
    try {
      await this.client.request('swipe', { testId, direction, distance, steps: 8 }, 8000);
      return this.record({ primitive: 'SWIPE', target: testId, ok: true });
    } catch (err) {
      return this.record({ primitive: 'SWIPE', target: testId, ok: false, error: (err as Error).message });
    }
  }
}
