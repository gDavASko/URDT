/** Offline check of the hearing chain (Silero VAD → Qwen3-ASR) on WAV files: npx tsx scripts/hearing_bench.ts <wav...> */
import fs from 'node:fs';
import { GameHearing, loadHearingConfig } from '../src/task/hearing.js';
const cfg = loadHearingConfig()!;
const h = new GameHearing(cfg, null as any, s => console.error(s));
for (const f of process.argv.slice(2)) {
  const b = fs.readFileSync(f);
  const pcm = new Float32Array((b.length - 44) / 2);
  for (let i = 0; i < pcm.length; i++) pcm[i] = b.readInt16LE(44 + i * 2) / 32768;
  // add simulated game music under the voice (sine chords) to exercise the VAD
  const mixed = new Float32Array(pcm.length + 16000 * 2);
  for (let i = 0; i < mixed.length; i++) mixed[i] = 0.08 * Math.sin(2 * Math.PI * 220 * i / 16000) + 0.05 * Math.sin(2 * Math.PI * 330 * i / 16000) + (i >= 16000 && i - 16000 < pcm.length ? pcm[i - 16000] : 0);
  const vad = (h as any).vad; await vad.load();
  const t0 = Date.now();
  const segs = await vad.segments(mixed);
  const texts = [];
  for (const [a, c] of segs) texts.push(await h.transcribe(mixed.subarray(a, c)));
  console.log(`${f.split(/[\/]/).pop()}: ${segs.length} speech segment(s) [${segs.map(([a, c]: number[]) => `${(a / 16000).toFixed(1)}–${(c / 16000).toFixed(1)}s`).join(', ')}] in ${Date.now() - t0}ms → ${JSON.stringify(texts)}`);
}
h.dispose(); process.exit(0);
