/**
 * Beacon inventory of every 2D mechanic (Mode 1 support tool): launches each catalog card through real
 * input and dumps what the agent can perceive. Output: URDT_Sandbox/review/inventory.json (+ compact .txt).
 */
import fs from 'node:fs';
import { UrdtWireClient } from '../src/protocol/urdt_wire_client.js';
import { WorldModel, sleep } from '../src/perception/world_model.js';
import { MotorCortex } from '../src/l1_kinematics/motor_cortex.js';
import { PlaybookContext } from '../src/l2_tactics/playbook_context.js';

const only = process.argv[2]?.split(',');
const client = new UrdtWireClient();
await client.connect(60000);
const world = new WorldModel(client);
const motor = new MotorCortex(client);
const out: Record<string, any> = {};
const lines: string[] = [];

async function toCatalog(): Promise<void> {
  for (let i = 0; i < 6; i++) {
    const s = await world.snapshot();
    if (s.get('btn_launch_m01')?.active) return;
    for (const id of ['btn_2d_play_catalog', 'btn_open_2d_suite']) {
      if (s.get(id)?.visible && s.get(id)?.active) { await motor.tap({ testId: id }); await sleep(500); break; }
    }
  }
}

for (let n = 1; n <= 32; n++) {
  const card = `btn_launch_m${String(n).padStart(2, '0')}`;
  if (only && !only.includes(String(n))) continue;
  await toCatalog();
  const fake: any = { id: card, timeoutMs: 1000, suite: '2d', module: '' };
  const ctx = new PlaybookContext(client, world, motor, null as any, fake, false);
  await ctx.ensureOnScreen(card);
  await motor.tap({ testId: card });
  await sleep(1200);
  const snap = await world.snapshot();
  const mod = snap.modules().find(m => m.visible);
  if (!mod) { lines.push(`#${n}: module not visible`); continue; }
  const parts = snap.within(mod).map(b => ({ id: b.testId, kind: b.kind, role: b.props.AreaType ?? '', c: [Math.round(b.center.x), Math.round(b.center.y)], r: b.rect ? [Math.round(b.rect.w), Math.round(b.rect.h)] : null, vis: b.visible, text: b.props.Text, game: b.game }));
  out[mod.testId] = { module: { rect: mod.rect, game: mod.game, instruction: mod.props.Instruction }, parts };
  lines.push(`\n## ${mod.testId}  rect=${JSON.stringify(mod.rect)}  game=${JSON.stringify(mod.game)}`);
  for (const p of parts) {
    const g = Object.entries(p.game ?? {}).filter(([k]) => !['RectTransform'].includes(k)).map(([k, v]) => `${k}=${typeof v === 'object' ? JSON.stringify(v) : v}`).join(' ');
    lines.push(`  ${p.id.padEnd(28)} ${p.kind.padEnd(9)} ${String(p.role).padEnd(18)} c=${p.c.join(',')} ${p.r ? `s=${p.r.join('x')}` : ''} ${p.vis ? '' : 'HIDDEN'} ${p.text ? `"${String(p.text).slice(0, 50)}"` : ''} ${g}`.slice(0, 400));
  }
}
fs.mkdirSync('../URDT_Sandbox/review', { recursive: true });
fs.writeFileSync('../URDT_Sandbox/review/inventory.json', JSON.stringify(out, null, 1));
fs.writeFileSync('../URDT_Sandbox/review/inventory.txt', lines.join('\n'));
console.log(`inventory: ${Object.keys(out).length} mechanics`);
client.close();
process.exit(0);
