/**
 * Hearing (auxiliary): voice hints and sound cues from the game's own audio mix.
 *
 *   URDT `audio` (16 kHz mono PCM of the AudioListener mix + AudioSource play events)
 *     → Silero VAD (onnxruntime-node) finds speech segments inside music/SFX
 *     → Qwen3-ASR (llama.cpp llama-server, 30 languages incl. Russian) transcribes each segment
 *     → transcripts become briefing texts ("[voice] …"); play events become sound cues.
 *
 * Config: config/agent.config.json → "hearing". Never in the control loop: used at briefing and when stuck.
 */

import fs from 'node:fs';
import path from 'node:path';
import { spawn, ChildProcess } from 'node:child_process';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { sleep } from '../perception/world_model.js';
import { ROOT } from './runtime_paths.js';

export interface HearingConfig {
  enabled: boolean; active: string; backend: string;
  servers: Record<string, string>; models: Record<string, { model: string; mmproj: string }>;
  vadModel: string; port: number; gpuLayers: number; timeoutMs: number; language?: string;
}

export interface HeardSpeech { fromSample: number; toSample: number; text: string; latencyMs: number }
export interface SoundCue { sample: number; source: string; clip: string }
export interface HearingResult { speech: HeardSpeech[]; cues: SoundCue[]; cursor: number; audioSeconds: number; peak: number }

export function loadHearingConfig(): HearingConfig | null {
  try {
    const cfg = JSON.parse(fs.readFileSync(path.join(ROOT, 'CoreAgent', 'config', 'agent.config.json'), 'utf-8'));
    return cfg.hearing?.enabled ? cfg.hearing as HearingConfig : null;
  } catch { return null; }
}

/** Silero VAD v5 (16 kHz, 512-sample windows, recurrent state 2×1×128). */
class SileroVad {
  private session: any = null;
  private ort: any = null;
  constructor(private readonly modelPath: string) {}

  async load(): Promise<boolean> {
    if (this.session) return true;
    if (!fs.existsSync(this.modelPath)) return false;
    this.ort = await import('onnxruntime-node');
    this.session = await this.ort.InferenceSession.create(this.modelPath);
    return true;
  }

  /** Returns speech segments [start, end) in samples. */
  async segments(pcm: Float32Array, threshold = 0.5, minSpeechMs = 250, minSilenceMs = 400, padMs = 200): Promise<Array<[number, number]>> {
    const win = 512, sr = 16000;
    let state = new this.ort.Tensor('float32', new Float32Array(2 * 128), [2, 1, 128]);
    const srT = new this.ort.Tensor('int64', BigInt64Array.from([BigInt(sr)]), []);
    const probs: number[] = [];
    // Silero v5 at 16 kHz expects each 512-sample window prefixed by the last 64 samples of the previous one;
    // without this context the speech probability stays ≈0.
    const CTX = 64;
    let context = new Float32Array(CTX);
    for (let i = 0; i + win <= pcm.length; i += win) {
      const chunk = new Float32Array(CTX + win);
      chunk.set(context, 0);
      chunk.set(pcm.subarray(i, i + win), CTX);
      const out = await this.session.run({ input: new this.ort.Tensor('float32', chunk, [1, CTX + win]), state, sr: srT });
      probs.push(out.output.data[0]);
      state = out.stateN;
      context = chunk.slice(chunk.length - CTX);
    }
    const segs: Array<[number, number]> = [];
    let start = -1, silence = 0;
    const minSil = Math.ceil((minSilenceMs / 1000) * sr / win);
    for (let k = 0; k < probs.length; k++) {
      if (probs[k] >= threshold) { if (start < 0) start = k; silence = 0; }
      else if (start >= 0 && ++silence >= minSil) { segs.push([start * win, (k - silence + 1) * win]); start = -1; silence = 0; }
    }
    if (start >= 0) segs.push([start * win, probs.length * win]);
    const pad = Math.round((padMs / 1000) * sr);
    return segs.filter(([a, b]) => (b - a) / sr * 1000 >= minSpeechMs).map(([a, b]) => [Math.max(0, a - pad), Math.min(pcm.length, b + pad)]);
  }
}

function wav(pcm: Float32Array, sr = 16000): Buffer {
  const buf = Buffer.alloc(44 + pcm.length * 2);
  buf.write('RIFF', 0); buf.writeUInt32LE(36 + pcm.length * 2, 4); buf.write('WAVE', 8); buf.write('fmt ', 12);
  buf.writeUInt32LE(16, 16); buf.writeUInt16LE(1, 20); buf.writeUInt16LE(1, 22); buf.writeUInt32LE(sr, 24);
  buf.writeUInt32LE(sr * 2, 28); buf.writeUInt16LE(2, 32); buf.writeUInt16LE(16, 34); buf.write('data', 36); buf.writeUInt32LE(pcm.length * 2, 40);
  for (let i = 0; i < pcm.length; i++) buf.writeInt16LE(Math.max(-32768, Math.min(32767, Math.round(pcm[i] * 32767))), 44 + i * 2);
  return buf;
}

export class GameHearing {
  private proc: ChildProcess | null = null;
  private ready = false;
  private readonly vad: SileroVad;
  public cursor = 0;

  constructor(private readonly cfg: HearingConfig, private readonly client: UrdtWireClient, private readonly log: (s: string) => void) {
    this.vad = new SileroVad(cfg.vadModel);
  }

  get url(): string { return `http://127.0.0.1:${this.cfg.port}`; }

  private async healthy(): Promise<boolean> {
    try { return (await fetch(`${this.url}/health`)).ok; } catch { return false; }
  }

  async ensureServer(): Promise<boolean> {
    if (this.ready || await this.healthy()) { this.ready = true; return true; }
    const exe = this.cfg.servers[this.cfg.backend];
    const m = this.cfg.models[this.cfg.active];
    if (!exe || !fs.existsSync(exe) || !m || !fs.existsSync(m.model) || !fs.existsSync(m.mmproj)) {
      this.log(`[hearing] unavailable: missing server/model for ${this.cfg.active}`);
      return false;
    }
    this.log(`[hearing] starting llama-server (${this.cfg.active}, ${this.cfg.backend})`);
    this.proc = spawn(exe, ['-m', m.model, '--mmproj', m.mmproj, '--port', String(this.cfg.port), '--host', '127.0.0.1', '-ngl', String(this.cfg.gpuLayers), '-c', '4096', '--no-webui'], { stdio: 'ignore', windowsHide: true });
    this.proc.on('exit', () => { this.ready = false; });
    for (let i = 0; i < 120; i++) { if (await this.healthy()) { this.ready = true; this.log('[hearing] ready'); return true; } await sleep(1000); }
    return false;
  }

  /** Current end of the game audio stream (to listen only to what comes next). */
  async mark(): Promise<number> {
    const r = await this.client.call('audio', { pcm: false, max_seconds: 0 }, 5000).catch(() => null);
    this.cursor = Number(r?.data?.to_sample ?? 0);
    return this.cursor;
  }

  async transcribe(pcm: Float32Array): Promise<string> {
    if (!(await this.ensureServer())) return '';
    const res = await fetch(`${this.url}/v1/chat/completions`, {
      method: 'POST', headers: { 'content-type': 'application/json' }, signal: AbortSignal.timeout(this.cfg.timeoutMs),
      body: JSON.stringify({
        temperature: 0, max_tokens: 200,
        messages: [{ role: 'user', content: [{ type: 'input_audio', input_audio: { data: wav(pcm).toString('base64'), format: 'wav' } }] }],
      }),
    });
    const data: any = await res.json();
    const text = String(data?.choices?.[0]?.message?.content ?? '');
    // Qwen3-ASR answers "language <lang><asr_text>…" — keep the transcript only.
    return text.replace(/^.*?<asr_text>/s, '').replace(/<\/?[a-z_]+>/g, '').replace(/^language\s+\w+\s*/i, '').trim();
  }

  /** Listens to everything since `since` (or the last `seconds`), returns transcribed speech and sound cues. */
  async listen(opts: { since?: number; seconds?: number } = {}): Promise<HearingResult> {
    const payload: Record<string, unknown> = { max_seconds: opts.seconds ?? 20 };
    if (opts.since !== undefined) payload.since_sample = opts.since;
    const r = await this.client.call('audio', payload, 10000).catch(() => null);
    const d = r?.data;
    if (!d) return { speech: [], cues: [], cursor: this.cursor, audioSeconds: 0, peak: 0 };
    const bytes = Buffer.from(String(d.pcm16_b64 ?? ''), 'base64');
    const pcm = new Float32Array(bytes.length / 2);
    for (let i = 0; i < pcm.length; i++) pcm[i] = bytes.readInt16LE(i * 2) / 32768;
    const cues: SoundCue[] = (d.events ?? []).map((e: any) => ({ sample: Number(e.sample), source: String(e.source), clip: String(e.clip) }));
    const speech: HeardSpeech[] = [];
    if (pcm.length > 8000 && Number(d.peak) > 0.01 && await this.vad.load()) {
      for (const [a, b] of await this.vad.segments(pcm)) {
        const t = Date.now();
        const text = await this.transcribe(pcm.subarray(a, b));
        if (text) speech.push({ fromSample: Number(d.from_sample) + a, toSample: Number(d.from_sample) + b, text, latencyMs: Date.now() - t });
      }
    }
    this.cursor = Number(d.to_sample);
    return { speech, cues, cursor: this.cursor, audioSeconds: pcm.length / 16000, peak: Number(d.peak ?? 0) };
  }

  dispose(): void {
    try { this.proc?.kill(); } catch { /* ignore */ }
    this.proc = null;
    this.ready = false;
  }
}
