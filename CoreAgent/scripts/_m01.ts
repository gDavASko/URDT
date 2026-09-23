import { getRuntime } from '../src/task/runtime.js';
const { world, motor, client } = await getRuntime();
const sl = (ms: number) => new Promise(r => setTimeout(r, ms));
for (let i = 0; i < 6 && !(await world.inspect('M01_SnapToSlot'))?.visible; i++) {
  const snap = await world.snapshot();
  const b = ['btn_2d_play_catalog', 'btn_launch_m01', 'btn_open_2d_suite'].map(id => snap.get(id)).find(x => x?.visible);
  if (b) { await motor.tap({ testId: b.testId }); await sl(900); }
}
const dump = async (tag: string) => {
  const snap = await world.snapshot(); const sc = snap.get('M01_SnapToSlot')!;
  const items = snap.within(sc).filter(b => /^Item_/.test(b.testId));
  console.log(`== ${tag} stage=${sc.game?.CurrentStage} progress=${sc.props.ProgressNormalized}`);
  for (const it of items) {
    const h: any = (await client.call('hit_test', { testId: it.testId })).data;
    console.log(`  ${it.testId.padEnd(24)} c=(${it.center.x.toFixed(0)},${it.center.y.toFixed(0)}) junk=${it.game?.IsJunk} snapped=${it.props.IsSnapped ?? it.game?.IsSnapped} top=${String(h?.topHit?.path ?? '').split('/').slice(-2).join('/')}`);
  }
};
await dump('stage1');
// solve stage 1 by matching names
for (const [a, b] of [['Item_Circle', 'Slot_Circle'], ['Item_Square', 'Slot_Square'], ['Item_Triangle', 'Slot_Triangle']]) {
  const it = await world.inspect(a), sl2 = await world.inspect(b);
  if (it && sl2) { await motor.drag(it.center, sl2.center, { dwellEndMs: 150 }, 'T'); await sl(400); }
}
await sl(2000);
await dump('stage2');
process.exit(0);
