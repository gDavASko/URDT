/**
 * URDT MCP server — exposes the L3 game-verification agent to a meta-AI (Claude Code, Codex, …).
 *
 *   urdt_status        connection + runtime readiness
 *   urdt_observe       what L3 sees (windows, modules, beacons with live GameState)
 *   urdt_run_task      run a verification task (see contract.ts / the urdt-game-verification skill)
 *   urdt_answer        answer a needs_clarification question and resume the task
 *   urdt_get_result    re-read a finished task result
 *   urdt_act           one honest-input action (click/drag/…) for manual probing by the meta-AI
 *
 * Register:  claude mcp add urdt -- npx --prefix E:/Projects/URDT/CoreAgent tsx E:/Projects/URDT/CoreAgent/src/mcp/urdt_mcp_server.ts
 */

import { McpServer } from '@modelcontextprotocol/sdk/server/mcp.js';
import { StdioServerTransport } from '@modelcontextprotocol/sdk/server/stdio.js';
import { z } from 'zod';
import path from 'node:path';
import { pathToFileURL } from 'node:url';
import { AnswerSchema, TaskSchema } from '../task/contract.js';
import { describeScene, getRuntime, log, shutdown } from '../task/runtime.js';

const ALLOWED_ACTIONS = ['click', 'double_click', 'drag', 'press_move', 'swipe', 'scroll', 'key_press', 'type_text', 'pointer_down', 'pointer_up', 'inspect', 'query', 'hit_test', 'capture', 'audio', 'input_status', 'coverage', 'layout'];

const text = (v: unknown) => ({ content: [{ type: 'text' as const, text: typeof v === 'string' ? v : JSON.stringify(v, null, 2) }] });

const server = new McpServer({ name: 'urdt', version: '1.0.0' });

server.registerTool('urdt_status', {
  description: 'Check that the Unity game (URDT server, Play Mode) is reachable and ready.',
  inputSchema: {},
}, async () => {
  const { client } = await getRuntime();
  const h = await client.call('health', {});
  return text({ connected: client.isReady, health: h.data });
});

server.registerTool('urdt_observe', {
  description: 'Describe what the game agent perceives now: open windows, mechanic modules and beacons (id, kind, role, position, text, live GameState). Optional scope limits it to one module/window.',
  inputSchema: { scope: z.string().optional() },
}, async ({ scope }) => text(await describeScene(scope)));

server.registerTool('urdt_run_task', {
  description: 'Run a gameplay verification task in the live Unity game with honest input. Returns status success | fail | needs_clarification | blocked, success/forbid predicate results, findings (defects, shortcuts, console errors, assumptions), the strategy used and learned skills. Read the urdt-game-verification skill for the task format.',
  inputSchema: TaskSchema.shape,
}, async (task) => {
  const { runner } = await getRuntime();
  return text(await runner.run(task));
});

server.registerTool('urdt_answer', {
  description: 'Answer a needs_clarification question (choose optionId or give explicit success/forbid/scope/entry) and resume the paused task.',
  inputSchema: AnswerSchema.shape,
}, async (answer) => {
  const { runner } = await getRuntime();
  return text(await runner.answer(AnswerSchema.parse(answer)));
});

server.registerTool('urdt_get_result', {
  description: 'Return the stored result of a task by id.',
  inputSchema: { taskId: z.string() },
}, async ({ taskId }) => {
  const { runner } = await getRuntime();
  return text(runner.getResult(taskId) ?? { error: `unknown task ${taskId}` });
});

server.registerTool('urdt_act', {
  description: `Send one URDT wire action (honest input or read-only query) for manual probing. Allowed: ${ALLOWED_ACTIONS.join(', ')}. Payload is the URDT payload, e.g. {"testId":"btn_open_2d_suite"} for click.`,
  inputSchema: { action: z.enum(ALLOWED_ACTIONS as [string, ...string[]]), payload: z.record(z.unknown()).default({}) },
}, async ({ action, payload }) => {
  const { client } = await getRuntime();
  return text(await client.call(action, payload));
});

server.registerTool('urdt_submit_skill', {
  description: 'Submit a controller (skill) for this game, e.g. in answer to a result.skillRequest. The source must `export default async function (api) {…}` using only the api (state, parts, inspect, motor.tap/drag/hold/press/release/slice, sleep, expired, say). It is stored as a CANDIDATE in the game URDT_Knowledge folder and runs first when its requirements match; it reaches the shared core library only through the regression gate.',
  inputSchema: {
    name: z.string(), description: z.string(), source: z.string(),
    requires: z.object({ scopeGame: z.array(z.string()).optional(), beacons: z.array(z.string()).optional(), buttons: z.array(z.string()).optional() }),
  },
}, async (input) => {
  const { runner } = await getRuntime();
  const { store } = await runner.knowledge();
  const r = store.submitCandidate(input);
  if (!r.ok) return text({ accepted: false, reason: r.reason });
  try { await import(pathToFileURL(path.join(r.dir!, 'controller.ts')).href + `?v=${Date.now()}`); }
  catch (err) { return text({ accepted: false, reason: `does not compile/load: ${(err as Error).message}`, dir: r.dir }); }
  return text({ accepted: true, tier: 'candidate', version: r.version, dir: r.dir });
});

server.registerTool('urdt_skills', {
  description: 'Skill library for the connected game: core skills (shared, gated) and candidates of this game, with verified outcome statistics per module.',
  inputSchema: {},
}, async () => {
  const { runner } = await getRuntime();
  const { store } = await runner.knowledge();
  const stats = store.stats();
  return text({
    gameKnowledge: store.gameDir,
    core: store.coreManifests().map(m => ({ name: m.name, version: m.version, source: m.source, stats: stats[m.name] ?? null })),
    candidates: store.candidateManifests().map(m => ({ name: m.name, version: m.version, requires: m.requires, stats: stats[m.name] ?? null, evidence: m.evidence.length })),
  });
});

server.registerTool('urdt_knowledge', {
  description: 'Facts the agent learned about this game (routes, match rules, fail causes, which skill solved which module), with the build they were observed on and confidence.',
  inputSchema: { prefix: z.string().optional() },
}, async ({ prefix }) => {
  const { runner } = await getRuntime();
  const { store } = await runner.knowledge();
  return text({ dir: store.gameDir, build: store.build, facts: store.facts().filter(f => !prefix || f.key.startsWith(prefix)) });
});

server.registerTool('urdt_map', {
  description: 'Application map of this game: summary (screens, transitions, builds), route to a module/window, unvisited controls (coverage), navigation regressions vs earlier builds.',
  inputSchema: { action: z.enum(['summary', 'route', 'unvisited', 'regressions', 'full']).default('summary'), target: z.string().optional() },
}, async ({ action, target }) => {
  const { runner, world } = await getRuntime();
  const { map } = await runner.knowledge();
  if (action === 'route') {
    const here = map.see(await world.snapshot());
    return text({ from: here, route: target ? map.route(here, n => n.modules.includes(target) || n.windows.includes(target)) : null });
  }
  if (action === 'unvisited') return text(map.unvisited());
  if (action === 'regressions') return text(map.regressions());
  if (action === 'full') return text({ nodes: map.nodes, edges: map.edges });
  return text(map.summary());
});

server.registerTool('urdt_layers', {
  description: 'UI layers of the current screen: window/module stack, modal, visible controls covered by something else (cannot be pressed), and layout/localization defects (truncation, overflow, unreadable font, missing glyphs).',
  inputSchema: { scope: z.string().optional() },
}, async ({ scope }) => {
  const { runner, world, client } = await getRuntime();
  const { map } = await runner.knowledge();
  const layers = await map.layers(await world.snapshot(), client);
  const layout: any = await client.call('layout', { path_contains: scope ?? '', audit: true }, 5000).catch(() => null);
  return text({ layers, layout: layout?.data ?? null });
});

await server.connect(new StdioServerTransport());
// The client owns this process: when it closes stdin (or goes away) release the URDT session and exit, otherwise
// an orphaned server keeps the game's single session busy.
const bye = () => { try { shutdown(); } finally { process.exit(0); } };
process.stdin.on('end', bye);
process.stdin.on('close', bye);
log('URDT MCP server ready (stdio)');
