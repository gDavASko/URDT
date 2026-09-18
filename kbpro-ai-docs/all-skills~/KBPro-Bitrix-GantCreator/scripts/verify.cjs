// Verify applied plan: samples N tasks per person and checks dates + DEPENDS_ON
// against updates.json. Always include chain joints (module transitions) in review.
// Usage: node verify.cjs [--updates updates.json] [--per-person 4]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const updates = JSON.parse(fs.readFileSync(arg('updates', 'updates.json'), 'utf8').replace(/^﻿/, ''));
const perPerson = Number(arg('per-person', 4));

const byPerson = new Map();
for (const u of updates) {
  if (!byPerson.has(u.resp)) byPerson.set(u.resp, []);
  byPerson.get(u.resp).push(u);
}
const sample = [];
for (const [, list] of byPerson) {
  const idx = new Set([0, 1, Math.floor(list.length / 2), list.length - 1]);
  let n = 0;
  for (const i of idx) { if (n++ >= perPerson) break; sample.push(list[i]); }
}

async function main() {
  let bad = 0;
  for (const u of sample) {
    const res = await fetch(`${base}/task.item.getdata.json`, {
      method: 'POST', headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ TASKID: Number(u.id) }),
    });
    const r = (await res.json()).result || {};
    const dep = (r.DEPENDS_ON || []).map(Number);
    const wantDep = u.pred ? [Number(u.pred)] : [];
    const depOk = JSON.stringify(dep) === JSON.stringify(wantDep);
    // getdata returns dates in server TZ (+03:00) — compare instants, not strings
    const dateOk = r.START_DATE_PLAN && new Date(r.START_DATE_PLAN).getTime() === new Date(u.start).getTime();
    if (!depOk || !dateOk) bad++;
    console.log(`${depOk && dateOk ? 'OK ' : 'FAIL'} ${u.id} dep=[${dep}] want=[${wantDep}] start=${r.START_DATE_PLAN} | ${(r.TITLE || '').slice(0, 50)}`);
    await new Promise(r2 => setTimeout(r2, 250));
  }
  console.log(`\nSampled ${sample.length}, failures: ${bad}`);
  if (bad) process.exit(1);
}
main().catch(e => { console.error(e); process.exit(1); });
