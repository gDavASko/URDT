/**
 * Shared bootstrap for the CLI and the MCP server: one lazy URDT session + L3 task runner.
 * Logs go to stderr so the MCP stdio channel stays clean.
 */

import path from 'node:path';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { WorldModel } from '../perception/world_model.js';
import { MotorCortex } from '../l1_kinematics/motor_cortex.js';
import { MicroSlmArbiter } from '../l2_tactics/micro_slm_arbiter.js';
import { TaskRunner } from './task_runner.js';

export { ROOT } from './runtime_paths.js';
import { ROOT } from './runtime_paths.js';

export interface UrdtRuntime { client: UrdtWireClient; world: WorldModel; motor: MotorCortex; arbiter: MicroSlmArbiter; runner: TaskRunner }

let rt: UrdtRuntime | null = null;

export function log(msg: string): void {
  process.stderr.write(`[${new Date().toISOString().slice(11, 23)}] ${msg}\n`);
}

export async function getRuntime(opts: { slm?: boolean } = {}): Promise<UrdtRuntime> {
  if (rt) { await rt.client.waitReady(120000); return rt; }
  const client = new UrdtWireClient({ log });
  await client.connect(120000);
  const world = new WorldModel(client);
  const motor = new MotorCortex(client);
  const arbiter = new MicroSlmArbiter();
  if (opts.slm) await arbiter.initialize();
  const runner = new TaskRunner({ client, world, motor, arbiter, outDir: path.join(ROOT, 'URDT_Sandbox', 'tasks'), harnessDir: path.join(ROOT, '.harness'), log });
  rt = { client, world, motor, arbiter, runner };
  return rt;
}

/** Compact scene description for a meta-AI (what L3 sees). */
export async function describeScene(scope?: string): Promise<unknown> {
  const { world } = await getRuntime();
  const snap = await world.snapshot();
  const root = scope ? snap.get(scope) : undefined;
  const list = (root ? snap.within(root) : snap.active()).filter(b => b.visible);
  return {
    windows: snap.openWindows(),
    modules: snap.modules().filter(m => m.visible).map(m => ({ id: m.testId, progress: m.props.ProgressNormalized, completed: m.props.IsCompleted, instruction: m.props.Instruction })),
    beacons: list.slice(0, 150).map(b => ({ id: b.testId, kind: b.kind, role: b.props.AreaType, center: [Math.round(b.center.x), Math.round(b.center.y)], text: b.props.Text, game: b.game })),
  };
}

export function shutdown(): void {
  rt?.runner.dispose();
  rt?.arbiter.dispose();
  rt?.client.close();
  rt = null;
}
