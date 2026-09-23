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
import { AnswerSchema, TaskSchema } from '../task/contract.js';
import { describeScene, getRuntime, log } from '../task/runtime.js';

const ALLOWED_ACTIONS = ['click', 'double_click', 'drag', 'press_move', 'swipe', 'scroll', 'key_press', 'type_text', 'pointer_down', 'pointer_up', 'inspect', 'query', 'hit_test', 'capture', 'audio', 'input_status', 'coverage'];

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

await server.connect(new StdioServerTransport());
log('URDT MCP server ready (stdio)');
