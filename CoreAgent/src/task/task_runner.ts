/**
 * L3 task executor: turns a meta-AI task into verified play.
 *
 *   resolve   → complete the task from GDD/observation; ask the meta-AI when something is ambiguous
 *   reach     → navigate to the scope screen (explicit entry clicks, or a search for matching controls)
 *   play      → hint playbook, else universal explorer, else recognized skills, then explorer again
 *   judge     → success/forbid predicates (with completion latch), console errors, stalls, shortcuts
 *   report    → TaskResult JSON + evidence packet + HTML
 */

import fs from 'node:fs';
import path from 'node:path';
import { UrdtWireClient } from '../protocol/urdt_wire_client.js';
import { Beacon, WorldModel, WorldSnapshot, sleep } from '../perception/world_model.js';
import { MotorCortex } from '../l1_kinematics/motor_cortex.js';
import { MicroSlmArbiter } from '../l2_tactics/micro_slm_arbiter.js';
import { PlaybookContext } from '../l2_tactics/playbook_context.js';
import { PLAYBOOKS } from '../l2_tactics/playbooks/index.js';
import { loadGdd, ScenarioSpec } from '../l3_gdd/gdd_loader.js';
import { InvariantChecker } from '../l3_gdd/invariant_checker.js';
import { writeEvidence } from '../l3_gdd/audit_report.js';
import { UniversalExplorer } from './explorer.js';
import { recognizeSkills, motionBetween } from './skill_library.js';
import { buildBriefing, Briefing } from './briefing.js';
import { VisionAnalyst, loadVisionConfig } from './vision.js';
import { GameHearing, loadHearingConfig } from './hearing.js';
import { Answer, Clarification, Finding, Predicate, Task, TaskResult, TaskSchema } from './contract.js';

const NAV_BACK = /(back|main_menu|catalog)/i;

export interface RunnerDeps { client: UrdtWireClient; world: WorldModel; motor: MotorCortex; arbiter: MicroSlmArbiter; outDir: string; harnessDir: string; log: (s: string) => void }

export class TaskRunner {
  private readonly pending = new Map<string, Task>();
  private readonly results = new Map<string, TaskResult>();
  private consoleErrors: string[] = [];
  private lastBriefing: Briefing | undefined;
  private vision: VisionAnalyst | null | undefined;

  private visionAnalyst(): VisionAnalyst | null {
    if (this.vision === undefined) {
      const cfg = loadVisionConfig();
      this.vision = cfg ? new VisionAnalyst(cfg, this.d.log) : null;
    }
    return this.vision;
  }

  private hearing: GameHearing | null | undefined;

  private ears(): GameHearing | null {
    if (this.hearing === undefined) {
      const cfg = loadHearingConfig();
      this.hearing = cfg ? new GameHearing(cfg, this.d.client, this.d.log) : null;
    }
    return this.hearing;
  }

  dispose(): void { this.vision?.dispose(); this.hearing?.dispose(); }

  constructor(private readonly d: RunnerDeps) {
    d.client.on('event:log_error', (e: any) => { this.consoleErrors.push(String(e?.message ?? e?.msg ?? JSON.stringify(e))); });
  }

  getResult(taskId: string): TaskResult | undefined { return this.results.get(taskId); }

  /** Applies a meta-AI answer to a paused task and resumes it. */
  async answer(a: Answer): Promise<TaskResult> {
    const task = this.pending.get(a.taskId);
    if (!task) throw new Error(`no paused task ${a.taskId}`);
    const r = this.results.get(a.taskId);
    const option = r?.clarification?.options.find(o => o.id === a.optionId);
    const payload: any = option?.payload ?? {};
    if (a.success ?? payload.success) task.success.all = a.success ?? payload.success;
    if (a.forbid) task.forbid = [...task.forbid, ...a.forbid];
    if (a.scope ?? payload.scope) task.target.scope = a.scope ?? payload.scope;
    if (a.entry ?? payload.entry) task.target.entry = a.entry ?? payload.entry;
    if (a.note) task.context.designNotes = `${task.context.designNotes ?? ''}\n[meta-AI] ${a.note}`.trim();
    return this.run(task);
  }

  async run(input: unknown): Promise<TaskResult> {
    const task = TaskSchema.parse(input);
    const started = Date.now();
    const findings: Finding[] = [];
    const strategy: TaskResult['strategy'] = [];
    const step = (s: string, o: string) => { strategy.push({ t: Date.now(), step: s, outcome: o }); this.d.log(`[${task.taskId}] ${s} → ${o}`); };
    this.consoleErrors = [];
    this.lastBriefing = undefined;
    await this.d.client.waitReady(120000);
    await this.d.client.call('subscribe', { events: ['log_error'] }).catch(() => undefined);

    // ── 1. Resolve the task (GDD if referenced; otherwise observation + clarification) ──
    if (task.context.gddPath && task.context.gddScenarioId) {
      const gdd = loadGdd(task.context.gddPath);
      const sc = gdd.scenarios.find(s => s.id === task.context.gddScenarioId);
      if (sc) this.fromGdd(task, sc, step);
    }
    if (!task.target.scope) {
      const snap = await this.d.world.snapshot();
      const q = this.ask(task, 'scope', 'Which screen/module should I test?', 'The task names no scope beacon and none could be inferred from the goal.',
        snap.modules().map(m => ({ id: m.testId, label: `${m.testId} (visible now)`, payload: { scope: m.testId } })), { windows: snap.openWindows() });
      if (q) return this.finish(task, started, 'needs_clarification', 'scope unknown', findings, strategy, [], 0, q);
    }

    // ── 2. Reach the scope ──
    const reached = await this.reach(task, step, findings);
    if (!reached) {
      const snap = await this.d.world.snapshot();
      const cands = snap.ofKind('button').filter(b => b.visible && tokenOverlap(b.testId, task.target.scope!) > 0).slice(0, 6);
      const q = this.ask(task, 'entry', `I could not reach "${task.target.scope}". Which control opens it?`, 'No known route; similar controls are listed.',
        cands.map(c => ({ id: c.testId, label: c.testId, payload: { entry: [c.testId] } })), { screen: snap.openWindows() });
      if (q) return this.finish(task, started, 'needs_clarification', 'scope not reachable', findings, strategy, [], 0, q);
      return this.finish(task, started, 'blocked', `scope ${task.target.scope} not reachable`, findings, strategy, [], 0);
    }

    // ── 2b. Briefing: read captions and instructions, inventory the scene, one screenshot, candidate plans ──
    // Hearing: voice hints usually play on entering a level — listen to the last seconds plus a short wait.
    const heard: string[] = [];
    const ears = this.ears();
    if (ears) {
      await sleep(2500);
      const h = await ears.listen({ seconds: 12 });
      heard.push(...h.speech.map(s => s.text));
      step('hearing', `${h.audioSeconds.toFixed(1)}s audio, peak ${h.peak.toFixed(2)}, speech: ${h.speech.map(s => `"${s.text}"`).join(' ') || 'none'}, sounds: ${h.cues.map(c => `${c.source}:${c.clip}`).join(', ') || 'none'}`);
    }
    // Coverage: interactable objects inside the scope that carry no beacon (or only react to legacy OnMouse*)
    // are invisible/unreachable for L3 — report them so the meta-AI instruments them instead of chasing a phantom stall.
    const cov = await this.d.client.call('coverage', { scope: task.target.scope }, 5000).catch(() => null);
    const missing: Array<{ path: string; component: string; reason: string }> = cov?.data?.missing ?? [];
    step('coverage', cov?.data ? `${cov.data.interactables} interactable components, ${missing.length} unobservable` : 'unavailable');
    for (const m of missing) {
      findings.push({
        severity: m.reason === 'legacy_OnMouse' ? 'MAJOR' : 'MINOR', kind: 'UNOBSERVABLE',
        message: m.reason === 'legacy_OnMouse'
          ? `${m.path} (${m.component}) handles legacy OnMouse* — synthetic input cannot reach it; use EventSystem pointer handlers`
          : `${m.path} (${m.component}) is interactable but has no URDT beacon — L3 cannot see or press it`,
        evidence: m,
      });
    }
    const briefing: Briefing = await buildBriefing(this.d.world, this.d.client, task.target.scope!, path.join(this.d.outDir, task.taskId), true, this.visionAnalyst(), task.goal, heard);
    step('briefing', briefing.summary);
    this.lastBriefing = briefing;

    // ── 3. Success criteria (ask or assume) ──
    if (!task.success.all?.length) {
      const scope = await this.d.world.inspect(task.target.scope!);
      const options: Clarification['options'] = [];
      if (scope && 'IsCompleted' in scope.props) options.push({ id: 'completed', label: `${scope.testId}.IsCompleted == true`, payload: { success: [{ beacon: '@scope', path: 'IsCompleted', op: '==', value: true }] } });
      if (scope && 'ProgressNormalized' in scope.props) options.push({ id: 'progress', label: `${scope.testId}.ProgressNormalized >= 1`, payload: { success: [{ beacon: '@scope', path: 'ProgressNormalized', op: '>=', value: 1 }] } });
      const q = this.ask(task, 'success', `How do I know "${task.goal}" is achieved?`, task.success.description ? `Only a text description was given: "${task.success.description}".` : 'No success criteria given.',
        options, { scopeProps: scope?.props, scopeGame: scope?.game, briefing });
      if (q) return this.finish(task, started, 'needs_clarification', 'success criteria needed', findings, strategy, [], 0, q);
      task.success.all = (options[0]?.payload as any)?.success ?? [];
      findings.push({ severity: 'INFO', kind: 'ASSUMPTION', message: `autonomy=full: assumed success = ${options[0]?.label ?? 'none'}` });
    }

    // ── 4. Play ──
    const scenario: ScenarioSpec = { id: task.taskId, title: task.goal, suite: '2d', module: task.target.scope, playbook: task.hint?.playbook ?? 'explore', params: task.hint?.params ?? {}, dod: [task.goal], invariants: [], timeoutMs: task.budget.timeMs };
    const ctx = new PlaybookContext(this.d.client, this.d.world, this.d.motor, this.d.arbiter, scenario, false);
    ctx.startWatch();
    const deadline = started + task.budget.timeMs;
    const success = task.success.all!.map(p => ({ ...p, beacon: p.beacon === '@scope' || p.beacon === '@module' ? task.target.scope! : p.beacon }));
    const forbid = task.forbid.map(p => ({ ...p, beacon: p.beacon === '@scope' || p.beacon === '@module' ? task.target.scope! : p.beacon }));
    const learned: string[] = [];
    let actions = 0;
    const solvedNow = async () => ctx.latched.completed && success.every(s => s.path === 'IsCompleted' || s.path === 'ProgressNormalized')
      || (await this.evalPreds(success, ctx)).every(r => r.pass);

    // ── 4a. Audit probes (no GDD needed): defective items must be rejected by every receptacle ──
    if (task.audit) {
      const probed = await this.junkProbes(task, findings, step);
      if (probed && task.target.entry?.length) { await this.reach(task, step, findings); step('restart level after audit probes', 'fresh start'); }
    }

    if (task.hint && PLAYBOOKS[task.hint.playbook]) {
      const r = await PLAYBOOKS[task.hint.playbook](ctx);
      step(`hint playbook ${task.hint.playbook}`, `${r.status}: ${r.summary}`);
    }
    if (!(await solvedNow())) {
      const explore = async (share: number, label: string) => {
        const ex = new UniversalExplorer(this.d.world, this.d.motor, {
          scopeId: task.target.scope!, success, forbid, doNotTouch: task.doNotTouch, briefing,
          consult: this.visionAnalyst() ? async (failedAttempts: string[]) => {
            const uri = await VisionAnalyst.grabHdFrame(this.d.client);
            if (!uri) return null;
            const snap = await this.d.world.snapshot();
            const scopeB = snap.get(task.target.scope!);
            const parts = scopeB ? snap.within(scopeB).filter(b => b.visible && b.kind !== 'window' && b.kind !== 'module').slice(0, 60) : [];
            const file = path.join(this.d.outDir, task.taskId, `stuck_${Date.now()}.jpg`);
            fs.mkdirSync(path.dirname(file), { recursive: true });
            fs.writeFileSync(file, Buffer.from(uri.split(',')[1], 'base64'));
            const heardNow = ears ? (await ears.listen({ since: ears.cursor })).speech.map(x => `[voice] ${x.text}`) : [];
            if (heardNow.length) step('hearing (stuck)', heardNow.join(' '));
            const v = await this.visionAnalyst()!.analyze(uri, parts, [...briefing.texts.map(t => t.text), ...heardNow], task.goal, { w: 1920, h: 1080 }, file, failedAttempts);
            step('vision consult (stuck)', v ? `${v.goal} | ${v.plan.map(s => `${s.action}:${s.item ?? s.button ?? ''}${s.target ? '→' + s.target : ''}`).join(', ')} (${v.latencyMs}ms)` : 'no answer');
            return v && v.plan.length ? { kind: 'vision_plan', why: `vision after stagnation: ${v.goal}`, visionSteps: v.plan } : null;
          } : undefined,
          deadline: Math.min(deadline, Date.now() + (deadline - Date.now()) * share), maxActions: task.budget.actions - actions, log: this.d.log,
        });
        const out = await ex.run();
        actions += out.actions;
        learned.push(...out.learned);
        for (const s of out.steps) strategy.push({ t: s.t, step: `explorer:${s.step}`, outcome: s.outcome });
        this.junkFindings(out.steps, findings);
        step(`${label} (${out.actions} actions)`, out.solved || await solvedNow() ? 'goal reached' : 'goal not reached');
      };
      await explore(0.45, 'universal explorer');
      if (!(await solvedNow()) && Date.now() < deadline) {
        const s1 = await this.d.world.snapshot();
        await sleep(200);
        const s2 = await this.d.world.snapshot();
        const scope = s2.get(task.target.scope!);
        const skills = scope ? recognizeSkills(scope, s2.within(scope), motionBetween(s1, s2)) : [];
        step('skill recognition', skills.length ? skills.map(s => `${s.skill} (${s.why})`).join('; ') : 'no known structure');
        for (const sk of skills) {
          if (await solvedNow() || Date.now() > deadline) break;
          // Exploration may have left the level in a bad state (crashed car, half-filled slots): restart it
          // through the known route, as a human player would, before applying a structured skill.
          const cur = await this.d.world.inspect(task.target.scope!);
          if (task.target.entry?.length && Number(cur?.props.ProgressNormalized ?? 0) > 0) {
            const again = await this.reach(task, step, findings);
            step('restart level before skill', again ? 'fresh start' : 'restart failed');
          }
          (ctx.scenario as any).params = sk.params;
          (ctx as any).deadline = deadline;
          const r = sk.custom ? await sk.custom(ctx) : await PLAYBOOKS[sk.skill](ctx);
          step(`skill:${sk.skill}`, `${r.status}: ${r.summary}`);
          if (r.status === 'COMPLETED') learned.push(`recognized skill ${sk.skill}: ${sk.why}`);
        }
      }
      if (!(await solvedNow()) && Date.now() < deadline) await explore(1, 'explorer (second pass, remaining budget)');
    }
    ctx.stopWatch();

    // ── 5. Judge ──
    const checker = new InvariantChecker(this.d.world);
    checker.fallback = (id: string) => ctx.latched.beacons.get(id) ?? null;
    const sRes = await checker.evaluate(success.map((p, i) => ({ id: `S${i}`, text: p.text ?? `${p.beacon}.${p.path} ${p.op} ${JSON.stringify(p.value ?? '')}`, ...p } as any)), task.target.scope);
    const fRes = await checker.evaluate(forbid.map((p, i) => ({ id: `F${i}`, text: p.text ?? '', ...p } as any)), task.target.scope);
    const latchedSuccess = ctx.latched.completed && success.every(s => ['IsCompleted', 'ProgressNormalized'].includes(s.path));
    const successOk = sRes.every(r => r.pass) || latchedSuccess;
    for (const e of this.consoleErrors.slice(0, 10)) findings.push({ severity: 'MAJOR', kind: 'CONSOLE_ERROR', message: e });
    for (const f of fRes.filter(r => r.pass)) findings.push({ severity: 'CRITICAL', kind: 'FORBIDDEN_STATE', message: `forbidden state reached: ${f.beacon}.${f.id} (${JSON.stringify(f.observed)})` });
    if (!successOk) findings.push({ severity: 'CRITICAL', kind: 'GOAL_NOT_REACHED', message: `goal "${task.goal}" not reached within budget`, evidence: sRes });
    // Audit defects fail the verification even if the goal was reachable: the module does not match the design.
    const auditDefect = findings.some(f => f.kind === 'UNGUARDED_SHORTCUT' && (f.severity === 'MAJOR' || f.severity === 'CRITICAL'));
    const status = successOk && !fRes.some(r => r.pass) && !auditDefect ? 'success' : 'fail';
    const result = this.finish(task, started, status, successOk ? `goal reached${latchedSuccess && !sRes.every(r => r.pass) ? ' (completion latched before teardown)' : ''}` : 'goal not reached',
      findings, strategy, learned, actions, undefined,
      sRes.map((r, i) => ({ predicate: success[i], pass: r.pass || latchedSuccess, observed: r.observed })),
      fRes.map((r, i) => ({ predicate: forbid[i], violated: r.pass, observed: r.observed })));
    if (status !== 'success') {
      result.evidenceFile = writeEvidence(this.d.harnessDir, {
        $schema: 'urdt/failure_evidence_v1.json', incidentId: `INC_${task.taskId}`, timestamp: Date.now(), failingPhase: task.taskId,
        gddInvariantViolated: findings.map(f => f.message).join(' | ').slice(0, 400), stagnationType: 'TASK_FAILED', activeScene: 'URDT_TestPoligon_UI',
        beaconStateSnapshot: [...ctx.latched.beacons.values()].slice(0, 60).map(b => ({ id: b.testId, kind: b.kind, screenPixel: b.center, game: b.game })),
        actionHistoryBeforeFailure: this.d.motor.trace.slice(-30),
        diagnostics: { engineConsoleErrors: this.consoleErrors.slice(-20), activeModalDialog: null, recommendedAiFix: findings.map(f => `${f.kind}: ${f.message}`).join('\n') },
      });
    }
    return result;
  }

  private fromGdd(task: Task, sc: ScenarioSpec, step: (s: string, o: string) => void): void {
    task.target.scope ??= sc.module;
    task.target.entry ??= sc.launch ? [sc.launch] : undefined;
    if (!task.success.all?.length) task.success.all = sc.invariants.filter(i => i.beacon === '@module' || i.severity !== 'MINOR').map(i => ({ beacon: i.beacon === '@module' ? '@scope' : i.beacon, path: i.path, op: i.op as any, value: i.value, text: i.text }));
    step('task completed from GDD', `${sc.id}: scope ${sc.module}, ${task.success.all?.length} success predicates`);
  }

  /** Returns a clarification (and pauses the task) unless autonomy is full. */
  private ask(task: Task, id: string, question: string, why: string, options: Clarification['options'], observed: unknown): Clarification | null {
    if (task.autonomy === 'full' && options.length > 0) return null;
    if (task.autonomy === 'full' && options.length === 0) return null;
    this.pending.set(task.taskId, task);
    return { questionId: id, question, why, options, observed };
  }

  private async reach(task: Task, step: (s: string, o: string) => void, findings: Finding[]): Promise<boolean> {
    const scopeId = task.target.scope!;
    const visible = async () => (await this.d.world.inspect(scopeId))?.visible === true;
    // Always start the scope fresh: leave it first if it is already open.
    for (let i = 0; i < 4 && await visible() && task.target.entry?.length; i++) await this.pressBack();
    if (await visible()) { step('reach scope', 'already on screen'); return true; }
    const entry = task.target.entry ?? [];
    const plan = entry.length ? entry : await this.guessEntry(scopeId);
    if (plan.length) task.target.entry = plan;          // remember the route: used to restart the level
    if (!entry.length && plan.length) findings.push({ severity: 'INFO', kind: 'ASSUMPTION', message: `entry control guessed from names: ${plan.join(' → ')}` });
    for (const id of plan) {
      const found = await this.findControl(id);
      if (!found) { step(`find control ${id}`, 'not found on any explored screen'); return false; }
      const ctx = new PlaybookContext(this.d.client, this.d.world, this.d.motor, this.d.arbiter, { id: 'nav', title: '', suite: '2d', playbook: '', dod: [], invariants: [], timeoutMs: 20000 } as any, false);
      await ctx.ensureOnScreen(id);
      const ok = await ctx.tapUntil(id, async () => (await visible()) || !(await this.d.world.inspect(id))?.visible, 8, 600);
      step(`press ${id}`, ok ? 'screen changed' : 'no effect');
    }
    const ok = await visible();
    step('reach scope', ok ? `${scopeId} visible` : `${scopeId} not visible`);
    if (!ok) findings.push({ severity: 'MAJOR', kind: 'NAVIGATION', message: `could not open ${scopeId}` });
    return ok;
  }

  private async pressBack(): Promise<void> {
    const snap = await this.d.world.snapshot();
    const back = snap.ofKind('button').find(b => b.visible && NAV_BACK.test(b.testId) && /catalog/i.test(b.testId))
      ?? snap.ofKind('button').find(b => b.visible && NAV_BACK.test(b.testId));
    if (back) { await this.d.motor.tap({ testId: back.testId }); await sleep(700); }
  }

  /** Breadth-first search for a control over menus reachable through navigation buttons. */
  private async findControl(id: string): Promise<boolean> {
    const present = async () => { const b = await this.d.world.inspect(id); return !!b && b.active; };
    if (await present()) return true;
    const tried = new Set<string>();
    for (let round = 0; round < 6; round++) {
      await this.pressBack();
      if (await present()) return true;
      const snap = await this.d.world.snapshot();
      const nav = snap.ofKind('button').filter(b => b.visible && /^btn_(open|go|show)/i.test(b.testId) && !tried.has(b.testId));
      for (const b of nav) {
        tried.add(b.testId);
        await this.d.motor.tap({ testId: b.testId });
        await sleep(700);
        if (await present()) return true;
        await this.pressBack();
      }
    }
    return present();
  }

  /** Without explicit entry: the control whose name shares a distinctive token with the scope (e.g. m05). */
  private async guessEntry(scopeId: string): Promise<string[]> {
    const want = scopeId.match(/^[A-Za-z]+\d+/)?.[0]?.toLowerCase();
    for (let round = 0; round < 3; round++) {
      const snap = await this.d.world.snapshot();
      const hit = snap.beacons.find(b => b.kind === 'button' && want && b.testId.toLowerCase().endsWith(want));
      if (hit) return [hit.testId];
      const open = snap.ofKind('button').find(b => b.visible && /^btn_open_/i.test(b.testId) && /2d|game|play|level/i.test(b.testId));
      if (open) { await this.d.motor.tap({ testId: open.testId }); await sleep(800); }
    }
    return [];
  }

  private async evalPreds(preds: Predicate[], ctx: PlaybookContext): Promise<Array<{ pass: boolean }>> {
    const checker = new InvariantChecker(this.d.world);
    checker.fallback = (id: string) => ctx.latched.beacons.get(id) ?? null;
    return checker.evaluate(preds.map((p, i) => ({ id: `P${i}`, text: '', ...p } as any)));
  }

  /** Drags each junk-looking item onto the receptacle it most resembles and checks that the game refuses it. */
  private async junkProbes(task: Task, findings: Finding[], step: (s: string, o: string) => void): Promise<boolean> {
    const snap = await this.d.world.snapshot();
    const scope = snap.get(task.target.scope!);
    if (!scope) return false;
    const parts = snap.within(scope);
    const junk = parts.filter(b => b.kind === 'draggable' && b.visible && (b.props.IsJunk === true || Object.entries(b.game ?? {}).some(([k, v]) => /junk|broken/i.test(k) && v === true))).slice(0, 2);
    const receptacles = parts.filter(b => (b.kind === 'slot' || (b.kind === 'draggable' && b.game?.IsSource === false)) && b.visible && Object.keys(b.game ?? {}).length > 0);
    if (!receptacles.length) return false;
    let probed = false;
    // Cross-type probe: receptacles are "typed" when one of their attributes takes values that also appear in an
    // attribute of the items (ItemTypeId=red ↔ AcceptedTypeId=red|blue). An item dropped into a receptacle of
    // another type must be refused.
    const items = parts.filter(b => b.kind === 'draggable' && b.visible && !junk.includes(b) && Object.keys(b.game ?? {}).length > 0);
    const strVals = (b: Beacon) => Object.entries(b.game ?? {}).filter(([, v]) => typeof v === 'string' && v !== '') as Array<[string, string]>;
    let mismatch: { item: Beacon; target: Beacon; why: string } | null = null;
    for (const it of items) {
      for (const [ki, vi] of strVals(it)) {
        for (const r of receptacles) {
          for (const [kr, vr] of strVals(r)) {
            const typed = receptacles.some(o => strVals(o).some(([k2, v2]) => k2 === kr && v2.toLowerCase() === vi.toLowerCase()));
            if (typed && vr.toLowerCase() !== vi.toLowerCase() && receptacles.filter(o => strVals(o).some(([k2]) => k2 === kr)).length > 1) {
              mismatch ??= { item: it, target: r, why: `${it.testId}.${ki}=${vi} vs ${r.testId}.${kr}=${vr}` };
            }
          }
        }
      }
    }
    if (mismatch) {
      probed = true;
      const before = await this.d.world.inspect(scope.testId);
      const rBefore = await this.d.world.inspect(mismatch.target.testId);
      await this.d.motor.drag(mismatch.item.center, mismatch.target.center, { dwellEndMs: 150 }, `AUDIT ${mismatch.item.testId}`);
      await sleep(600);
      const after = await this.d.world.inspect(scope.testId);
      const rAfter = await this.d.world.inspect(mismatch.target.testId);
      const iAfter = await this.d.world.inspect(mismatch.item.testId);
      const countUp = Number(rAfter?.game?.CurrentCount ?? 0) > Number(rBefore?.game?.CurrentCount ?? 0);
      const placed = iAfter ? Object.entries(iAfter.game ?? {}).some(([k, v]) => v === true && /snapped|locked|installed|connected|equipped|deposited/i.test(k)) : false;
      const progressed = Number(after?.props.ProgressNormalized ?? 0) > Number(before?.props.ProgressNormalized ?? 0) + 1e-4;
      const accepted = countUp || placed || progressed;
      step(`audit probe (cross-type): ${mismatch.why}`, accepted ? 'ACCEPTED (defect)' : 'rejected (ok)');
      if (accepted) findings.push({ severity: 'MAJOR', kind: 'UNGUARDED_SHORTCUT', message: `receptacle accepted an item of another type: ${mismatch.why} (count ${rBefore?.game?.CurrentCount}→${rAfter?.game?.CurrentCount}, placed=${placed})`, evidence: { item: iAfter?.game, receptacle: rAfter?.game } });
    }
    for (const j of junk) {
      probed = true;
      const r = receptacles[0];
      const before = await this.d.world.inspect(scope.testId);
      await this.d.motor.drag(j.center, r.center, { dwellEndMs: 150 }, `AUDIT ${j.testId}`);
      await sleep(600);
      const after = await this.d.world.inspect(scope.testId);
      const jAfter = await this.d.world.inspect(j.testId);
      const placed = jAfter ? (jAfter.props.IsSnapped === true || Object.entries(jAfter.game ?? {}).some(([k, v]) => v === true && /snapped|locked|installed|connected|equipped|deposited/i.test(k))) : false;
      const progressed = Number(after?.props.ProgressNormalized ?? 0) > Number(before?.props.ProgressNormalized ?? 0) + 1e-4;
      const accepted = placed || progressed;
      step(`audit probe: ${j.testId} → ${r.testId}`, accepted ? 'ACCEPTED (defect)' : 'rejected (ok)');
      if (accepted) findings.push({ severity: 'MAJOR', kind: 'UNGUARDED_SHORTCUT', message: `defective item ${j.testId} was accepted by ${r.testId} (placed=${placed}, progress ${Number(before?.props.ProgressNormalized ?? 0).toFixed(2)}→${Number(after?.props.ProgressNormalized ?? 0).toFixed(2)})`, evidence: { item: jAfter?.game, receptacle: r.testId } });
    }
    return probed;
  }

  private junkFindings(steps: Array<{ step: string; outcome: string }>, findings: Finding[]): void {
    for (const s of steps) {
      if (/^drag .*(junk|broken|defect|glitch|stone|mud)/i.test(s.step) && s.outcome.startsWith('reward')) {
        findings.push({ severity: 'MAJOR', kind: 'UNGUARDED_SHORTCUT', message: `a junk/defective item was accepted and rewarded: ${s.step}` });
      }
    }
  }

  private finish(task: Task, started: number, status: TaskResult['status'], summary: string, findings: Finding[], strategy: TaskResult['strategy'], learned: string[], actions: number,
    clarification?: Clarification, success: TaskResult['success'] = [], forbidden: TaskResult['forbidden'] = []): TaskResult {
    const result: TaskResult = { taskId: task.taskId, status, summary, clarification, success, forbidden, findings, strategy, learnedSkills: [...new Set(learned)], actions, durationMs: Date.now() - started, briefing: this.lastBriefing };
    this.lastBriefing = undefined;
    const dir = path.join(this.d.outDir, task.taskId);
    fs.mkdirSync(dir, { recursive: true });
    result.reportFile = path.join(dir, 'result.json');
    fs.writeFileSync(path.join(dir, 'task.json'), JSON.stringify(task, null, 2), 'utf-8');
    fs.writeFileSync(result.reportFile, JSON.stringify(result, null, 2), 'utf-8');
    this.results.set(task.taskId, result);
    if (status !== 'needs_clarification') this.pending.delete(task.taskId);
    return result;
  }
}

function tokenOverlap(a: string, b: string): number {
  const ta = new Set(a.toLowerCase().split(/[^a-z0-9]+/).filter(t => t.length > 1));
  return b.toLowerCase().split(/[^a-z0-9]+/).filter(t => t.length > 1 && ta.has(t)).length;
}

export type { WorldSnapshot, Beacon };
