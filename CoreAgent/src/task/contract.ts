/**
 * Meta-AI ↔ L3 contract.
 *
 * The meta-AI (the coding agent that built the module, or any orchestrator) owns the knowledge: it states
 * WHAT to verify and WHAT counts as success. L3 owns HOW: it reaches the screen, perceives the beacons,
 * plays with honest input, and reports evidence. When the task is ambiguous, L3 does not guess — it returns
 * `needs_clarification` with concrete options drawn from what it observed, and resumes on the answer.
 *
 * Every schema here is also documented in `.claude/skills/urdt-game-verification/SKILL.md`.
 */

import { z } from 'zod';

/** A predicate over one beacon property (same language as GDD invariants). */
export const PredicateSchema = z.object({
  beacon: z.string().describe('testId, or "@scope" for the scope root (e.g. the mechanic module)'),
  path: z.string().describe('property, e.g. "IsCompleted", "ProgressNormalized", "game.CurrentMass", "Text"'),
  op: z.enum(['==', '!=', '>=', '<=', '>', '<', 'contains', 'changed', 'unchanged', 'num>=', 'num<=', 'num==']),
  value: z.unknown().optional(),
  text: z.string().optional(),
});
export type Predicate = z.infer<typeof PredicateSchema>;

export const TaskSchema = z.object({
  taskId: z.string().default(() => `task_${Date.now().toString(36)}`),
  /** Human-language goal ("Complete the snap-to-slot puzzle", "Check that the new shop button buys a sword"). */
  goal: z.string(),
  /** Why this task exists: module name, changed files, design intent. Used for reports and clarifications. */
  context: z.object({
    module: z.string().optional(),
    changedFiles: z.array(z.string()).optional(),
    designNotes: z.string().optional(),
    gddPath: z.string().optional(),
    gddScenarioId: z.string().optional(),
    /** Design text for the current module (GDD section); read by the briefing like on-screen captions. */
    gddText: z.string().optional(),
  }).default({}),
  /** Where to play. Either a scope beacon to reach (L3 finds a route) or explicit entry clicks. */
  target: z.object({
    scope: z.string().optional().describe('beacon that must be visible, e.g. "M01_SnapToSlot"; its rect bounds exploration'),
    entry: z.array(z.string()).optional().describe('explicit clicks to reach the screen (catalog cards, menu buttons)'),
    home: z.string().optional().describe('window beacon of the home screen (default window_main_menu)'),
    mode: z.enum(['single', 'campaign']).default('single').describe('campaign: press entry (e.g. "run all levels") and play every module that appears, in whatever order the game presents them, until the run ends'),
  }).default({}),
  /**
   * Success = ALL predicates true. If omitted or given only as text, L3 proposes candidate predicates from the
   * live scene and asks for confirmation (needs_clarification) unless `autonomy` is "full".
   */
  success: z.object({
    all: z.array(PredicateSchema).optional(),
    description: z.string().optional(),
  }).default({}),
  /** Things that must never happen (checked continuously and at the end). */
  forbid: z.array(PredicateSchema).default([]),
  /** Controls L3 must not press (destructive, purchases, reset...). Supports trailing * wildcard. */
  doNotTouch: z.array(z.string()).default([]),
  /** Optional known strategy (a playbook name + params) — a hint, not required. */
  hint: z.object({ playbook: z.string(), params: z.record(z.unknown()).default({}) }).optional(),
  autonomy: z.enum(['ask', 'full']).default('ask').describe('"ask": clarify ambiguities with the meta-AI; "full": decide alone and report assumptions'),
  budget: z.object({
    timeMs: z.number().default(120000),
    actions: z.number().default(400),
  }).default({}),
  /** Also hunt for defects beyond the goal: exceptions, stalls, unguarded shortcuts, unobservable entities. */
  audit: z.boolean().default(true),
});
export type Task = z.infer<typeof TaskSchema>;

export const ClarificationSchema = z.object({
  questionId: z.string(),
  question: z.string(),
  why: z.string(),
  options: z.array(z.object({ id: z.string(), label: z.string(), payload: z.unknown().optional() })),
  observed: z.unknown().optional().describe('what L3 already knows (candidate beacons/properties)'),
});
export type Clarification = z.infer<typeof ClarificationSchema>;

export const AnswerSchema = z.object({
  taskId: z.string(),
  questionId: z.string(),
  /** Pick an option id, or override with explicit content. */
  optionId: z.string().optional(),
  success: z.array(PredicateSchema).optional(),
  forbid: z.array(PredicateSchema).optional(),
  scope: z.string().optional(),
  entry: z.array(z.string()).optional(),
  note: z.string().optional(),
});
export type Answer = z.infer<typeof AnswerSchema>;

export type Finding = {
  severity: 'CRITICAL' | 'MAJOR' | 'MINOR' | 'INFO';
  kind: 'GOAL_NOT_REACHED' | 'FORBIDDEN_STATE' | 'CONSOLE_ERROR' | 'STALL' | 'UNOBSERVABLE' | 'UNGUARDED_SHORTCUT' | 'OCCLUDED' | 'LAYOUT'
    | 'INPUT_REJECTED' | 'NAVIGATION' | 'ASSUMPTION' | 'LEARNED';
  message: string;
  evidence?: unknown;
};

export interface TaskResult {
  taskId: string;
  status: 'success' | 'fail' | 'needs_clarification' | 'blocked' | 'running';
  summary: string;
  clarification?: Clarification;
  success: Array<{ predicate: Predicate; pass: boolean; observed: unknown }>;
  forbidden: Array<{ predicate: Predicate; violated: boolean; observed: unknown }>;
  findings: Finding[];
  /** How the goal was reached: which skills/strategies, which learned rules. */
  strategy: Array<{ t: number; step: string; outcome: string }>;
  learnedSkills: string[];
  /** What L3 read and inferred before acting (texts, captions, verbs, plans, screenshot file for the meta-AI). */
  briefing?: unknown;
  actions: number;
  durationMs: number;
  evidenceFile?: string;
  reportFile?: string;
  /** When L3 could not complete the goal: everything a meta-AI needs to write a controller (urdt_submit_skill). */
  skillRequest?: string;
  /** Knowledge used/produced: game knowledge folder, skills tried with their tier. */
  knowledge?: { dir: string; skillsTried: Array<{ skill: string; tier: string; ok: boolean }> };
}
