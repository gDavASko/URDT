/**
 * Dual export (Failure Evidence Contract): machine loop (.harness/failure_evidence.json + per-run JSON)
 * and human loop (QA_Audit_Report.html). Every dynamic string is escaped (architecture §8.3).
 */

import fs from 'node:fs';
import path from 'node:path';
import { InvariantResult } from './invariant_checker.js';
import { HypothesisRecord, PlaybookResult } from '../l2_tactics/playbook_context.js';
import { MotorTraceEntry } from '../l1_kinematics/motor_cortex.js';

export interface ScenarioReport {
  id: string;
  title: string;
  suite: string;
  playbook: string;
  startedAt: string;
  durationMs: number;
  verdict: 'CERTIFIED' | 'FAILED' | 'BLOCKED';
  result: PlaybookResult;
  invariants: InvariantResult[];
  exploits: Array<{ id: string; text: string; pass: boolean; results: InvariantResult[] }>;
  dod: string[];
  hypotheses: HypothesisRecord[];
  arbiterDecisions: any[];
  actions: MotorTraceEntry[];
  log: string[];
  evidence?: FailureEvidencePacket;
}

export interface FailureEvidencePacket {
  $schema: string;
  incidentId: string;
  timestamp: number;
  failingPhase: string;
  gddInvariantViolated: string;
  stagnationType: string;
  activeScene: string;
  beaconStateSnapshot: Array<Record<string, unknown>>;
  actionHistoryBeforeFailure: MotorTraceEntry[];
  diagnostics: {
    engineConsoleErrors: string[];
    activeModalDialog: string | null;
    screenshotDataUri?: string;
    /** Rolling RAM dashcam (≈5 s before the failure), data:image/jpeg URIs, oldest first. */
    crashDashcamFrames?: string[];
    recommendedAiFix: string;
  };
}

export function escapeHtml(s: unknown): string {
  return String(s ?? '').replace(/[&<>"']/g, c => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c]!));
}

export function writeEvidence(harnessDir: string, packet: FailureEvidencePacket): string {
  fs.mkdirSync(harnessDir, { recursive: true });
  const file = path.join(harnessDir, `failure_evidence_${packet.failingPhase}.json`);
  fs.writeFileSync(file, JSON.stringify(packet, null, 2), 'utf-8');
  fs.writeFileSync(path.join(harnessDir, 'failure_evidence.json'), JSON.stringify(packet, null, 2), 'utf-8');
  return file;
}

export function writeHtmlReport(file: string, run: { startedAt: string; gdd: string; slm: string; map?: any; reports: ScenarioReport[] }): void {
  const certified = run.reports.filter(r => r.verdict === 'CERTIFIED').length;
  const rows = run.reports.map(r => {
    const inv = r.invariants.map(i => `<li class="${i.pass ? 'ok' : 'bad'}">${escapeHtml(i.id)} — ${escapeHtml(i.text)}: observed <code>${escapeHtml(JSON.stringify(i.observed))}</code> ${escapeHtml(i.op)} <code>${escapeHtml(JSON.stringify(i.expected))}</code></li>`).join('');
    const ex = r.exploits.map(e => `<li class="${e.pass ? 'ok' : 'bad'}">${escapeHtml(e.id)} — ${escapeHtml(e.text)}</li>`).join('');
    const hyp = r.hypotheses.map(h => `<li class="${h.outcome === 'confirmed' ? 'ok' : h.outcome === 'refuted' ? 'bad' : ''}">${escapeHtml(h.text)} <em>${escapeHtml(h.outcome)}</em></li>`).join('');
    const arb = r.arbiterDecisions.map(d => `<li>${escapeHtml(d.source)} ${escapeHtml(d.latencyMs)}ms → ${escapeHtml(d.action)} ${escapeHtml(d.targetBeaconId)} <small>${escapeHtml(d.diagnosis)}</small></li>`).join('');
    const acts = r.actions.slice(-40).map(a => `<tr><td>${escapeHtml(new Date(a.t).toISOString().slice(11, 23))}</td><td>${escapeHtml(a.primitive)}</td><td>${escapeHtml(a.target ?? '')}</td><td>${a.from ? `${a.from.x.toFixed(0)},${a.from.y.toFixed(0)}` : ''}</td><td>${a.to ? `${a.to.x.toFixed(0)},${a.to.y.toFixed(0)}` : ''}</td><td>${escapeHtml(a.frames ?? '')}</td><td>${a.ok ? 'ok' : escapeHtml(a.error)}</td></tr>`).join('');
    const uri = r.evidence?.diagnostics.screenshotDataUri ?? '';
    const shot = /^data:image\/(png|jpeg);base64,[A-Za-z0-9+/=]+$/.test(uri) ? `<img class="shot" alt="failure frame" src="${uri}">` : '';
    const film = (r.evidence?.diagnostics.crashDashcamFrames ?? []).filter(f => /^data:image\/jpeg;base64,[A-Za-z0-9+/=]+$/.test(f)).slice(-10)
      .map(f => `<img class="film" alt="dashcam frame" src="${f}">`).join('');
    return `<details class="card ${r.verdict.toLowerCase()}"><summary><b>${escapeHtml(r.id)}</b> ${escapeHtml(r.title)} <span class="badge">${escapeHtml(r.verdict)}</span> <span class="muted">${escapeHtml(r.playbook)} · ${(r.durationMs / 1000).toFixed(1)}s · ${escapeHtml(r.result.summary)}</span></summary>
<div class="grid"><div><h4>DoD</h4><ul>${r.dod.map(d => `<li>${escapeHtml(d)}</li>`).join('')}</ul><h4>Invariants</h4><ul>${inv}</ul>${ex ? `<h4>Exploit experiments</h4><ul>${ex}</ul>` : ''}</div>
<div><h4>Hypotheses (micro tier)</h4><ul>${hyp}</ul>${arb ? `<h4>Micro-SLM arbiter</h4><ul>${arb}</ul>` : ''}</div></div>
${shot}${film ? `<h4>Dashcam (last frames before the verdict)</h4><div class="strip">${film}</div>` : ''}<h4>L1 action trace (last 40)</h4><table><tr><th>t</th><th>primitive</th><th>target</th><th>from</th><th>to</th><th>frames</th><th>result</th></tr>${acts}</table>
<details><summary>log</summary><pre>${escapeHtml(r.log.join('\n'))}</pre></details></details>`;
  }).join('\n');
  const html = `<!doctype html><html lang="ru"><head><meta charset="utf-8"><title>URDT QA Audit</title><meta name="viewport" content="width=device-width,initial-scale=1">
<style>:root{--bg:#fff;--fg:#1d2230;--muted:#667;--ok:#1a7f37;--bad:#c62828;--card:#f5f6f8}
@media (prefers-color-scheme:dark){:root{--bg:#111418;--fg:#e6e8eb;--muted:#9aa;--ok:#4cc36a;--bad:#ff6b6b;--card:#1b1f25}}
body{background:var(--bg);color:var(--fg);font:14px/1.45 system-ui,sans-serif;margin:0 auto;max-width:1200px;padding:16px}
.card{background:var(--card);border-radius:8px;margin:8px 0;padding:8px 12px}.badge{font-weight:700;margin-left:6px}
.certified .badge{color:var(--ok)}.failed .badge,.blocked .badge{color:var(--bad)}.muted{color:var(--muted)}
.ok{color:var(--ok)}.bad{color:var(--bad)}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(320px,1fr));gap:12px}
table{border-collapse:collapse;width:100%;font-size:12px;overflow-x:auto;display:block}td,th{border-bottom:1px solid #8884;padding:2px 6px;text-align:left}
.shot{max-width:100%;border-radius:6px;margin:8px 0}.strip{display:flex;gap:4px;overflow-x:auto}.film{height:90px;border-radius:4px}pre{white-space:pre-wrap;font-size:11px}code{font-size:12px}</style></head>
<body><h1>URDT Autonomous Reviewer — QA Audit</h1>
<p>Run ${escapeHtml(run.startedAt)} · GDD <code>${escapeHtml(run.gdd)}</code> · arbiter: ${escapeHtml(run.slm)} · <b>${certified}/${run.reports.length}</b> certified</p>
${rows}
${run.map ? `<h2>Application map (Mode 1)</h2><pre>${escapeHtml(JSON.stringify({ nodes: Object.keys(run.map.nodes), edges: run.map.edges, defects: run.map.structuralDefects }, null, 1))}</pre>` : ''}
</body></html>`;
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, html, 'utf-8');
}
