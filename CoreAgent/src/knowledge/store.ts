/**
 * Knowledge store — how the agent learns without overgrowing, degrading, or leaking one game into another.
 *
 *   <game project>/URDT_Knowledge/            (reported by URDT health.knowledge_dir; read FIRST at connect)
 *     project.json                             product, builds seen
 *     app_map.json                             screens, transitions, layers, coverage (see app_map.ts)
 *     facts.json                               game facts (routes, rules, fail causes) with build + evidence
 *     params.json                              tuned skill parameters per module (lead, aim offsets…)
 *     skills/stats.json                        per-skill outcome statistics in THIS game, per context signature
 *     skills/candidates/<name>/                synthesized skills for this game (manifest.json + controller.ts)
 *   CoreAgent/knowledge/core/                  generic skills that passed the regression gate (+ stats)
 *   CoreAgent/knowledge/archive/               retired skills (never loaded, kept for rollback)
 *
 * Rules
 *  - Game facts/params never leave the game folder; other games never read them.
 *  - A skill runs only when its applicability contract matches; among matches the one with the best posterior
 *    success for this context signature runs first (Beta prior: core 0.6, candidate 0.35), new ones get a
 *    capped trial budget.
 *  - Learning happens only from verified outcomes (the task's success predicates), never from raw reward.
 *  - Versions are immutable: a changed skill is a new version; promotion to core requires the regression gate.
 *  - Maintenance archives skills that lost (≥6 runs, <20% success) or went unused, and caps 3 active skills per
 *    applicability signature.
 */

import fs from 'node:fs';
import path from 'node:path';
import { ROOT } from '../task/runtime_paths.js';

export type SkillTier = 'core' | 'candidate' | 'archived';

export interface SkillRequirement {
  /** Keys that must exist in the scope beacon's game state. */
  scopeGame?: string[];
  /** Regexes (source) that must match at least one beacon id in scope. */
  beacons?: string[];
  /** Regex (source) on button ids that must exist. */
  buttons?: string[];
}

export interface SkillManifest {
  name: string;
  version: number;
  tier: SkillTier;
  source: 'builtin' | 'synthesized';
  description: string;
  requires: SkillRequirement;
  /** synthesized only: controller file relative to the skill folder */
  entry?: string;
  project?: string;            // where a candidate was born
  createdAt: number;
  parent?: string;             // name@version it replaces
  evidence: Array<{ runId: string; project: string; module: string; ok: boolean; ms: number; at: number }>;
}

export interface SkillStat { runs: number; ok: number; ms: number; lastRun: number; lastOk: number; byContext: Record<string, { runs: number; ok: number }> }

export interface Fact { key: string; value: unknown; module?: string; build: string; evidence: string[]; updatedAt: number; confidence: number }

function readJson<T>(file: string, fallback: T): T {
  try { return JSON.parse(fs.readFileSync(file, 'utf-8')) as T; } catch { return fallback; }
}
function writeJson(file: string, data: unknown): void {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  const tmp = `${file}.tmp`;
  fs.writeFileSync(tmp, JSON.stringify(data, null, 2));
  fs.renameSync(tmp, file);
}

export const CORE_DIR = path.join(ROOT, 'CoreAgent', 'knowledge', 'core');
export const ARCHIVE_DIR = path.join(ROOT, 'CoreAgent', 'knowledge', 'archive');

export class KnowledgeStore {
  readonly gameDir: string;
  readonly build: string;
  readonly product: string;

  constructor(health: { knowledge_dir?: string; product_name?: string; build_id?: string; projectPathHash?: string }) {
    // The game's own folder when URDT reports it; otherwise a per-project fallback inside the agent (clearly named).
    this.gameDir = health.knowledge_dir && health.knowledge_dir.length > 0
      ? health.knowledge_dir
      : path.join(ROOT, 'CoreAgent', 'knowledge', 'unbound', health.projectPathHash ?? 'unknown');
    this.build = health.build_id ?? 'unknown';
    this.product = health.product_name ?? 'unknown';
    fs.mkdirSync(path.join(this.gameDir, 'skills', 'candidates'), { recursive: true });
    const readme = path.join(this.gameDir, 'README.md');
    if (!fs.existsSync(readme)) {
      fs.writeFileSync(readme, `# URDT_Knowledge\n\nWhat the URDT testing agent learned about this game: application map, facts, tuned parameters,\nskill statistics and game-specific skills. Safe to commit; delete to make the agent re-learn.\nNothing here is loaded for other games.\n`);
    }
    const proj = readJson<{ product: string; builds: string[]; updatedAt: number }>(this.file('project.json'), { product: this.product, builds: [], updatedAt: 0 });
    if (!proj.builds.includes(this.build)) proj.builds.push(this.build);
    proj.product = this.product;
    proj.updatedAt = Date.now();
    writeJson(this.file('project.json'), proj);
  }

  file(rel: string): string { return path.join(this.gameDir, rel); }

  // ── Facts (game-scoped) ──────────────────────────────────────────────────
  facts(): Fact[] { return readJson<Fact[]>(this.file('facts.json'), []); }

  /** Facts of other builds are returned with halved confidence: they must be re-confirmed before being trusted. */
  fact(key: string): Fact | undefined {
    const f = this.facts().find(x => x.key === key);
    return f && f.build !== this.build ? { ...f, confidence: f.confidence / 2 } : f;
  }

  putFact(key: string, value: unknown, evidence: string, module?: string): void {
    const all = this.facts();
    const i = all.findIndex(x => x.key === key);
    const prev = i >= 0 ? all[i] : undefined;
    const same = prev && JSON.stringify(prev.value) === JSON.stringify(value);
    const f: Fact = {
      key, value, module, build: this.build, updatedAt: Date.now(),
      evidence: [...(same ? prev!.evidence : []), evidence].slice(-5),
      confidence: same ? Math.min(1, (prev!.confidence ?? 0.5) + 0.15) : 0.5,
    };
    if (i >= 0) all[i] = f; else all.push(f);
    writeJson(this.file('facts.json'), all);
  }

  removeFact(key: string): void {
    writeJson(this.file('facts.json'), this.facts().filter(f => f.key !== key));
  }

  // ── Snapshots: the game's learned state can always be rolled back ─────────
  /** Saves facts, parameters and skill statistics; keeps the last 12 snapshots. Returns the snapshot id. */
  snapshot(label: string): string {
    const id = `${new Date().toISOString().replace(/[:.]/g, '-').slice(0, 19)}_${label.replace(/[^a-z0-9_-]/gi, '_')}`;
    const dir = this.file(path.join('history', id));
    fs.mkdirSync(dir, { recursive: true });
    for (const f of ['facts.json', 'params.json', path.join('skills', 'stats.json')]) {
      const src = this.file(f);
      if (fs.existsSync(src)) fs.copyFileSync(src, path.join(dir, path.basename(f)));
    }
    const all = fs.readdirSync(this.file('history')).sort();
    for (const old of all.slice(0, Math.max(0, all.length - 12))) fs.rmSync(this.file(path.join('history', old)), { recursive: true, force: true });
    return id;
  }

  snapshots(): string[] {
    const h = this.file('history');
    return fs.existsSync(h) ? fs.readdirSync(h).sort() : [];
  }

  /** Restores facts, parameters and skill statistics from a snapshot (the current state is snapshotted first). */
  rollback(id: string): boolean {
    const dir = this.file(path.join('history', id));
    if (!fs.existsSync(dir)) return false;
    this.snapshot('before-rollback');
    for (const [name, dest] of [['facts.json', 'facts.json'], ['params.json', 'params.json'], ['stats.json', path.join('skills', 'stats.json')]] as const) {
      const src = path.join(dir, name);
      if (fs.existsSync(src)) fs.copyFileSync(src, this.file(dest));
      else if (fs.existsSync(this.file(dest))) fs.rmSync(this.file(dest));
    }
    return true;
  }

  // ── Tuned parameters (game-scoped) ──────────────────────────────────────
  params(skill: string, module: string): Record<string, unknown> | undefined {
    return readJson<Record<string, Record<string, unknown>>>(this.file('params.json'), {})[`${skill}@${module}`];
  }
  putParams(skill: string, module: string, p: Record<string, unknown>): void {
    const all = readJson<Record<string, Record<string, unknown>>>(this.file('params.json'), {});
    all[`${skill}@${module}`] = { ...(all[`${skill}@${module}`] ?? {}), ...p, _build: this.build, _at: Date.now() };
    writeJson(this.file('params.json'), all);
  }

  // ── Skills: manifests (core + this game's candidates) and statistics ───────
  coreManifests(): SkillManifest[] { return readJson<SkillManifest[]>(path.join(CORE_DIR, 'skills.json'), []); }
  private saveCore(m: SkillManifest[]): void { writeJson(path.join(CORE_DIR, 'skills.json'), m); }

  candidateManifests(): Array<SkillManifest & { dir: string }> {
    const dir = this.file(path.join('skills', 'candidates'));
    if (!fs.existsSync(dir)) return [];
    return fs.readdirSync(dir).map(n => path.join(dir, n)).filter(d => fs.existsSync(path.join(d, 'manifest.json')))
      .map(d => ({ ...readJson<SkillManifest>(path.join(d, 'manifest.json'), null as any), dir: d }))
      .filter(m => m && m.tier === 'candidate');
  }

  /** Registers built-in skills as core manifests once (their code lives in the agent). */
  ensureBuiltin(defs: Array<{ name: string; description: string; requires: SkillRequirement }>): void {
    const core = this.coreManifests();
    let changed = false;
    for (const d of defs) {
      if (core.some(m => m.name === d.name)) continue;
      core.push({ name: d.name, version: 1, tier: 'core', source: 'builtin', description: d.description, requires: d.requires, createdAt: Date.now(), evidence: [] });
      changed = true;
    }
    if (changed) this.saveCore(core);
  }

  stats(): Record<string, SkillStat> { return readJson<Record<string, SkillStat>>(this.file(path.join('skills', 'stats.json')), {}); }

  /** Records a verified outcome for a skill in this game (and as evidence on its manifest). */
  record(skill: string, context: string, ok: boolean, ms: number, runId: string, module: string): void {
    const all = this.stats();
    const s = all[skill] ?? { runs: 0, ok: 0, ms: 0, lastRun: 0, lastOk: 0, byContext: {} };
    s.runs++; if (ok) { s.ok++; s.lastOk = Date.now(); }
    s.ms += ms; s.lastRun = Date.now();
    const c = s.byContext[context] ?? { runs: 0, ok: 0 };
    c.runs++; if (ok) c.ok++;
    s.byContext[context] = c;
    all[skill] = s;
    writeJson(this.file(path.join('skills', 'stats.json')), all);
    const ev = { runId, project: this.product, module, ok, ms, at: Date.now() };
    const cand = this.candidateManifests().find(m => m.name === skill);
    if (cand) {
      const { dir, ...m } = cand;
      m.evidence = [...m.evidence, ev].slice(-30);
      writeJson(path.join(dir, 'manifest.json'), m);
    } else {
      const core = this.coreManifests();
      const m = core.find(x => x.name === skill);
      if (m) { m.evidence = [...m.evidence, ev].slice(-30); this.saveCore(core); }
    }
  }

  /**
   * Orders matching skills: posterior success for this context (Beta prior by tier), then overall, then speed.
   * Candidates with a failing record in this context (≥3 runs, 0 ok) are skipped.
   */
  rank<T extends { skill: string }>(matches: T[], context: string): T[] {
    const st = this.stats();
    const tierOf = (n: string): SkillTier => this.candidateManifests().some(m => m.name === n) ? 'candidate' : 'core';
    const score = (n: string) => {
      const s = st[n];
      const prior = tierOf(n) === 'core' ? 0.6 : 0.35;
      const c = s?.byContext[context];
      const ctx = c ? (c.ok + prior * 2) / (c.runs + 2) : prior;
      const all = s ? (s.ok + prior * 2) / (s.runs + 2) : prior;
      return ctx * 0.7 + all * 0.3;
    };
    return matches
      .filter(m => { const c = st[m.skill]?.byContext[context]; return !(tierOf(m.skill) === 'candidate' && c && c.runs >= 3 && c.ok === 0); })
      .sort((a, b) => score(b.skill) - score(a.skill));
  }

  // ── Synthesized skills (candidates) ─────────────────────────────────────
  /** Saves a synthesized controller as a candidate of THIS game after a static safety check. */
  submitCandidate(input: { name: string; description: string; requires: SkillRequirement; source: string }): { ok: boolean; reason?: string; dir?: string; version?: number } {
    const bad = checkControllerSource(input.source);
    if (bad) return { ok: false, reason: bad };
    const safe = input.name.replace(/[^a-z0-9_-]/gi, '_').toLowerCase();
    const prev = this.candidateManifests().filter(m => m.name === safe).sort((a, b) => b.version - a.version)[0];
    const version = (prev?.version ?? 0) + 1;
    const dir = this.file(path.join('skills', 'candidates', `${safe}@v${version}`));
    fs.mkdirSync(dir, { recursive: true });
    fs.writeFileSync(path.join(dir, 'controller.ts'), input.source);
    const m: SkillManifest = {
      name: safe, version, tier: 'candidate', source: 'synthesized', description: input.description, requires: input.requires,
      entry: 'controller.ts', project: this.product, createdAt: Date.now(), parent: prev ? `${prev.name}@${prev.version}` : undefined, evidence: [],
    };
    writeJson(path.join(dir, 'manifest.json'), m);
    // Older versions of the same candidate are retired (immutable history, one active version).
    if (prev) { const { dir: pd, ...pm } = prev; writeJson(path.join(pd, 'manifest.json'), { ...pm, tier: 'archived' }); }
    return { ok: true, dir, version };
  }

  /** Copies a candidate into core (called by the regression gate only). */
  promote(name: string): boolean {
    const cand = this.candidateManifests().find(m => m.name === name);
    if (!cand) return false;
    const target = path.join(CORE_DIR, 'skills', `${cand.name}@v${cand.version}`);
    fs.mkdirSync(target, { recursive: true });
    fs.copyFileSync(path.join(cand.dir, cand.entry ?? 'controller.ts'), path.join(target, 'controller.ts'));
    const core = this.coreManifests().filter(m => m.name !== cand.name);
    const { dir, ...m } = cand;
    core.push({ ...m, tier: 'core', entry: path.relative(CORE_DIR, path.join(target, 'controller.ts')) });
    this.saveCore(core);
    writeJson(path.join(dir, 'manifest.json'), { ...m, tier: 'archived' });
    void dir;
    return true;
  }

  /** Retires skills that lost or went unused; caps active candidates per requirement signature. */
  maintain(now = Date.now()): string[] {
    const st = this.stats();
    const retired: string[] = [];
    const sig = (m: SkillManifest) => JSON.stringify(m.requires);
    const bySig = new Map<string, Array<SkillManifest & { dir: string }>>();
    for (const m of this.candidateManifests()) {
      const s = st[m.name];
      const losing = s && s.runs >= 6 && s.ok / s.runs < 0.2;
      const stale = (s?.lastRun ?? m.createdAt) < now - 30 * 86400000;
      if (losing || stale) {
        const { dir, ...mm } = m;
        const dest = path.join(ARCHIVE_DIR, this.product, `${m.name}@v${m.version}`);
        fs.mkdirSync(dest, { recursive: true });
        for (const f of fs.readdirSync(dir)) fs.copyFileSync(path.join(dir, f), path.join(dest, f));
        writeJson(path.join(dir, 'manifest.json'), { ...mm, tier: 'archived' });
        retired.push(`${m.name}@v${m.version} (${losing ? 'losing' : 'unused'})`);
        continue;
      }
      bySig.set(sig(m), [...(bySig.get(sig(m)) ?? []), m]);
    }
    for (const group of bySig.values()) {
      const ranked = group.sort((a, b) => {
        const sa = st[a.name], sb = st[b.name];
        return ((sb ? sb.ok / Math.max(1, sb.runs) : 0.35) - (sa ? sa.ok / Math.max(1, sa.runs) : 0.35));
      });
      for (const m of ranked.slice(3)) {
        const { dir, ...mm } = m;
        writeJson(path.join(dir, 'manifest.json'), { ...mm, tier: 'archived' });
        retired.push(`${m.name}@v${m.version} (dominated)`);
      }
    }
    return retired;
  }
}

/** Synthesized controllers get a restricted API; reject sources that try to escape it. */
export function checkControllerSource(raw: string): string | null {
  // Comments and string contents may mention anything ("tempo window", "process") — check code only.
  const src = raw
    .replace(/\/\*[\s\S]*?\*\//g, '')
    .replace(/(^|[^:])\/\/.*$/gm, '$1')
    .replace(/'(?:\.|[^'\\n])*'|"(?:\.|[^"\\n])*"/g, '""');
  const forbidden: Array<[RegExp, string]> = [
    [/\brequire\s*\(/, 'require()'], [/\bimport\s*\(/, 'dynamic import'], [/^\s*import\s/m, 'import statements (use the api argument)'],
    [/\bprocess\s*\./, 'process'], [/\bchild_process\b|\bfs\s*\./, 'node APIs'], [/\beval\s*\(|new\s+Function\b/, 'eval'],
    [/\.call\s*\(\s*["`]/, 'raw URDT calls (only api.world / api.motor are allowed)'], [/\bglobalThis\b|\bwindow\s*\.|\bdocument\s*\./, 'globals'],
  ];
  for (const [re, what] of forbidden) if (re.test(src)) return `controller uses ${what}`;
  if (!/export\s+default\s+async\s+function/.test(raw)) return 'controller must `export default async function (api) { … }`';
  return null;
}
