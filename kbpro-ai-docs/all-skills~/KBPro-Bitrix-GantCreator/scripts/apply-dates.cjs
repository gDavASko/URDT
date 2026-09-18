// Apply plan dates + responsible + person-hour estimate/duration from updates.json via batch (50 cmds per call).
// Batching plain tasks.task.update is SAFE (unlike task.dependence.* — never batch those).
// Usage: node apply-dates.cjs [--updates updates.json]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const updates = JSON.parse(fs.readFileSync(arg('updates', 'updates.json'), 'utf8').replace(/^﻿/, ''));

async function main() {
  let ok = 0; const errors = [];
  for (let i = 0; i < updates.length; i += 50) {
    const chunk = updates.slice(i, i + 50);
    const cmd = {};
    for (const u of chunk) {
      cmd['u' + u.id] = `tasks.task.update?taskId=${u.id}` +
        `&fields[START_DATE_PLAN]=${encodeURIComponent(u.start)}` +
        `&fields[END_DATE_PLAN]=${encodeURIComponent(u.end)}` +
        `&fields[RESPONSIBLE_ID]=${u.resp}` +
        `&fields[UF_TASKS_TASK_1783529349965]=${Number(u.hours) || 1}` +
        `&fields[DURATION_TYPE]=hours` +
        `&fields[DURATION_PLAN]=${Number(u.hours) || 1}`;
    }
    const res = await fetch(`${base}/batch.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ halt: 0, cmd }),
    });
    const data = await res.json();
    const errObj = (data.result && data.result.result_error) || {};
    ok += Object.keys((data.result && data.result.result) || {}).length;
    for (const [k, v] of Object.entries(errObj)) errors.push(`${k}: ${JSON.stringify(v)}`);
    console.log(`batch ${i / 50 + 1}: ok so far ${ok}, errors ${errors.length}`);
  }
  console.log(`\nDone: ${ok}/${updates.length} updated`);
  if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
}
main().catch(e => { console.error(e); process.exit(1); });
