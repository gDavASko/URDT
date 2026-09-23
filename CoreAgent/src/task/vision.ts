/**
 * Vision analyst (auxiliary, rare): a local Qwen3-VL served by llama.cpp `llama-server`.
 *
 * Used at briefing time and when exploration stalls — never inside the control loop. The model gets one
 * full-resolution frame plus the list of beacons with their boxes (0–1000 image coordinates, top-left origin)
 * and must answer in a JSON schema whose object ids are an enum of the live beacon ids, so it can only refer
 * to things L3 can actually act on. Its output is a hypothesis for L3's planner, cross-checked with beacons.
 *
 * Model/backend come from config/agent.config.json → "vision" (switch "active" to qwen3vl-2b if 4b is heavy).
 */

import fs from 'node:fs';
import path from 'node:path';
import { spawn, ChildProcess } from 'node:child_process';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { Beacon, sleep } from '../perception/world_model.js';
import { ROOT } from './runtime_paths.js';

export interface VisionConfig {
  enabled: boolean; active: string; backend: string;
  servers: Record<string, string>; models: Record<string, { model: string; mmproj: string }>;
  port: number; gpuLayers: number; contextSize: number; timeoutMs: number;
  imageMaxTokens?: number; ubatch?: number; maxOutputTokens?: number;
}

export interface VisionPlanStep { action: 'tap' | 'drag' | 'hold' | 'trace' | 'rotate' | 'wait' | 'pull' | 'move_over'; item?: string; target?: string; button?: string; durationMs?: number; repeat?: number; why: string }
export interface VisionAnalysis {
  goal: string;
  instructionsSeen: string[];
  plan: VisionPlanStep[];
  cues: Array<{ id: string; cue: string }>;
  avoid: string[];
  confidence: number;
  latencyMs: number;
  model: string;
  imageFile?: string;
}

export function loadVisionConfig(): VisionConfig | null {
  try {
    const cfg = JSON.parse(fs.readFileSync(path.join(ROOT, 'CoreAgent', 'config', 'agent.config.json'), 'utf-8'));
    return cfg.vision?.enabled ? cfg.vision as VisionConfig : null;
  } catch {
    return null;
  }
}

export class VisionAnalyst {
  private proc: ChildProcess | null = null;
  private ready = false;

  constructor(private readonly cfg: VisionConfig, private readonly log: (s: string) => void) {}

  get url(): string { return `http://127.0.0.1:${this.cfg.port}`; }

  async ensureServer(): Promise<boolean> {
    if (this.ready) return true;
    if (await this.healthy()) { this.ready = true; return true; }
    const exe = this.cfg.servers[this.cfg.backend];
    const m = this.cfg.models[this.cfg.active];
    if (!exe || !fs.existsSync(exe) || !m || !fs.existsSync(m.model) || !fs.existsSync(m.mmproj)) {
      this.log(`[vision] unavailable: missing server or model files for ${this.cfg.active}/${this.cfg.backend}`);
      return false;
    }
    this.log(`[vision] starting llama-server (${this.cfg.active}, ${this.cfg.backend})`);
    this.proc = spawn(exe, ['-m', m.model, '--mmproj', m.mmproj, '--port', String(this.cfg.port), '--host', '127.0.0.1',
      '-ngl', String(this.cfg.gpuLayers), '-c', String(this.cfg.contextSize), '--no-webui', '-fa', 'on',
      ...(this.cfg.imageMaxTokens ? ['--image-max-tokens', String(this.cfg.imageMaxTokens)] : []),
      ...(this.cfg.ubatch ? ['-ub', String(this.cfg.ubatch), '-b', String(Math.max(2048, this.cfg.ubatch))] : [])], { stdio: 'ignore', windowsHide: true });
    this.proc.on('exit', code => { this.ready = false; this.log(`[vision] llama-server exited (${code})`); });
    for (let i = 0; i < 120; i++) {
      if (await this.healthy()) { this.ready = true; this.log('[vision] ready'); return true; }
      await sleep(1000);
    }
    this.log('[vision] server did not become healthy');
    return false;
  }

  private async healthy(): Promise<boolean> {
    try { return (await fetch(`${this.url}/health`)).ok; } catch { return false; }
  }

  /** Requests an end-of-frame full-resolution JPEG through URDT (two-step: request, then collect). */
  static async grabHdFrame(client: UrdtWireClient): Promise<string | null> {
    const first = await client.call('capture', { screenshot: false, log_tail: 0, hd: true }, 5000).catch(() => null);
    const since = first?.data?.hd_since_ms;
    if (!since) return first?.data?.hd_jpeg_datauri ?? null;
    for (let i = 0; i < 20; i++) {
      await sleep(60);
      const r = await client.call('capture', { screenshot: false, log_tail: 0, hd: true, hd_since_ms: since }, 5000).catch(() => null);
      if (r?.data?.hd_jpeg_datauri) return r.data.hd_jpeg_datauri;
    }
    return null;
  }

  async analyze(imageDataUri: string, beacons: Beacon[], texts: string[], goal: string, screen = { w: 1920, h: 1080 }, imageFile?: string, failedAttempts: string[] = []): Promise<VisionAnalysis | null> {
    if (!(await this.ensureServer())) return null;
    const ids = beacons.map(b => b.testId);
    const box = (b: Beacon) => {
      const r = b.rect ?? { x: b.center.x - 20, y: b.center.y - 20, w: 40, h: 40 };
      const x1 = Math.round((r.x / screen.w) * 1000), x2 = Math.round(((r.x + r.w) / screen.w) * 1000);
      const y1 = Math.round((1 - (r.y + r.h) / screen.h) * 1000), y2 = Math.round((1 - r.y / screen.h) * 1000);
      return `[${x1},${y1},${x2},${y2}]`;
    };
    // Compact state tells the model what is already occupied/placed (e.g. inventory slots holding the items).
    const state = (b: Beacon) => {
      const kv = Object.entries(b.game ?? {}).filter(([k, v]) => typeof v === 'boolean' && v && /occupied|full|snapped|placed|installed|locked|junk|broken|hazard|obstacle/i.test(k)).map(([k]) => k);
      if (b.props.IsSnapped === true) kv.push('IsSnapped');
      return kv.length ? ` state ${kv.join(',')}` : '';
    };
    // Large families (grid cells, particles) are listed once with their id range to keep the prompt and answer short.
    const famOf = (id: string) => id.match(/^(.*?)((?:[_#]?\d+)+)$/)?.[1]?.replace(/[_#]$/, '') ?? null;
    const famCount = new Map<string, number>();
    for (const b of beacons) { const f = famOf(b.testId); if (f) famCount.set(f, (famCount.get(f) ?? 0) + 1); }
    const shown = new Set<string>();
    const lines: string[] = [];
    for (const b of beacons) {
      const f = famOf(b.testId);
      if (f && (famCount.get(f) ?? 0) >= 6) {
        if (shown.has(f)) continue;
        shown.add(f);
        const members = beacons.filter(x => famOf(x.testId) === f);
        lines.push(`- ${members[0].testId} … ${members[members.length - 1].testId} (${members.length} ${b.kind}s of family ${f}; use any member id)`);
        continue;
      }
      lines.push(`- ${b.testId} (${b.kind}${b.props.AreaType ? `, role ${b.props.AreaType}` : ''}) box ${box(b)}${state(b)}${b.props.Text ? ` text "${String(b.props.Text).replace(/<[^>]+>/g, '').slice(0, 60)}"` : ''}`);
    }
    const objectList = lines.join('\n');
    const idEnum = ids.length ? ids : ['none'];
    const schema = {
      type: 'object',
      properties: {
        goal: { type: 'string', maxLength: 160 },
        instructionsSeen: { type: 'array', items: { type: 'string', maxLength: 120 }, maxItems: 4 },
        // Reason first, then a mandatory object: without `object` the grammar-constrained model tends to emit
        // steps with no ids at all, which the robot cannot execute.
        plan: { type: 'array', maxItems: 6, items: { type: 'object', properties: {
          why: { type: 'string', maxLength: 90 },
          action: { enum: ['tap', 'drag', 'move_over', 'pull', 'hold', 'trace', 'rotate', 'wait'] },
          object: { enum: idEnum }, target: { enum: [...idEnum, 'none'] },
          durationMs: { type: 'integer' }, repeat: { type: 'integer' } }, required: ['why', 'action', 'object', 'target', 'durationMs', 'repeat'] } },
        cues: { type: 'array', maxItems: 4, items: { type: 'object', properties: { id: { enum: idEnum }, cue: { type: 'string', maxLength: 80 } }, required: ['id', 'cue'] } },
        avoid: { type: 'array', maxItems: 6, items: { enum: idEnum } },
        confidence: { type: 'number' },
      },
      required: ['goal', 'plan', 'avoid', 'confidence'],
    };
    const prompt = `You are the vision analyst of an autonomous game-testing robot. The screenshot shows one level/screen of a game.
Task given to the robot: "${goal}".
Read every caption and instruction on the screen, look at the objects, and explain what the player must do to win.
Only refer to these interactive objects (ids; boxes are [x1,y1,x2,y2] in 0-1000 image coordinates, origin top-left):
${objectList}
Texts already read by the robot: ${texts.map(t => `"${t}"`).join('; ') || 'none'}.${failedAttempts.length ? `
The robot is STUCK. These attempts had NO effect — do not repeat them, find what is missing (a button to press, an order, a precondition, a different object):
${failedAttempts.slice(-15).map(a => `  * ${a}`).join(' | ')}` : ''}
Return JSON: goal (what winning means here), instructionsSeen (captions you read), plan (ordered concrete steps; every step names
the object it acts on and a target id or "none"):
  tap object (repeat = how many taps if it must be tapped many times/fast; durationMs 0)
  drag object -> target (drop it there)
  move_over object -> target (carry a tool over the target and keep it there durationMs, e.g. lens, dispenser)
  pull object (pull it back AWAY from target and release, slingshot-style)
  hold object for durationMs (long holds for pedals/pumps, up to 60000)
  trace object -> target (move the object continuously across the target area / along a path, e.g. wipe, steer, cut)
  rotate object (circular motion), wait durationMs
cues (visual-only hints per object, e.g. colour meaning),
avoid (objects that look like traps/junk/decoys), confidence 0..1. Be concise.`;
    const started = Date.now();
    try {
      const res = await fetch(`${this.url}/v1/chat/completions`, {
        method: 'POST', headers: { 'content-type': 'application/json' },
        signal: AbortSignal.timeout(this.cfg.timeoutMs),
        body: JSON.stringify({
          temperature: 0.1, max_tokens: Math.max(900, this.cfg.maxOutputTokens ?? 0),
          messages: [{ role: 'user', content: [{ type: 'image_url', image_url: { url: imageDataUri } }, { type: 'text', text: prompt }] }],
          response_format: { type: 'json_schema', json_schema: { name: 'vision_plan', schema } },
        }),
      });
      const data: any = await res.json();
      const content = data?.choices?.[0]?.message?.content ?? '{}';
      const parsed = JSON.parse(content);
      const valid = (id?: string) => !id || ids.includes(id);
      return {
        goal: String(parsed.goal ?? ''),
        instructionsSeen: parsed.instructionsSeen ?? [],
        plan: (parsed.plan ?? []).map((st: any): VisionPlanStep => ({
          action: st.action, why: String(st.why ?? ''), durationMs: st.durationMs || undefined, repeat: st.repeat || undefined,
          item: st.object ?? st.item, button: st.action === 'tap' ? (st.object ?? st.button) : st.button,
          target: st.target && st.target !== 'none' ? st.target : undefined,
        })).filter((st: VisionPlanStep) => valid(st.item) && valid(st.target) && valid(st.button) && (!!st.item || !!st.button || st.action === 'wait')),
        cues: (parsed.cues ?? []).filter((c: any) => valid(c.id)),
        avoid: (parsed.avoid ?? []).filter((a: string) => valid(a)),
        confidence: Number(parsed.confidence ?? 0),
        latencyMs: Date.now() - started,
        model: this.cfg.active,
        imageFile,
      };
    } catch (err) {
      this.log(`[vision] analysis failed: ${(err as Error).message}`);
      return null;
    }
  }

  dispose(): void {
    try { this.proc?.kill(); } catch { /* ignore */ }
    this.proc = null;
    this.ready = false;
  }
}
