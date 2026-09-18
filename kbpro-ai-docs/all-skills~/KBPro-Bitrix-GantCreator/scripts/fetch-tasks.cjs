// Fetch all tasks of a Bitrix24 workgroup into tasks.json (slim shape for the scheduler).
// Usage: node fetch-tasks.cjs --group <GROUP_ID> [--out tasks.json] [--cx-field UF_TASKS_TASK_1783529349965]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const groupId = Number(arg('group'));
if (!groupId) { console.error('Pass --group <GROUP_ID>'); process.exit(1); }
const out = arg('out', 'tasks.json');
const cxField = arg('cx-field', 'UF_TASKS_TASK_1783529349965');
const cxKey = cxField.toLowerCase().replace(/_([a-z0-9])/g, (_, c) => c.toUpperCase()).replace(/^uf/, 'uf');

async function main() {
  const all = [];
  let start = 0;
  for (;;) {
    const res = await fetch(`${base}/tasks.task.list.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filter: { GROUP_ID: groupId },
        select: ['ID', 'TITLE', 'STATUS', 'RESPONSIBLE_ID', 'DEADLINE', 'START_DATE_PLAN',
                 'END_DATE_PLAN', 'PARENT_ID', 'TAGS', cxField],
        start,
      }),
    });
    const data = await res.json();
    if (data.error) { console.error(data.error_description || data.error); process.exit(1); }
    all.push(...data.result.tasks);
    if (data.next === undefined) break;
    start = data.next;
  }
  const slim = all.map(t => ({
    id: t.id, title: t.title, status: t.status,
    responsibleId: t.responsibleId, deadline: t.deadline,
    startDatePlan: t.startDatePlan, endDatePlan: t.endDatePlan,
    complexity: t[cxKey],
    tags: Object.values(t.tags || {}).map(x => x.title),
  }));
  fs.writeFileSync(out, JSON.stringify(slim, null, 2), 'utf8');
  console.log(`Fetched ${slim.length} tasks of group ${groupId} -> ${out}`);
}
main().catch(e => { console.error(e); process.exit(1); });
