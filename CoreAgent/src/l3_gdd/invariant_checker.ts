/**
 * Evaluates GDD invariants against live beacons (Hoare post-conditions / exploit expectations).
 */

import { InvariantSpec } from './gdd_loader.js';
import { Beacon, WorldModel } from '../perception/world_model.js';

export interface InvariantResult {
  id: string;
  text: string;
  pass: boolean;
  observed: unknown;
  expected: unknown;
  op: string;
  beacon: string;
  severity: string;
}

export function readPath(b: Beacon | null, path: string): unknown {
  if (!b) return undefined;
  if (path.startsWith('game.')) return b.game?.[path.slice(5)];
  if (path in b.props) return b.props[path];
  if (b.game && path in b.game) return b.game[path];
  return (b as any)[path];
}

function num(v: unknown): number {
  return typeof v === 'number' ? v : typeof v === 'string' ? parseFloat(v) : Number(v);
}

/** First number embedded in a value (e.g. "Баланс сил: 86% (Цель: 92%)" → 86). */
export function firstNumber(v: unknown): number {
  if (typeof v === 'number') return v;
  const m = String(v ?? '').match(/-?\d+(?:[.,]\d+)?/);
  return m ? parseFloat(m[0].replace(',', '.')) : NaN;
}

export function compare(observed: unknown, op: string, expected: unknown, baseline?: unknown): boolean {
  switch (op) {
    case '==': return typeof expected === 'number' ? Math.abs(num(observed) - expected) < 1e-3 : observed === expected;
    case '!=': return observed !== expected;
    case '>=': return num(observed) >= num(expected);
    case '<=': return num(observed) <= num(expected);
    case '>': return num(observed) > num(expected);
    case '<': return num(observed) < num(expected);
    case 'contains': return String(observed ?? '').includes(String(expected));
    case 'num>=': return firstNumber(observed) >= num(expected);
    case 'num<=': return firstNumber(observed) <= num(expected);
    case 'num==': return firstNumber(observed) === num(expected);
    case 'changed': return JSON.stringify(observed) !== JSON.stringify(baseline);
    case 'unchanged': return JSON.stringify(observed) === JSON.stringify(baseline);
    default: return false;
  }
}

export class InvariantChecker {
  /** Fallback for beacons that no longer exist (mechanic torn down after completion): last observation. */
  public fallback: ((testId: string) => Beacon | null) | null = null;

  constructor(private readonly world: WorldModel) {}

  private async look(testId: string): Promise<{ b: Beacon | null; latched: boolean }> {
    const live = await this.world.inspect(testId);
    if (live && live.active) return { b: live, latched: false };
    const old = this.fallback?.(testId) ?? null;
    return old ? { b: old, latched: true } : { b: live, latched: false };
  }

  public resolveId(beacon: string, moduleId?: string): string {
    return beacon === '@module' ? (moduleId ?? '') : beacon;
  }

  /** Captures the baseline values needed by `changed` / `unchanged` operators. */
  public async baseline(specs: InvariantSpec[], moduleId?: string): Promise<Map<string, unknown>> {
    const out = new Map<string, unknown>();
    for (const s of specs) {
      if (s.op === 'changed' || s.op === 'unchanged') {
        out.set(s.id, readPath((await this.look(this.resolveId(s.beacon, moduleId))).b, s.path));
      }
    }
    return out;
  }

  public async evaluate(specs: InvariantSpec[], moduleId?: string, baselines?: Map<string, unknown>): Promise<InvariantResult[]> {
    const results: InvariantResult[] = [];
    for (const s of specs) {
      const { b, latched } = await this.look(this.resolveId(s.beacon, moduleId));
      const observed = readPath(b, s.path);
      const base = baselines?.get(s.id);
      results.push({
        id: s.id,
        text: s.text,
        pass: b !== null && compare(observed, s.op, s.value, base),
        observed,
        expected: s.op === 'changed' || s.op === 'unchanged' ? `${s.op} from ${JSON.stringify(base)}` : s.value,
        op: s.op,
        beacon: latched ? `${s.beacon} (last observation)` : s.beacon,
        severity: s.severity ?? 'CRITICAL',
      });
    }
    return results;
  }
}
