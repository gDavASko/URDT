/**
 * GDD loader — parses the machine-readable part of a GDD markdown document.
 *
 * Human prose stays prose; every mechanic/screen carries one fenced block tagged `urdt-spec`
 * holding JSON. The three GDD tiers of the Game & Testing spec §4.1 map to:
 *   Macro (DoD)          → `dod` strings + the `goal` invariants
 *   Meso (invariants)    → `invariants` (post-conditions) and `exploits` (negative Hoare triples)
 *   Micro (hypotheses)   → formed at runtime by the playbook from live beacons
 */

import fs from 'node:fs';

export type CompareOp = '==' | '!=' | '>=' | '<=' | '>' | '<' | 'contains' | 'changed' | 'unchanged' | 'num>=' | 'num<=' | 'num==';

/** A predicate over a beacon. `beacon` is a testId, "@module" (the mechanic module) or "@screen". */
export interface InvariantSpec {
  id: string;
  text: string;
  beacon: string;
  path: string;            // property path, e.g. "IsCompleted" or "game.CurrentMass"
  op: CompareOp;
  value?: unknown;
  severity?: 'CRITICAL' | 'MAJOR' | 'MINOR';
}

/** Negative / exploit experiment: perform a deliberate wrong action and assert the game resists it. */
export interface ExploitSpec {
  id: string;
  text: string;
  action: 'drag_junk_to_slot' | 'drop_item_outside' | 'spam_tap' | 'wrong_pair';
  /** Optional explicit actors for the experiment. */
  junk?: string;
  target?: string;
  button?: string;
  expect: InvariantSpec[];
}

export interface ScenarioSpec {
  id: string;
  title: string;
  suite: 'ui' | '2d';
  /** Launch button of the mechanic in the catalog, or null for UI scenarios. */
  launch?: string;
  /** Beacon id of the mechanic module. */
  module?: string;
  playbook: string;
  params?: Record<string, any>;
  dod: string[];
  invariants: InvariantSpec[];
  exploits?: ExploitSpec[];
  timeoutMs: number;
  tags?: string[];
}

export interface NavigationSpec {
  home: string;                 // window beacon of the home screen
  entries: Record<string, { window: string; via: string[] }>;
  denylist: string[];           // buttons Mode 1 must never press (destructive / long-running)
  backButtons: string[];        // semantic "go back" controls
  explorePattern?: string;      // regex of controls Mode 1 may press (navigation, not in-screen gameplay)
}

export interface GddDocument {
  title: string;
  version: string;
  navigation: NavigationSpec;
  scenarios: ScenarioSpec[];
  sourcePath: string;
}

const BLOCK = /```urdt-(spec|navigation|meta)\s*\n([\s\S]*?)```/g;

export function loadGdd(path: string): GddDocument {
  const text = fs.readFileSync(path, 'utf-8');
  const doc: GddDocument = {
    title: 'untitled',
    version: '0',
    navigation: { home: '', entries: {}, denylist: [], backButtons: [] },
    scenarios: [],
    sourcePath: path,
  };
  let m: RegExpExecArray | null;
  while ((m = BLOCK.exec(text)) !== null) {
    let json: any;
    try {
      json = JSON.parse(m[2]);
    } catch (err) {
      throw new Error(`GDD ${path}: invalid JSON in urdt-${m[1]} block near offset ${m.index}: ${(err as Error).message}`);
    }
    if (m[1] === 'meta') {
      doc.title = json.title ?? doc.title;
      doc.version = json.version ?? doc.version;
    } else if (m[1] === 'navigation') {
      doc.navigation = { ...doc.navigation, ...json };
    } else {
      validateScenario(json, path);
      doc.scenarios.push(json as ScenarioSpec);
    }
  }
  if (doc.scenarios.length === 0) throw new Error(`GDD ${path}: no urdt-spec scenarios found`);
  return doc;
}

function validateScenario(s: any, path: string): void {
  const missing = ['id', 'title', 'suite', 'playbook', 'dod', 'invariants', 'timeoutMs'].filter(k => s[k] === undefined);
  if (missing.length) throw new Error(`GDD ${path}: scenario ${s.id ?? '?'} missing ${missing.join(', ')}`);
  for (const inv of s.invariants) {
    if (!inv.id || !inv.beacon || !inv.path || !inv.op) throw new Error(`GDD ${path}: scenario ${s.id} has malformed invariant ${JSON.stringify(inv)}`);
  }
}
