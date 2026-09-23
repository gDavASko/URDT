/** Vision latency/quality benchmark on a saved screenshot: npx tsx scripts/vision_bench.ts <jpg> <model> <imageMaxTokens> */
import fs from 'node:fs';
import { VisionAnalyst, loadVisionConfig } from '../src/task/vision.js';
const [img, model, maxTok] = process.argv.slice(2);
const cfg = { ...loadVisionConfig()!, active: model, imageMaxTokens: Number(maxTok), port: 8093 };
const v = new VisionAnalyst(cfg, s => console.error(s));
const uri = `data:image/jpeg;base64,${fs.readFileSync(img).toString('base64')}`;
const ids = ['Chip_Fwd_1', 'Chip_Fwd_2', 'Chip_Turn', 'Chip_Glitch_Junk', 'Slot_0', 'Slot_1', 'Slot_2', 'Button_Execute'];
const beacons: any[] = ids.map(id => ({ testId: id, kind: id.startsWith('Slot') ? 'slot' : id.startsWith('Button') ? 'button' : 'draggable', props: {}, center: { x: 0, y: 0 } }));
for (let i = 0; i < 2; i++) {
  const r = await v.analyze(uri, beacons, [], 'Complete the mechanic');
  console.log(`${model} maxTok=${maxTok} run${i + 1}: ${r?.latencyMs}ms plan=${r?.plan.map(s => `${s.action}:${s.item ?? s.button}${s.target ? '>' + s.target : ''}`).join(',')} avoid=${r?.avoid}`);
}
v.dispose();
process.exit(0);
