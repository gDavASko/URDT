/**
 * Briefing — what a human does before touching a level: read every caption, look at the scene, form a plan.
 *
 *  1. Texts: module instruction, text beacons, button captions (a label inside a button rect names the button).
 *  2. Semantics: verb lexicon (RU/EN) → preferred action kinds; "A → B → C" sequences; "avoid X"; numbers.
 *  3. Inventory: draggables, receptacles, buttons, ordered families (#1..#3 slots, Waypoint_0..n).
 *  4. One screenshot to a file (dashcam end-of-frame JPEG) — an auxiliary artefact for the meta-AI or a
 *     vision model, never an input to the control loop.
 *  5. Plans: concrete candidate strategies derived from the above (e.g. place items in the order the
 *     instruction names them, then press the button captioned "Run"/"Пуск").
 */

import fs from 'node:fs';
import path from 'node:path';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { Beacon, WorldModel, rectContains } from '../perception/world_model.js';
import { VisionAnalyst, VisionAnalysis, VisionPlanStep } from './vision.js';

export type ActionKind = 'tap' | 'drag' | 'dwell_drag' | 'hold' | 'trace' | 'scrub' | 'rotate' | 'alternate' | 'mash' | 'timing';

const VERBS: Array<{ kind: ActionKind; re: RegExp }> = [
  { kind: 'drag', re: /перетащ|перенес|помест|положи|вставь|собери|разлож|составь|соедин|подключ|наден|оден|уложи|drag|place|put|insert|sort|connect|assemble|equip/i },
  { kind: 'hold', re: /зажм|удерж|держи|накач|hold|press and hold|charge/i },
  { kind: 'rotate', re: /поверн|вращ|крут|оборот|rotate|spin|turn the/i },
  { kind: 'trace', re: /проведи|веди|по линии|по контуру|вырез|по точкам|trace|follow|cut along|path/i },
  { kind: 'scrub', re: /сотри|стира|отмой|очист|scrub|wipe|clean/i },
  { kind: 'alternate', re: /чередуй|поочеред|ритм|alternate|rhythm/i },
  { kind: 'mash', re: /быстро (на)?жим|быстро тап|часто|мни|tap fast|mash|rapid/i },
  { kind: 'timing', re: /в момент|вовремя|когда .* (проход|окаж)|поклёв|поклев|timing|when the/i },
  { kind: 'dwell_drag', re: /задерж(и|ите) над|наведи.* и держ|лупа|lens|hover over/i },
  { kind: 'tap', re: /нажми|нажмите|кликн|тапни|выбери|кликай|tap|click|press/i },
];
const AVOID = /(избега\w*|не (трогай|нажимай|касай)\w*|остерега\w*|берегись|avoid|don'?t touch|beware of)\s+([^.!;]+)/gi;
const RUN_CAPTION = /пуск|старт|запуск|выполн|go|run|start|execute|launch|play/i;

export interface Briefing {
  scope: string;
  instruction: string;
  texts: Array<{ id: string; text: string }>;
  captions: Record<string, string>;          // button testId → caption
  verbs: ActionKind[];
  sequences: string[][];                      // ordered item names from "A → B → C"
  avoid: string[];
  numbers: number[];
  inventory: { draggables: string[]; receptacles: string[]; buttons: string[]; families: Record<string, string[]> };
  plans: Plan[];
  screenshotFile?: string;
  vision?: VisionAnalysis | null;
  summary: string;
}

export interface Plan {
  kind: 'ordered_placement' | 'press_run' | 'prefer_actions' | 'vision_plan';
  visionSteps?: VisionPlanStep[];
  why: string;
  steps?: Array<{ item: string; target: string }>;
  button?: string;
  actions?: ActionKind[];
}

function strip(t: string): string {
  return String(t ?? '').replace(/<[^>]+>/g, '').replace(/\s+/g, ' ').trim();
}

function norm(t: string): string {
  return strip(t).toLowerCase().replace(/[^a-zа-яё0-9 ]/gi, ' ').replace(/\s+/g, ' ').trim();
}

export async function buildBriefing(world: WorldModel, client: UrdtWireClient, scopeId: string, outDir: string, screenshot = true, vision?: VisionAnalyst | null, goal = '', heard: string[] = []): Promise<Briefing> {
  const snap = await world.snapshot();
  const scope = snap.get(scopeId);
  const parts = scope ? snap.within(scope).filter(b => b.testId !== scopeId) : [];
  const texts = parts.filter(b => b.kind === 'text' && strip(b.props.Text)).map(b => ({ id: b.testId, text: strip(b.props.Text) }));
  // Voice hints heard in the game audio are treated exactly like on-screen captions.
  for (const [i, h] of heard.entries()) texts.push({ id: `[voice]#${i + 1}`, text: h });
  const instruction = strip(scope?.props.Instruction ?? '');
  const all = [instruction, ...texts.map(t => t.text)].join(' . ');

  // Captions: a text beacon whose centre lies inside a button's rect labels that button.
  const buttons = parts.filter(b => b.kind === 'button' && b.visible);
  const captions: Record<string, string> = {};
  for (const b of buttons) {
    const label = texts.find(t => { const tb = snap.get(t.id); return !!(tb && b.rect && rectContains(b.rect, tb.center, 2)); });
    if (label) captions[b.testId] = label.text;
  }

  const verbs = VERBS.filter(v => v.re.test(all)).map(v => v.kind);
  const sequences = [...all.matchAll(/([^.:!]+?(?:\s*(?:->|→|=>|—>)\s*[^.:!→>-]+)+)/g)]
    .map(m => m[1].split(/\s*(?:->|→|=>|—>)\s*/).map(s => norm(s).split(' ').slice(-2).join(' ')).filter(Boolean))
    .filter(s => s.length >= 2);
  const avoid = [...all.matchAll(AVOID)].map(m => strip(m[2]).slice(0, 60));
  const numbers = [...all.matchAll(/\b(\d+(?:[.,]\d+)?)\b/g)].map(m => parseFloat(m[1].replace(',', '.')));

  const draggables = parts.filter(b => b.kind === 'draggable' && Object.keys(b.game ?? {}).length);
  const receptacles = parts.filter(b => b.kind === 'slot' && b.visible);
  const families: Record<string, string[]> = {};
  for (const b of parts) {
    const m = b.testId.match(/^(.*?)[_#]?(\d+)$/);
    if (m && m[1]) (families[m[1].replace(/[_#]$/, '')] ??= []).push(b.testId);
  }
  for (const k of Object.keys(families)) if (families[k].length < 2) delete families[k];

  const plans: Plan[] = [];
  // Ordered placement: an instruction sequence whose words name draggable items, and an ordered slot family.
  const itemName = (b: Beacon) => norm([b.testId, ...Object.values(b.game ?? {}).filter(v => typeof v === 'string')].join(' '));
  for (const seq of sequences) {
    const slotFam = Object.values(families).map(ids => ids.map(id => snap.get(id)!).filter(b => b && b.kind === 'slot'))
      .filter(f => f.length >= seq.length).sort((a, b) => a.length - b.length)[0];
    if (!slotFam) continue;
    const ordered = [...slotFam].sort((a, b) => Number(a.testId.match(/(\d+)$/)?.[1]) - Number(b.testId.match(/(\d+)$/)?.[1]));
    const used = new Set<string>();
    const steps: Array<{ item: string; target: string }> = [];
    for (let i = 0; i < seq.length; i++) {
      const word = seq[i].split(' ').filter(w => w.length > 2).pop() ?? seq[i];
      const item = draggables.find(d => !used.has(d.testId) && itemName(d).includes(word.slice(0, Math.max(4, word.length - 2))));
      if (!item) { steps.length = 0; break; }
      used.add(item.testId);
      steps.push({ item: item.testId, target: ordered[i].testId });
    }
    if (steps.length === seq.length) plans.push({ kind: 'ordered_placement', why: `instruction orders "${seq.join(' → ')}"`, steps });
  }
  const run = buttons.find(b => RUN_CAPTION.test(captions[b.testId] ?? '') || RUN_CAPTION.test(b.testId));
  if (run) plans.push({ kind: 'press_run', why: `button captioned "${captions[run.testId] ?? run.testId}" starts execution`, button: run.testId });
  if (verbs.length) plans.push({ kind: 'prefer_actions', why: `instruction verbs → ${verbs.join(', ')}`, actions: verbs });

  let screenshotFile: string | undefined;
  let visionResult: VisionAnalysis | null = null;
  if (screenshot) {
    // Full-resolution end-of-frame frame (fallback: the low-res dashcam/diagnostic capture).
    let uri: string | undefined = (await VisionAnalyst.grabHdFrame(client)) ?? undefined;
    if (!uri) {
      const cap = await client.call('capture', { screenshot: true, log_tail: 0 }, 10000).catch(() => null);
      uri = cap?.data?.screenshot_jpeg_datauri ?? (cap?.data?.screenshot_b64 ? `data:image/png;base64,${cap.data.screenshot_b64}` : undefined);
    }
    const m = uri?.match(/^data:image\/(jpeg|png);base64,(.+)$/);
    if (m) {
      fs.mkdirSync(outDir, { recursive: true });
      screenshotFile = path.join(outDir, `briefing_${scopeId}.${m[1] === 'png' ? 'png' : 'jpg'}`);
      fs.writeFileSync(screenshotFile, Buffer.from(m[2], 'base64'));
    }
    if (uri && vision) {
      const actionable = parts.filter(b => b.visible && b.kind !== 'window' && b.kind !== 'module' && b.center.y > 0 && b.center.y < 1080).slice(0, 60);
      visionResult = await vision.analyze(uri, actionable, [instruction, ...texts.map(t => t.text)].filter(Boolean), goal || instruction, { w: 1920, h: 1080 }, screenshotFile);
      if (visionResult && visionResult.plan.length) {
        plans.unshift({ kind: 'vision_plan', why: `vision (${visionResult.model}, ${visionResult.latencyMs}ms, conf ${visionResult.confidence}): ${visionResult.goal}`, visionSteps: visionResult.plan });
      }
    }
  }

  const summary = [
    instruction && `instruction: "${instruction}"`,
    texts.length && `texts: ${texts.map(t => `"${t.text}"`).slice(0, 8).join(', ')}`,
    Object.keys(captions).length && `buttons: ${Object.entries(captions).map(([k, v]) => `${k}="${v}"`).join(', ')}`,
    verbs.length && `verbs → ${verbs.join(', ')}`,
    sequences.length && `sequence: ${sequences.map(s => s.join(' → ')).join('; ')}`,
    avoid.length && `avoid: ${avoid.join('; ')}`,
    `scene: ${draggables.length} draggable, ${receptacles.length} receptacles, ${buttons.length} buttons, families ${Object.keys(families).join(', ') || 'none'}`,
    plans.length && `plans: ${plans.map(p => p.kind).join(', ')}`,
    visionResult && `vision: "${visionResult.goal}" steps=${visionResult.plan.map(s => `${s.action}:${s.item ?? s.button ?? ''}${s.target ? '→' + s.target : ''}`).join(', ')} avoid=${visionResult.avoid.join(',')}`,
  ].filter(Boolean).join(' | ');

  return { scope: scopeId, instruction, texts, captions, verbs, sequences, avoid, numbers,
    inventory: { draggables: draggables.map(d => d.testId), receptacles: receptacles.map(r => r.testId), buttons: buttons.map(b => b.testId), families },
    plans, screenshotFile, vision: visionResult, summary };
}
