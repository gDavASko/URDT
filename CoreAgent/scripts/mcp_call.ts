/** Calls one URDT MCP tool through a real MCP client over stdio.  npx tsx scripts/mcp_call.ts <tool> <args.json> */
import fs from 'node:fs';
import { Client } from '@modelcontextprotocol/sdk/client/index.js';
import { StdioClientTransport } from '@modelcontextprotocol/sdk/client/stdio.js';
const [tool, argsFile] = process.argv.slice(2);
const args = argsFile ? JSON.parse(fs.readFileSync(argsFile, 'utf-8')) : {};
const client = new Client({ name: 'meta-ai', version: '1.0.0' });
await client.connect(new StdioClientTransport({ command: process.execPath, args: ['node_modules/tsx/dist/cli.mjs', 'src/mcp/urdt_mcp_server.ts'], cwd: process.cwd(), stderr: 'ignore' }));
const r: any = await client.callTool({ name: tool, arguments: args });
console.log(r.content?.[0]?.text ?? JSON.stringify(r));
await client.close();
process.exit(0);
