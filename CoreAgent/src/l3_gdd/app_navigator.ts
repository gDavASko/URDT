/**
 * Mode 1 — Runtime Graphify & navigation.
 *
 * A screen node is the set of open windows plus the active mechanic module (if any); an edge is a real
 * click on a button that changed the node. Exploration presses every non-denylisted visible button once
 * per node (bounded budget), records the transition and walks back. Mode 2 then path-finds over the
 * recorded multigraph (BFS) instead of replaying hardcoded routes; if the target screen is unknown the
 * navigator reports DISCOVERY_REQUIRED (Mode transition contract, architecture §2).
 */

import fs from 'node:fs';
import { WorldModel, WorldSnapshot, sleep } from '../perception/world_model.js';
import { MotorCortex } from '../l1_kinematics/motor_cortex.js';
import { NavigationSpec } from './gdd_loader.js';
import { MicroSlmArbiter } from '../l2_tactics/micro_slm_arbiter.js';

export interface ScreenNode {
  id: string;
  windows: string[];
  module: string | null;
  buttons: string[];
  firstSeen: string;
}

export interface ScreenEdge { from: string; to: string; via: string; clicks: number }

export interface ApplicationMap {
  generatedAt: string;
  nodes: Record<string, ScreenNode>;
  edges: ScreenEdge[];
  structuralDefects: Array<{ type: string; node: string; detail: string }>;
}

export function nodeIdOf(snap: WorldSnapshot): { id: string; windows: string[]; module: string | null } {
  const windows = snap.openWindows();
  const mod = snap.modules().find(m => m.visible)?.testId ?? null;
  return { id: `${windows.join('+') || 'no_window'}${mod ? `#${mod}` : ''}`, windows, module: mod };
}

export class AppNavigator {
  public map: ApplicationMap = { generatedAt: new Date().toISOString(), nodes: {}, edges: [], structuralDefects: [] };

  private readonly edgeFailures = new Map<string, number>();

  constructor(
    private readonly world: WorldModel,
    private readonly motor: MotorCortex,
    private readonly nav: NavigationSpec,
    private readonly log: (s: string) => void,
    private readonly arbiter?: MicroSlmArbiter,
  ) {}

  private isDenied(id: string): boolean {
    return this.nav.denylist.some(p => id === p || (p.endsWith('*') && id.startsWith(p.slice(0, -1))));
  }

  public load(path: string): boolean {
    if (!fs.existsSync(path)) return false;
    this.map = JSON.parse(fs.readFileSync(path, 'utf-8'));
    return true;
  }

  public save(path: string): void {
    fs.writeFileSync(path, JSON.stringify(this.map, null, 2), 'utf-8');
  }

  public async current(): Promise<{ id: string; snap: WorldSnapshot }> {
    const snap = await this.world.snapshot();
    const n = nodeIdOf(snap);
    this.observe(snap);
    return { id: n.id, snap };
  }

  private observe(snap: WorldSnapshot): ScreenNode {
    const n = nodeIdOf(snap);
    const buttons = snap.ofKind('button').filter(b => b.visible && b.center.y > 0 && b.center.y < 1080).map(b => b.testId).sort();
    const node = this.map.nodes[n.id] ?? { id: n.id, windows: n.windows, module: n.module, buttons, firstSeen: new Date().toISOString() };
    node.buttons = Array.from(new Set([...node.buttons, ...buttons])).sort();
    this.map.nodes[n.id] = node;
    return node;
  }

  private addEdge(from: string, to: string, via: string): void {
    const e = this.map.edges.find(x => x.from === from && x.to === to && x.via === via);
    if (e) e.clicks++;
    else this.map.edges.push({ from, to, via, clicks: 1 });
  }

  /** Clicks `testId`; returns the resulting node id and the last protocol error (if any). */
  public async pressDetailed(testId: string, attempts = 3): Promise<{ node: string; changed: boolean; error?: string }> {
    const before = await this.current();
    let error: string | undefined;
    for (let attempt = 0; attempt < attempts; attempt++) {
      const r = await this.motor.tap({ testId });
      error = r.ok ? undefined : r.error;
      await sleep(350);
      const after = await this.current();
      if (after.id !== before.id) {
        this.addEdge(before.id, after.id, testId);
        this.log(`  nav: ${before.id} --[${testId}]--> ${after.id}`);
        return { node: after.id, changed: true };
      }
      if (error && /E_NOT_FOUND/.test(error)) break;
      if (error && /E_NOT_HITTABLE/.test(error)) {
        // L2 modal interrupt (spec §3.2 priority 2): something covers the control — identify and dismiss it.
        if (!(await this.resolveBlocker(testId))) await sleep(700); // e.g. a victory card that fades by itself
      }
    }
    return { node: before.id, changed: false, error };
  }

  /**
   * Finds what covers `testId` via a read-only hit test, collects the controls that belong to the blocker
   * (their own top hit lies under the blocker's hierarchy path) and presses the one chosen as "dismiss".
   */
  public async resolveBlocker(testId: string): Promise<boolean> {
    const target = await this.world.inspect(testId);
    if (!target) return false;
    const hits = await this.world.hitTest(target.center);
    const top = hits[0];
    if (!top?.path || String(top.path).endsWith(target.name)) return false;
    const blockerPath: string = top.path;
    const snap = await this.world.snapshot();
    const inside: Array<{ id: string; text: string }> = [];
    for (const b of snap.ofKind('button').filter(x => x.visible && x.testId !== testId)) {
      const h = await this.world.hitTest(b.center);
      // A control belongs to the blocker only if it is itself hittable (its own top hit) inside the blocker subtree.
      const p0 = String(h[0]?.path ?? '');
      if (p0.startsWith(blockerPath) && (p0.endsWith(`/${b.name}`) || p0.includes(`/${b.name}/`))) inside.push({ id: b.testId, text: String(h[0].text ?? b.name) });
    }
    this.log(`  L2: ${testId} is covered by "${top.name}" (${blockerPath}); controls inside: ${inside.map(i => i.id).join(', ') || 'none'}`);
    if (inside.length === 0) return false;
    let choice = inside.find(i => /close|dismiss|cancel|back|ok|закр|отмен|назад/i.test(`${i.id} ${i.text}`))?.id;
    if (!choice && this.arbiter) {
      const d = await this.arbiter.decideAction({
        macroGoal: `dismiss the blocking window "${top.name}"`, activeModalId: String(top.name),
        beacons: inside.map(i => ({ id: i.id, text: i.text })), stuckCount: 0,
      });
      this.log(`  L2: arbiter(${d.source}, ${d.latencyMs}ms) → ${d.targetBeaconId}`);
      choice = d.action === 'WAIT' ? undefined : d.targetBeaconId;
    }
    if (!choice) return false;
    const r = await this.motor.tap({ testId: choice });
    await sleep(300);
    this.log(`  L2: dismissed "${top.name}" via ${choice} (${r.ok ? 'ok' : r.error})`);
    return r.ok;
  }

  public async press(testId: string): Promise<string> {
    return (await this.pressDetailed(testId)).node;
  }

  private dropEdge(from: string, via: string, why: string): void {
    const before = this.map.edges.length;
    this.map.edges = this.map.edges.filter(e => !(e.from === from && e.via === via));
    if (this.map.edges.length !== before) this.log(`  nav: dropped stale edge ${from} --[${via}]--> (${why})`);
  }

  /** Bounded exploration of the screen graph from the current node (Mode 1). */
  public async explore(budget = 40, depthLimit = 2): Promise<ApplicationMap> {
    const pattern = this.nav.explorePattern ? new RegExp(this.nav.explorePattern) : null;
    const deny = (id: string) => this.isDenied(id) || (pattern !== null && !pattern.test(id));
    const start = (await this.current()).id;
    const visited = new Set<string>();
    let clicks = 0;

    const dfs = async (nodeId: string, depth: number): Promise<void> => {
      if (visited.has(nodeId) || depth > depthLimit || clicks >= budget) return;
      visited.add(nodeId);
      const node = this.map.nodes[nodeId];
      for (const btn of node.buttons) {
        if (clicks >= budget || deny(btn) || this.nav.backButtons.includes(btn)) continue;
        if ((await this.current()).id !== nodeId && !(await this.goto(nodeId))) return;
        clicks++;
        const next = await this.press(btn);
        if (next === nodeId) continue;
        await dfs(next, depth + 1);
        if (!(await this.goto(nodeId))) {
          this.map.structuralDefects.push({ type: 'DEAD_END', node: next, detail: `no known route back to ${nodeId} after ${btn}` });
          return;
        }
      }
    };

    await dfs(start, 0);
    for (const [id, node] of Object.entries(this.map.nodes)) {
      const outgoing = this.map.edges.filter(e => e.from === id).length;
      if (outgoing === 0 && node.buttons.length === 0) {
        this.map.structuralDefects.push({ type: 'NO_EXIT', node: id, detail: 'screen exposes no actionable control' });
      }
    }
    this.map.generatedAt = new Date().toISOString();
    return this.map;
  }

  /** BFS over known edges. */
  public route(from: string, predicate: (nodeId: string) => boolean): ScreenEdge[] | null {
    if (predicate(from)) return [];
    const prev = new Map<string, ScreenEdge>();
    const queue = [from];
    const seen = new Set([from]);
    while (queue.length) {
      const cur = queue.shift()!;
      for (const e of this.map.edges.filter(x => x.from === cur)) {
        if (seen.has(e.to)) continue;
        seen.add(e.to);
        prev.set(e.to, e);
        if (predicate(e.to)) {
          const path: ScreenEdge[] = [];
          let k: string | undefined = e.to;
          while (k && prev.has(k)) { const pe: ScreenEdge = prev.get(k)!; path.unshift(pe); k = pe.from; }
          return path;
        }
        queue.push(e.to);
      }
    }
    return null;
  }

  /**
   * Navigates to a node matching `predicate`: BFS over known edges; stale edges (renamed / missing
   * controls, repeated no-ops) are dropped; with no known route the agent retreats through a semantic
   * back control, and as a last resort the Micro-SLM arbiter chooses among the visible controls.
   */
  public async navigate(predicate: (nodeId: string) => boolean, label: string): Promise<boolean> {
    const tried = new Set<string>();
    for (let hop = 0; hop < 12; hop++) {
      const { id, snap } = await this.current();
      if (predicate(id)) return true;
      const path = this.route(id, predicate);
      if (path && path.length) {
        const edge = path[0];
        const r = await this.pressDetailed(edge.via, 2);
        if (!r.changed) {
          const key = `${edge.from}|${edge.via}`;
          const fails = (this.edgeFailures.get(key) ?? 0) + 1;
          this.edgeFailures.set(key, fails);
          if ((r.error && /E_NOT_FOUND/.test(r.error)) || fails >= 2) this.dropEdge(edge.from, edge.via, r.error ?? 'no effect twice');
        }
        continue;
      }
      const visible = snap.ofKind('button').filter(b => b.visible && b.center.y > 0 && b.center.y < 1080 && !this.isDenied(b.testId) && !tried.has(b.testId));
      const back = this.nav.backButtons.find(bid => visible.some(v => v.testId === bid));
      let choice: string | undefined = back;
      if (!choice && this.arbiter && visible.length > 0) {
        const d = await this.arbiter.decideAction({
          macroGoal: `navigate to screen "${label}"`,
          activeModalId: null,
          screenSummary: `current screen ${id}`,
          beacons: visible.map(v => ({ id: v.testId, text: String(v.props.Text ?? v.name), controlType: 'button' })),
          stuckCount: hop,
        });
        this.log(`  nav: arbiter(${d.source}, ${d.latencyMs}ms) → ${d.targetBeaconId} (${d.diagnosis})`);
        if (d.action !== 'WAIT') choice = d.targetBeaconId;
      }
      if (!choice) break;
      tried.add(choice);
      await this.pressDetailed(choice, 2);
    }
    const ok = predicate((await this.current()).id);
    if (!ok) this.log(`  nav: DISCOVERY_REQUIRED — no known route to ${label}`);
    return ok;
  }

  public async goto(nodeId: string): Promise<boolean> {
    return this.navigate(id => id === nodeId, nodeId);
  }
}
