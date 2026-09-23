/**
 * Campaign run: L3 presses the game's "run all mechanics" button and plays every module in the order the game
 * presents it, reading the design document (GDD) section of each module at its briefing.
 *   npx tsx scripts/campaign.ts [gddPath] [minutes]
 */
import fs from 'node:fs';
import path from 'node:path';
import { getRuntime, shutdown, ROOT } from '../src/task/runtime.js';

process.on('unhandledRejection', e => console.error('[campaign] unhandled rejection:', (e as Error)?.message ?? e));
const gddPath = process.argv[2] ?? 'Docs/GDD/URDT_Polygon_Game_GDD.md';
const minutes = Number(process.argv[3] ?? 120);
const { runner } = await getRuntime();
const taskId = `campaign_${new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19)}`;
const r: any = await runner.run({
  taskId,
  goal: 'Play through the whole game: complete every mechanic with all its stages in the automatic run',
  context: { gddPath, designNotes: 'modules=37' },
  target: { mode: 'campaign', entry: ['btn_2d_run_sequential'] },
  success: { all: [] },
  autonomy: 'full',
  budget: { timeMs: minutes * 60000, actions: 20000 },
  audit: false,
});
const rows = (r.campaign ?? []) as Array<any>;
const md = [`# Campaign ${taskId}`, '', `GDD: \`${gddPath}\``, '', `**${r.summary}** in ${(r.durationMs / 60000).toFixed(1)} min`, '',
  '| module | status | s | actions | stage | fails |', '|---|---|---|---|---|---|',
  ...rows.map(p => `| ${p.module} | ${p.status} | ${p.seconds} | ${p.actions} | ${p.stages} | ${p.fails} |`)].join('\n');
const out = path.join(ROOT, 'URDT_Sandbox', 'experiments', `${taskId}.md`);
fs.writeFileSync(out, md);
console.log(md);
shutdown();
process.exit(0);
