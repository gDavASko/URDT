// Read the CURRENT Gantt plan of a workgroup into plan.json: per-person ordered chains
// (sorted by START_DATE_PLAN), plus an "unplanned" bucket for tasks without plan dates.
// This file is both the edit base (copy → reorder → reflow) and the diff baseline (--orig).
// Usage: node read-plan.cjs --group <GROUP_ID> [--out plan.json] [--cx-field UF_TASKS_TASK_1783529349965] [--tz +03:00]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const groupId = Number(arg('group'));
if (!groupId) { console.error('Pass --group <GROUP_ID>'); process.exit(1); }
const out = arg('out', 'plan.json');
const cxField = arg('cx-field', 'UF_TASKS_TASK_1783529349965');
const cxKey = cxField.toLowerCase().replace(/_([a-z0-9])/g, (_, c) => c.toUpperCase());

async function main() {
  const all = [];
  let start = 0;
  for (;;) {
    const res = await fetch(`${base}/tasks.task.list.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({
        filter: { GROUP_ID: groupId },
        select: ['ID', 'TITLE', 'STATUS', 'RESPONSIBLE_ID', 'START_DATE_PLAN', 'END_DATE_PLAN', cxField],
        start,
      }),
    });
    const data = await res.json();
    if (data.error) { console.error(data.error_description || data.error); process.exit(1); }
    all.push(...data.result.tasks);
    if (data.next === undefined) break;
    start = data.next;
  }
  const planned = all.filter(t => t.startDatePlan);
  const byResp = new Map();
  for (const t of planned) {
    if (!byResp.has(t.responsibleId)) byResp.set(t.responsibleId, []);
    byResp.get(t.responsibleId).push(t);
  }
  const people = [];
  for (const [resp, list] of byResp) {
    list.sort((a, b) => new Date(a.startDatePlan) - new Date(b.startDatePlan)
      || new Date(a.endDatePlan) - new Date(b.endDatePlan) || Number(a.id) - Number(b.id));
    people.push({
      resp: Number(resp),
      anchor: list[0].startDatePlan.slice(0, 10),
      tasks: list.map(t => ({
        id: t.id, title: t.title, cx: Number(t[cxKey]) || 0,
        start: t.startDatePlan, end: t.endDatePlan,
      })),
    });
  }
  const plan = {
    group: groupId, timezone: arg('tz', '+05:00'), holidays: [],
    people,
    unplanned: all.filter(t => !t.startDatePlan).map(t => ({
      id: t.id, title: t.title, cx: Number(t[cxKey]) || 0, resp: Number(t.responsibleId),
    })),
  };
  fs.writeFileSync(out, JSON.stringify(plan, null, 2), 'utf8');
  console.log(`Plan of group ${groupId}: ${people.length} chains, ${planned.length} planned, ${plan.unplanned.length} unplanned -> ${out}`);
  for (const p of people) console.log(`  resp=${p.resp}: ${p.tasks.length} tasks, ${p.anchor} -> ${p.tasks[p.tasks.length - 1].end.slice(0, 10)}`);
}
main().catch(e => { console.error(e); process.exit(1); });
