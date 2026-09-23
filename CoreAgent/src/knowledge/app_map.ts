/**
 * Application map of THIS game (stored in <game>/URDT_Knowledge/app_map.json).
 *
 *  - Screens: identified by the set of visible windows + mechanic modules (text/numbers never enter the id).
 *  - Transitions: a tap on a control that changed the screen, with count, latency, the build it was seen on and
 *    whether it still worked the last time it was used.
 *  - Navigation: shortest known route from the current screen to a screen that shows the target; unknown parts
 *    are explored by the runner and recorded here, so every run makes the next one faster.
 *  - Coverage: controls seen on each screen that were never pressed.
 *  - Build diff: transitions that worked on an earlier build but fail on this one (regressions in navigation).
 *  - Layers: per screen, which windows/modals are on top and which visible controls are covered (hit test).
 */

import fs from 'node:fs';
import { WorldSnapshot, Beacon } from '../perception/world_model.js';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';

export interface MapNode { id: string; windows: string[]; modules: string[]; buttons: string[]; visits: number; firstSeen: number; lastSeen: number; builds: string[] }
export interface MapEdge { from: string; to: string; testId: string; count: number; lastMs: number; lastOk: boolean; builds: string[]; failedOn: string[] }
export interface LayerReport {
  screen: string;
  stack: Array<{ id: string; kind: string; rect?: { x: number; y: number; w: number; h: number } }>;
  modal: string | null;
  covered: Array<{ control: string; coveredBy: string }>;
  controls: number;
}

interface MapFile { nodes: Record<string, MapNode>; edges: MapEdge[]; layers: Record<string, LayerReport> }

export function screenId(snap: WorldSnapshot): { id: string; windows: string[]; modules: string[] } {
  const windows = snap.ofKind('window').filter(b => b.visible).map(b => b.testId).sort();
  const mods = snap.ofKind('module').filter(b => b.visible);
  const modules = mods.map(b => b.testId).sort();
  // Panels of one window (catalog vs play panel) differ by the navigation controls they show: fingerprint the
  // visible buttons outside mechanic modules (mechanic internals do not define the screen).
  const inModule = (b: { center: { x: number; y: number } }) => mods.some(m => m.rect && b.center.x >= m.rect.x && b.center.x <= m.rect.x + m.rect.w && b.center.y >= m.rect.y && b.center.y <= m.rect.y + m.rect.h);
  const nav = snap.ofKind('button').filter(b => b.visible && !inModule(b)).map(b => b.testId).sort().join(',');
  let h = 2166136261;
  for (let i = 0; i < nav.length; i++) { h ^= nav.charCodeAt(i); h = Math.imul(h, 16777619) >>> 0; }
  return { id: `${windows.join('+') || '-'}|${modules.join('+') || '-'}|${h.toString(36)}`, windows, modules };
}

export class AppMap {
  private data: MapFile;
  constructor(private readonly file: string, private readonly build: string) {
    try { this.data = JSON.parse(fs.readFileSync(file, 'utf-8')); } catch { this.data = { nodes: {}, edges: [], layers: {} }; }
    this.data.layers ??= {};
  }

  save(): void {
    fs.writeFileSync(`${this.file}.tmp`, JSON.stringify(this.data, null, 2));
    fs.renameSync(`${this.file}.tmp`, this.file);
  }

  /** Registers (or refreshes) the screen currently shown. */
  see(snap: WorldSnapshot): string {
    const s = screenId(snap);
    const n = this.data.nodes[s.id] ?? { id: s.id, windows: s.windows, modules: s.modules, buttons: [], visits: 0, firstSeen: Date.now(), lastSeen: 0, builds: [] };
    n.visits++;
    n.lastSeen = Date.now();
    const btns = snap.ofKind('button').filter(b => b.visible).map(b => b.testId);
    n.buttons = [...new Set([...n.buttons, ...btns])].sort();
    if (!n.builds.includes(this.build)) n.builds.push(this.build);
    this.data.nodes[s.id] = n;
    return s.id;
  }

  /** Records the outcome of pressing a control on screen `from`. */
  record(from: string, testId: string, to: string, ms: number): void {
    const ok = from !== to;
    let e = this.data.edges.find(x => x.from === from && x.testId === testId && (x.to === to || !ok));
    if (!e && ok) { e = { from, to, testId, count: 0, lastMs: ms, lastOk: true, builds: [], failedOn: [] }; this.data.edges.push(e); }
    if (!e) return;
    e.count++;
    e.lastMs = ms;
    e.lastOk = ok;
    if (ok) { e.to = to; if (!e.builds.includes(this.build)) e.builds.push(this.build); }
    else if (!e.failedOn.includes(this.build)) e.failedOn.push(this.build);
    this.save();
  }

  /** Shortest known route (list of taps) from `from` to any screen satisfying `goal`. */
  route(from: string, goal: (n: MapNode) => boolean): string[] | null {
    if (this.data.nodes[from] && goal(this.data.nodes[from])) return [];
    const prev = new Map<string, { node: string; tap: string }>();
    const q = [from];
    const seen = new Set([from]);
    while (q.length) {
      const cur = q.shift()!;
      for (const e of this.data.edges.filter(x => x.from === cur && x.lastOk)) {
        if (seen.has(e.to)) continue;
        seen.add(e.to);
        prev.set(e.to, { node: cur, tap: e.testId });
        const n = this.data.nodes[e.to];
        if (n && goal(n)) {
          const taps: string[] = [];
          let k = e.to;
          while (prev.has(k)) { taps.unshift(prev.get(k)!.tap); k = prev.get(k)!.node; }
          return taps;
        }
        q.push(e.to);
      }
    }
    return null;
  }

  /** Controls seen on screens but never pressed there (navigation coverage). */
  unvisited(): Array<{ screen: string; controls: string[] }> {
    return Object.values(this.data.nodes).map(n => ({
      screen: n.id,
      controls: n.buttons.filter(b => !this.data.edges.some(e => e.from === n.id && e.testId === b)),
    })).filter(x => x.controls.length);
  }

  /** Transitions that worked on another build and fail on this one. */
  regressions(): MapEdge[] {
    return this.data.edges.filter(e => e.failedOn.includes(this.build) && e.builds.some(b => b !== this.build));
  }

  summary(): { screens: number; transitions: number; builds: string[]; unvisitedControls: number; regressions: number } {
    const builds = [...new Set(Object.values(this.data.nodes).flatMap(n => n.builds))];
    return { screens: Object.keys(this.data.nodes).length, transitions: this.data.edges.length, builds,
      unvisitedControls: this.unvisited().reduce((a, x) => a + x.controls.length, 0), regressions: this.regressions().length };
  }

  get nodes(): Record<string, MapNode> { return this.data.nodes; }
  get edges(): MapEdge[] { return this.data.edges; }

  /**
   * UI layers of the current screen: window/module stack ordered by what is on top at their centres, a modal
   * (a top window covering most of the screen), and visible controls whose centre is covered by something else.
   */
  async layers(snap: WorldSnapshot, client: UrdtWireClient, limit = 40): Promise<LayerReport> {
    const screen = this.see(snap);
    const containers = [...snap.ofKind('window'), ...snap.ofKind('module')].filter(b => b.visible);
    const topAt = async (b: Beacon) => {
      const r: any = await client.call('hit_test', { x: b.center.x, y: b.center.y }, 3000).catch(() => null);
      return { top: String(r?.data?.topHit?.path ?? ''), name: String(r?.data?.topHit?.name ?? '') };
    };
    // Order containers: one containing the top hit of another's centre is above it.
    const stack: LayerReport['stack'] = [];
    for (const c of containers) stack.push({ id: c.testId, kind: c.kind, rect: c.rect ?? undefined });
    const screenArea = 1920 * 1080;
    let modal: string | null = null;
    const covered: LayerReport['covered'] = [];
    const buttons = snap.ofKind('button').filter(b => b.visible).slice(0, limit);
    for (const b of buttons) {
      const r: any = await client.call('hit_test', { testId: b.testId }, 3000).catch(() => null);
      const top = String(r?.data?.topHit?.path ?? ''), mine = String(r?.data?.targetDiagnostics?.path ?? '');
      // A non-interactive child (a title) whose clicks fall through to its own parent is not covered.
      if (top && mine && top !== mine && !top.startsWith(mine + '/') && !mine.startsWith(top + '/')) covered.push({ control: b.testId, coveredBy: String(r?.data?.topHit?.name ?? top) });
    }
    for (const c of containers) {
      if (c.kind !== 'window' || !c.rect || c.rect.w * c.rect.h < 0.6 * screenArea) continue;
      const t = await topAt(c);
      if (t.top && covered.some(x => x.coveredBy === t.name)) modal = c.testId;
    }
    const rep: LayerReport = { screen, stack, modal, covered, controls: buttons.length };
    this.data.layers[screen] = rep;
    this.save();
    return rep;
  }
}
