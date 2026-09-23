/**
 * Perception layer: turns URDT query/inspect payloads into a normalized, typed beacon snapshot.
 * No pixels are read — every fact comes from the contractual beacon graph (architecture §1.2).
 */

import { UrdtWireClient } from '../protocol/urdt_wire_client.js';

export interface Rect { x: number; y: number; w: number; h: number }
export interface Point { x: number; y: number }

export type BeaconKind =
  | 'module' | 'draggable' | 'slot' | 'area' | 'button' | 'text' | 'window' | 'toggle' | 'slider'
  | 'dropdown' | 'input' | 'scroll' | 'stick' | 'drawing' | 'generic';

export interface Beacon {
  testId: string;
  name: string;
  handle: string;
  active: boolean;
  visible: boolean;
  interactable: boolean;
  kind: BeaconKind;
  component: string;
  targetKind: string;
  center: Point;
  rect: Rect | null;
  /** Z rotation in degrees, normalized to (-180, 180] (from the transform slice). */
  rotationZ: number;
  /** Merged beacon properties (component-level [TestInspectable] members). */
  props: Record<string, any>;
  /** Live read-only gameplay state (GameState) if the beacon exposes it. */
  game: Record<string, any>;
}

const KIND_BY_COMPONENT: Record<string, BeaconKind> = {
  Urdt2DModuleTarget: 'module',
  Urdt2DDraggableTarget: 'draggable',
  Urdt2DSlotTarget: 'slot',
  Urdt2DInteractiveAreaTarget: 'area',
  UrdtUiButtonTarget: 'button',
  UrdtUiTextTarget: 'text',
  UrdtUiWindowTarget: 'window',
  UrdtUiToggleTarget: 'toggle',
  UrdtUiSliderTarget: 'slider',
  UrdtUiDropdownTarget: 'dropdown',
  UrdtUiInputTarget: 'input',
  UrdtUiScrollTarget: 'scroll',
  UrdtUiStickTarget: 'stick',
  UrdtUiDrawingTarget: 'drawing',
  UrdtUiGenericTarget: 'generic',
  UrdtDebugTarget: 'generic',
};

const RECT_PATTERN = /x:([-\d.]+), y:([-\d.]+), width:([-\d.]+), height:([-\d.]+)/;

export function parseRect(value: unknown): Rect | null {
  if (typeof value !== 'string') return null;
  const m = value.match(RECT_PATTERN);
  return m ? { x: +m[1], y: +m[2], w: +m[3], h: +m[4] } : null;
}

export function rectContains(r: Rect, p: Point, pad = 0): boolean {
  return p.x >= r.x - pad && p.x <= r.x + r.w + pad && p.y >= r.y - pad && p.y <= r.y + r.h + pad;
}

export function dist(a: Point, b: Point): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

export function normalizeNode(node: any): Beacon {
  const components: Record<string, any> = node.components ?? {};
  let component = 'none';
  let props: Record<string, any> = {};
  for (const [name, slice] of Object.entries(components)) {
    if (name === 'RectTransform' || name === 'Transform') continue;
    if (KIND_BY_COMPONENT[name] || (slice && (slice as any).TargetId)) {
      if (component === 'none') component = name;
      props = { ...slice as any, ...props };
    }
  }
  const center: Point = props.ScreenCenter ?? node.screenPosition ?? { x: 0, y: 0 };
  const rawRot = Number((components.RectTransform ?? components.Transform)?.rotation?.z ?? 0);
  const rotationZ = rawRot > 180 ? rawRot - 360 : rawRot;
  return {
    testId: node.testId,
    name: node.name,
    handle: node.handle,
    active: node.activeInHierarchy !== false,
    visible: props.IsVisible !== false && node.activeInHierarchy !== false,
    interactable: props.IsInteractable !== false,
    kind: KIND_BY_COMPONENT[component] ?? 'generic',
    component,
    targetKind: props.TargetKind ?? '',
    center: { x: center.x, y: center.y },
    rect: parseRect(props.ScreenRect),
    rotationZ,
    props,
    game: props.GameState ?? {},
  };
}

export class WorldSnapshot {
  public readonly byId = new Map<string, Beacon>();
  constructor(public readonly beacons: Beacon[], public readonly takenAt = Date.now()) {
    for (const b of beacons) this.byId.set(b.testId, b);
  }
  get(testId: string): Beacon | undefined { return this.byId.get(testId); }
  active(): Beacon[] { return this.beacons.filter(b => b.active); }
  ofKind(kind: BeaconKind): Beacon[] { return this.beacons.filter(b => b.active && b.kind === kind); }
  modules(): Beacon[] { return this.ofKind('module'); }
  /** Windows that are currently shown — the "screen" node of the application graph. */
  openWindows(): string[] {
    return this.beacons.filter(b => b.kind === 'window' && b.active && b.visible).map(b => b.testId).sort();
  }
  /** Beacons under a module's screen rect (a module's own children). */
  within(container: Beacon, kinds?: BeaconKind[]): Beacon[] {
    if (!container.rect) return [];
    return this.beacons.filter(b => b.active && b !== container && rectContains(container.rect!, b.center, 2)
      && (!kinds || kinds.includes(b.kind)));
  }
}

export class WorldModel {
  constructor(private readonly client: UrdtWireClient) {}

  public async snapshot(): Promise<WorldSnapshot> {
    const data = await this.client.request<{ matches: any[] }>('query', { selector: {} }, 15000);
    return new WorldSnapshot((data.matches ?? []).map(normalizeNode));
  }

  public async inspect(testId: string): Promise<Beacon | null> {
    const res = await this.client.call('inspect', { testId });
    if (res.status !== 'ok' || !res.data) return null;
    return normalizeNode(res.data);
  }

  /** Polls a beacon until `predicate` holds or the timeout expires; returns the last observation. */
  public async waitFor(testId: string, predicate: (b: Beacon) => boolean, timeoutMs: number, pollMs = 60): Promise<{ ok: boolean; beacon: Beacon | null; waitedMs: number }> {
    const start = Date.now();
    let last: Beacon | null = null;
    while (Date.now() - start <= timeoutMs) {
      last = await this.inspect(testId);
      if (last && predicate(last)) return { ok: true, beacon: last, waitedMs: Date.now() - start };
      await sleep(pollMs);
    }
    return { ok: false, beacon: last, waitedMs: Date.now() - start };
  }

  /** Top raycast hit texts at a screen point (used to read dropdown options, overlays). */
  public async hitTest(p: Point): Promise<any[]> {
    const res = await this.client.call('hit_test', { x: p.x, y: p.y });
    return res.status === 'ok' ? (res.data?.hits ?? []) : [];
  }
}

export function sleep(ms: number): Promise<void> {
  return new Promise(r => setTimeout(r, ms));
}
