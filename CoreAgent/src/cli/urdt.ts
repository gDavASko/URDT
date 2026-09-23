/**
 * URDT CLI for scripts and CI (same L3 agent as the MCP server).
 *
 *   urdt status
 *   urdt observe [scope]
 *   urdt run <task.json>                      → prints TaskResult JSON; exit 0 success, 1 fail, 2 needs_clarification, 3 blocked
 *   urdt run <task.json> --answer <answer.json>   resume a clarified task in one go
 *
 * (npx tsx src/cli/urdt.ts …  or  npm run urdt -- …)
 */

import fs from 'node:fs';
import { describeScene, getRuntime, shutdown } from '../task/runtime.js';

const [cmd, a1, ...rest] = process.argv.slice(2);
const flag = (n: string) => { const i = rest.indexOf(n); return i >= 0 ? rest[i + 1] : undefined; };
const EXIT: Record<string, number> = { success: 0, fail: 1, needs_clarification: 2, blocked: 3 };

try {
  if (cmd === 'status') {
    const { client } = await getRuntime();
    console.log(JSON.stringify((await client.call('health', {})).data, null, 2));
  } else if (cmd === 'observe') {
    console.log(JSON.stringify(await describeScene(a1), null, 2));
  } else if (cmd === 'run' && a1) {
    const { runner } = await getRuntime({ slm: rest.includes('--slm') });
    let result = await runner.run(JSON.parse(fs.readFileSync(a1, 'utf-8')));
    const answerFile = flag('--answer');
    if (result.status === 'needs_clarification' && answerFile) {
      result = await runner.answer({ ...JSON.parse(fs.readFileSync(answerFile, 'utf-8')), taskId: result.taskId });
    }
    console.log(JSON.stringify(result, null, 2));
    shutdown();
    process.exit(EXIT[result.status] ?? 1);
  } else {
    console.log('usage: urdt status | observe [scope] | run <task.json> [--answer answer.json] [--slm]');
    process.exit(64);
  }
  shutdown();
  process.exit(0);
} catch (err) {
  console.error(err);
  shutdown();
  process.exit(4);
}
