/**
 * Real MCP round trip, as a meta-AI would do it: spawn the URDT MCP server over stdio, list tools, run a
 * task file, answer a clarification if one comes back, print the final result.
 *   npx tsx scripts/mcp_roundtrip.ts <task.json> [answer.json]
 */
import fs from 'node:fs';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';

const [taskFile, answerFile] = process.argv.slice(2);
const transport = new StdioClientTransport({ command: process.execPath, args: ['--import', 'tsx', 'src/mcp/urdt_mcp_server.ts'], stderr: 'inherit' });
const client = new Client({ name: 'meta-ai-roundtrip', version: '1.0.0' });
await client.connect(transport);
const tools = await client.listTools();
console.error(`tools: ${tools.tools.map(t => t.name).join(', ')}`);
const call = async (name: string, args: Record<string, unknown>) => {
  const r: any = await client.callTool({ name, arguments: args }, undefined, { timeout: 600000 });
  return JSON.parse(r.content[0].text);
};
let result = await call('urdt_run_task', JSON.parse(fs.readFileSync(taskFile, 'utf-8')));
if (result.status === 'needs_clarification') {
  console.error(`clarification: ${result.clarification.question} options=${result.clarification.options.map((o: any) => o.id).join('/')}`);
  const answer = answerFile ? JSON.parse(fs.readFileSync(answerFile, 'utf-8')) : { optionId: result.clarification.options[0]?.id };
  result = await call('urdt_answer', { ...answer, taskId: result.taskId, questionId: result.clarification.questionId });
}
console.log(JSON.stringify(result, null, 2));
await client.close();
process.exit(0);
