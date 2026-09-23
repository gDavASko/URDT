/**
 * Plays the given modules one by one (single mode) with the GDD section as design notes.
 *   npx tsx scripts/try_modules.ts M33_Arkanoid,M34_Snake [gddPath] [secondsPerModule]
 */
import { getRuntime, shutdown } from '../src/task/runtime.js';

process.on('unhandledRejection', e => console.error('[try] unhandled rejection:', (e as Error)?.message ?? e));
const ids = (process.argv[2] ?? '').split(',').filter(Boolean);
const gddPath = process.argv[3] ?? 'Docs/GDD/URDT_Polygon_Game_GDD.md';
const seconds = Number(process.argv[4] ?? 240);
const { runner } = await getRuntime();
const rows: string[] = [];
for (const id of ids) {
  const r: any = await runner.run({
    taskId: `try_${id}`, goal: `Complete every stage of ${id}`,
    context: { module: id, gddPath },
    target: { scope: id },
    success: { all: [{ beacon: '@scope', path: 'IsCompleted', op: '==', value: true }] },
    autonomy: 'full', budget: { timeMs: seconds * 1000, actions: 800 }, audit: false,
  }).catch((e: Error) => ({ status: 'error', summary: e.message, durationMs: 0, actions: 0, findings: [] }));
  const line = `${id.padEnd(22)} ${String(r.status).padEnd(8)} ${(r.durationMs / 1000).toFixed(1)}s ${r.actions} actions | ${r.summary} | ${(r.findings ?? []).filter((f: any) => f.severity !== 'INFO').map((f: any) => f.kind).join(',')}`;
  console.log(line);
  rows.push(line);
}
console.log('SUMMARY\n' + rows.join('\n'));
shutdown();
process.exit(0);
