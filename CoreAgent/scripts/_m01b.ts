import { getRuntime } from '../src/task/runtime.js';
const { world, motor, client } = await getRuntime();
const sl = (ms: number) => new Promise(r => setTimeout(r, ms));
const dump = async (tag: string) => {
  const snap = await world.snapshot(); const sc = snap.get('M01_SnapToSlot')!;
  console.log(`== ${tag} stage=${sc.game?.CurrentStage} fails=${sc.game?.FailCount} reason=${sc.game?.LastFailReason}`);
  for (const it of snap.within(sc).filter(b => /^Item_/.test(b.testId))) {
    const h: any = (await client.call('hit_test', { testId: it.testId })).data;
    console.log(`  ${it.testId.padEnd(24)} c=(${it.center.x.toFixed(0)},${it.center.y.toFixed(0)}) r=${JSON.stringify(it.rect && [Math.round(it.rect.w), Math.round(it.rect.h)])} junk=${it.game?.IsJunk} top=${String(h?.topHit?.path ?? '').split('/').slice(-1)[0]}`);
  }
};
const solve = async () => { for (const [a, b] of [['Item_Circle', 'Slot_Circle'], ['Item_Square', 'Slot_Square'], ['Item_Triangle', 'Slot_Triangle']]) {
  const it = await world.inspect(a), s2 = await world.inspect(b);
  if (it && s2) { await motor.drag(it.center, s2.center, { dwellEndMs: 150 }, 'T'); await sl(500); const m = await world.inspect('M01_SnapToSlot'); console.log(`   drag ${a}: fails=${m?.game?.FailCount} reason=${m?.game?.LastFailReason} prog=${m?.props.ProgressNormalized}`); }
} };
await solve(); await sl(2000);
await dump('stage3');
await solve(); await sl(1500);
await dump('after');
process.exit(0);
