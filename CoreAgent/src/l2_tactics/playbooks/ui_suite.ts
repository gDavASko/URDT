/**
 * UI playbook: executes GDD step lists where every step is a Hoare triple
 *   { do: <action>, target, value?, expect: [{ path, op, value }] }
 * Targets are re-inspected before every action (controls are re-shuffled on each Play Mode), scrolled into
 * the viewport with real swipes, and every step is verified by the beacon delta — never by the response alone.
 */

import { Beacon, rectContains, sleep } from '../../perception/world_model.js';
import { PlaybookContext, PlaybookResult } from '../playbook_context.js';
import { compare, readPath } from '../../l3_gdd/invariant_checker.js';

export interface UiStep {
  do: 'click' | 'double_click' | 'type' | 'key' | 'set_slider' | 'select_option' | 'scroll' | 'wait';
  target?: string;
  value?: any;
  repeat?: number;
  expect?: Array<{ path: string; op: string; value?: unknown; beacon?: string }>;
  pauseMs?: number;
}

async function ensureInViewport(ctx: PlaybookContext, testId: string, viewportId?: string): Promise<Beacon | null> {
  for (let i = 0; i < 60; i++) {
    const b = await ctx.world.inspect(testId);
    if (!b) return null;
    const vp = viewportId ? await ctx.world.inspect(viewportId) : null;
    if (!vp?.rect || rectContains(vp.rect, b.center, -4)) return b;
    const towardBottom = b.center.y < vp.rect.y;
    await ctx.motor.swipe(viewportId!, towardBottom ? 'up' : 'down', 180);
    await sleep(120);
  }
  return ctx.world.inspect(testId);
}

async function checkExpect(ctx: PlaybookContext, step: UiStep, before: Map<string, unknown>): Promise<{ ok: boolean; detail: string }> {
  const details: string[] = [];
  let ok = true;
  for (const e of step.expect ?? []) {
    const id = e.beacon ?? step.target!;
    const b = await ctx.world.inspect(id);
    const v = readPath(b, e.path);
    const pass = compare(v, e.op, e.value, before.get(`${id}.${e.path}`));
    details.push(`${id}.${e.path}=${JSON.stringify(v)} ${e.op} ${JSON.stringify(e.value ?? before.get(`${id}.${e.path}`))} → ${pass ? 'ok' : 'FAIL'}`);
    ok &&= pass;
  }
  return { ok, detail: details.join('; ') };
}

async function selectOption(ctx: PlaybookContext, dropdownId: string, label: string): Promise<void> {
  const dd = await ctx.world.inspect(dropdownId);
  if (!dd?.rect) return;
  await ctx.motor.tap({ testId: dropdownId });
  await sleep(300);
  for (let y = 0; y <= 1080; y += 16) {
    const hits = await ctx.world.hitTest({ x: dd.rect.x + dd.rect.w / 2, y });
    if (hits.some((h: any) => h.text === label)) {
      await ctx.motor.tap({ point: { x: dd.rect.x + dd.rect.w / 2, y } });
      return;
    }
  }
  ctx.say(`option "${label}" not discovered by live hit_test`);
}

export async function uiScenario(ctx: PlaybookContext): Promise<PlaybookResult> {
  const steps = (ctx.params.steps ?? []) as UiStep[];
  const viewport = ctx.params.viewport as string | undefined;
  let done = 0;
  for (const step of steps) {
    for (let rep = 0; rep < (step.repeat ?? 1); rep++) {
      if (ctx.expired()) return { status: 'TIMEOUT', summary: `${done}/${steps.length} steps`, stagnationType: 'TIMEOUT' };
      if (step.do === 'wait') { await sleep(Number(step.value ?? 1000)); continue; }
      let ok = false;
      let detail = '';
      for (let attempt = 1; attempt <= 3 && !ok; attempt++) {
        const target = step.target ? await ensureInViewport(ctx, step.target, viewport) : null;
        if (step.target && !target) return { status: 'DISCOVERY_REQUIRED', summary: `${step.target} not observable` };
        const before = new Map<string, unknown>();
        for (const e of step.expect ?? []) {
          const id = e.beacon ?? step.target!;
          before.set(`${id}.${e.path}`, readPath(await ctx.world.inspect(id), e.path));
        }
        const h = ctx.hypothesize(`${step.do} ${step.target ?? ''} ${step.value !== undefined ? JSON.stringify(step.value) : ''} ⇒ ${(step.expect ?? []).map(e => `${e.path} ${e.op} ${JSON.stringify(e.value ?? 'Δ')}`).join(', ')} (attempt ${attempt})`);
        switch (step.do) {
          case 'click': await ctx.motor.tap({ testId: step.target! }); break;
          case 'double_click': await ctx.motor.doubleTap(step.target!); break;
          case 'type': await ctx.motor.typeText(String(step.value)); break;
          case 'key': await ctx.motor.key(String(step.value)); break;
          case 'scroll': await ctx.motor.scroll(step.target!, Number(step.value)); break;
          case 'select_option': await selectOption(ctx, step.target!, String(step.value)); break;
          case 'set_slider': {
            const r = target!.rect!;
            const to = { x: r.x + r.w * Number(step.value), y: r.y + r.h / 2 };
            await ctx.motor.drag(target!.center, to, { durationMs: 300, dwellEndMs: 60, tremorPx: 0 }, `SLIDER→${step.value}`);
            break;
          }
        }
        await sleep(step.pauseMs ?? 300);
        ({ ok, detail } = await checkExpect(ctx, step, before));
        ctx.resolve(h, ok);
        if (!ok) ctx.say(`  ${detail}`);
      }
      if (!ok) return { status: 'FAILED', summary: `step ${done + 1} (${step.do} ${step.target ?? ''}) failed: ${detail}`, stagnationType: 'MICRO_STUCK' };
    }
    done++;
  }
  return { status: 'COMPLETED', summary: `${done}/${steps.length} UI steps verified` };
}
