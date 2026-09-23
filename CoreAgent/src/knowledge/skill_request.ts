/**
 * Skill request: when L3 cannot complete a module with its skills and exploration, it prepares everything a
 * meta-AI needs to write a controller for it — observed at NORMAL game speed (never by changing time scale:
 * games use unscaled/realtime timers, so a slowed game is not the game the player gets).
 *
 * Written to <game>/URDT_Knowledge/skills/requests/<module>.json and returned in the task result.
 */

import fs from 'node:fs';
import path from 'node:path';
import { WorldModel, sleep } from '../perception/world_model.js';

const CONTROLLER_API = `export default async function (api) {
  // api.state()   -> Promise<object>: public state of the mechanic (the scope beacon's game.* + IsCompleted, ProgressNormalized)
  // api.parts()   -> Promise<Array<{testId, kind, center:{x,y}, rect:{x,y,w,h}, game, props, visible}>> beacons in the scope
  // api.inspect(testId), api.sleep(ms), api.expired(), api.say(msg), api.params
  // api.motor.tap({testId}|{point:{x,y}}, holdMs?), drag(from, to, {durationMs, dwellEndMs}), hold(point, {maxMs}),
  //          press(point, pointerId?), release(point, pointerId?), slice(points[], {speedPxPerS})
  // Screen space: Unity's (origin bottom-left, y up). Only honest input — never call game code.
  // Loop until api.state().IsCompleted; wait while IsInTransition (stage change / fail restart).
  return 'COMPLETED' | 'TIMEOUT';
}`;

export async function buildSkillRequest(opts: {
  world: WorldModel; scopeId: string; goal: string; knowledgeDir: string; design: string;
  strategy: Array<{ step: string; outcome: string }>; failReasons: string[];
}): Promise<string> {
  const { world, scopeId } = opts;
  // Observe dynamics without acting: 15 samples over 3 s.
  const samples: Array<{ t: number; state: Record<string, unknown>; moving: Record<string, [number, number]> }> = [];
  let prev = await world.snapshot();
  const t0 = Date.now();
  for (let i = 0; i < 15; i++) {
    await sleep(200);
    const snap = await world.snapshot();
    const scope = snap.get(scopeId);
    const moving: Record<string, [number, number]> = {};
    for (const b of scope ? snap.within(scope) : []) {
      const p = prev.get(b.testId);
      if (p && Math.hypot(b.center.x - p.center.x, b.center.y - p.center.y) > 2) moving[b.testId] = [Math.round(b.center.x), Math.round(b.center.y)];
    }
    const state = Object.fromEntries(Object.entries(scope?.game ?? {}).filter(([, v]) => typeof v !== 'object'));
    samples.push({ t: Date.now() - t0, state, moving });
    prev = snap;
  }
  const snap = await world.snapshot();
  const scope = snap.get(scopeId);
  const parts = scope ? snap.within(scope).filter(b => b.testId !== scopeId) : [];
  const changing = Object.keys(samples[0]?.state ?? {}).filter(k => samples.some(s => JSON.stringify(s.state[k]) !== JSON.stringify(samples[0].state[k])));
  const request = {
    module: scopeId,
    goal: opts.goal,
    createdAt: new Date().toISOString(),
    realtime: samples.some(s => Object.keys(s.moving).length > 0) || changing.length > 0,
    instruction: scope?.props.Instruction,
    design: opts.design.slice(0, 6000),
    state: { keys: Object.keys(scope?.game ?? {}), changingWithoutInput: changing, samples },
    beacons: parts.slice(0, 120).map(b => ({ id: b.testId, kind: b.kind, role: b.props.AreaType, center: [Math.round(b.center.x), Math.round(b.center.y)], size: b.rect ? [Math.round(b.rect.w), Math.round(b.rect.h)] : null, game: b.game })),
    attempts: opts.strategy.slice(-40),
    failReasons: [...new Set(opts.failReasons)].slice(0, 20),
    controllerApi: CONTROLLER_API,
    submit: 'MCP urdt_submit_skill { name, description, requires: { scopeGame?: string[], beacons?: string[], buttons?: string[] }, source } — then re-run the task; the candidate runs first when its requirements match.',
  };
  const dir = path.join(opts.knowledgeDir, 'skills', 'requests');
  fs.mkdirSync(dir, { recursive: true });
  const file = path.join(dir, `${scopeId}.json`);
  fs.writeFileSync(file, JSON.stringify(request, null, 2));
  return file;
}
