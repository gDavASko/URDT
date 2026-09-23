/**
 * Runs a synthesized skill (controller.ts written by the meta-AI) with a restricted API.
 *
 * Contract for controller authors (also sent in every skill request):
 *
 *   export default async function (api) {
 *     while (!api.expired()) {
 *       const s = await api.state();            // scope beacon game state (public properties of the mechanic)
 *       if (s.IsCompleted) return 'COMPLETED';
 *       if (s.IsInTransition) { await api.sleep(100); continue; }
 *       const parts = await api.parts();        // beacons in the scope: { testId, kind, center{x,y}, rect, game, props, visible }
 *       ...
 *       await api.motor.tap({ testId: 'BtnLeft' });  // or tap({ point:{x,y} }), drag(from,to), hold(point,{maxMs}), press/release
 *     }
 *     return 'TIMEOUT';
 *   }
 *
 * Screen space is Unity's: origin bottom-left, y up. No game methods can be called — only honest input.
 */

import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { Point, sleep } from '../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../l2_tactics/playbook_context.js';

export interface ControllerApi {
  moduleId: string;
  params: Record<string, unknown>;
  state(): Promise<Record<string, any>>;
  parts(): Promise<Array<{ testId: string; kind: string; center: Point; rect?: { x: number; y: number; w: number; h: number }; game: Record<string, any>; props: Record<string, any>; visible: boolean }>>;
  inspect(testId: string): Promise<any>;
  motor: {
    tap(t: { testId?: string; point?: Point }, holdMs?: number): Promise<void>;
    drag(from: Point, to: Point, opts?: { durationMs?: number; dwellEndMs?: number }): Promise<void>;
    hold(p: Point, opts: { maxMs: number }): Promise<void>;
    press(p: Point, pointerId?: number): Promise<void>;
    release(p: Point, pointerId?: number): Promise<void>;
    slice(points: Point[], opts?: { speedPxPerS?: number }): Promise<void>;
  };
  sleep(ms: number): Promise<void>;
  expired(): boolean;
  say(msg: string): void;
}

export async function runController(file: string, ctx: PlaybookContext, params: Record<string, unknown> = {}): Promise<PlaybookResult> {
  let mod: any;
  try {
    mod = await import(pathToFileURL(path.resolve(file)).href + `?v=${Date.now()}`);
  } catch (err) {
    return { status: 'FAILED', summary: `controller does not load: ${(err as Error).message}` };
  }
  const fn = mod?.default;
  if (typeof fn !== 'function') return { status: 'FAILED', summary: 'controller has no default export function' };
  const api: ControllerApi = {
    moduleId: ctx.moduleId,
    params,
    state: async () => {
      const b = await ctx.world.inspect(ctx.moduleId);
      return { ...(b?.game ?? {}), IsCompleted: b?.props.IsCompleted === true, ProgressNormalized: b?.props.ProgressNormalized };
    },
    parts: async () => (await ctx.parts()).map(b => ({ testId: b.testId, kind: b.kind, center: b.center, rect: b.rect ?? undefined, game: b.game ?? {}, props: b.props, visible: b.visible })),
    inspect: (id: string) => ctx.world.inspect(id),
    motor: {
      tap: async (t, holdMs = 16) => { await ctx.motor.tap(t, holdMs); },
      drag: async (from, to, opts = {}) => { await ctx.motor.drag(from, to, { durationMs: opts.durationMs ?? 250, dwellEndMs: opts.dwellEndMs ?? 60 }, 'SKILL'); },
      hold: async (p, opts) => { await ctx.motor.hold(p, { maxMs: opts.maxMs }); },
      press: (p, id = 0) => ctx.motor.press(p, id),
      release: (p, id = 0) => ctx.motor.release(p, id),
      slice: async (pts, opts = {}) => { await ctx.motor.slice(pts, opts); },
    },
    sleep,
    expired: () => ctx.expired(),
    say: (m: string) => ctx.say(`[skill] ${m}`),
  };
  try {
    const r = await fn(api);
    const done = (await ctx.world.inspect(ctx.moduleId))?.props.IsCompleted === true;
    if (done) return { status: 'COMPLETED', summary: 'module completed by synthesized controller' };
    return { status: String(r ?? '') === 'COMPLETED' ? 'STUCK' : 'TIMEOUT', summary: `controller returned ${String(r)} without completing the module` } as PlaybookResult;
  } catch (err) {
    return { status: 'FAILED', summary: `controller threw: ${(err as Error).message}` };
  }
}
