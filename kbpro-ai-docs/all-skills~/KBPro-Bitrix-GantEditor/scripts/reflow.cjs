// Recompute the Gantt after editing plan.json chain order (insert / move / reassign).
// Re-lays out EVERY chain from its anchor with the standard rules (workdays, cx days,
// 1h marker slots) — unchanged prefixes reproduce identical dates, downstream shifts.
// With --orig it also emits minimal change sets for fast application.
// Usage: node reflow.cjs --plan plan-new.json [--orig plan.json]
//        [--out updates.json] [--changes-dates changes-dates.json] [--changes-links changes-links.json]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const stripBom = s => s.replace(/^﻿/, '');
const plan = JSON.parse(stripBom(fs.readFileSync(arg('plan', 'plan-new.json'), 'utf8')));
const origPath = arg('orig', null);
const orig = origPath ? JSON.parse(stripBom(fs.readFileSync(origPath, 'utf8'))) : null;

const TZ = plan.timezone || '+05:00';
const HOLIDAYS = new Set(plan.holidays || []);
const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const isWorkday = d => d.getDay() !== 0 && d.getDay() !== 6 && !HOLIDAYS.has(iso(d));
const nextWorkday = d => { const x = new Date(d); do { x.setDate(x.getDate() + 1); } while (!isWorkday(x)); return x; };
const toWorkday = d => { const x = new Date(d); while (!isWorkday(x)) x.setDate(x.getDate() + 1); return x; };
const fmt = (d, time) => `${iso(d)}T${time}:00${TZ}`;
const WORK_START = plan.workStart || '10:00';
const WORK_END = plan.workEnd || '18:00';
const WORK_HOURS = Number(plan.workHours || 8);
const minutesOf = time => {
  const [h, m] = time.split(':').map(Number);
  return h * 60 + (m || 0);
};
const padTime = minutes => `${String(Math.floor(minutes / 60)).padStart(2, '0')}:${String(minutes % 60).padStart(2, '0')}`;
const startMin = minutesOf(WORK_START);
const endMin = minutesOf(WORK_END);
if (endMin - startMin !== WORK_HOURS * 60) {
  console.error(`[ERR] work window ${WORK_START}-${WORK_END} does not match workHours=${WORK_HOURS}`);
  process.exit(1);
}
const makeCursor = d => ({ date: toWorkday(d), minute: startMin });
const normalizeCursor = c => {
  c.date = toWorkday(c.date);
  if (c.minute < startMin) c.minute = startMin;
  if (c.minute >= endMin) {
    c.date = nextWorkday(c.date);
    c.minute = startMin;
  }
  return c;
};
const cloneCursor = c => ({ date: new Date(c.date), minute: c.minute });
const cursorIso = c => fmt(c.date, padTime(c.minute));
const advanceHours = (cursor, rawHours) => {
  let c = normalizeCursor(cloneCursor(cursor));
  let remaining = Math.max(1, Math.ceil(Number(rawHours) || 0)) * 60;
  const start = cloneCursor(c);
  while (remaining > 0) {
    const available = endMin - c.minute;
    if (remaining <= available) {
      c.minute += remaining;
      remaining = 0;
    } else {
      remaining -= available;
      c.date = nextWorkday(c.date);
      c.minute = startMin;
    }
  }
  return { start, end: cloneCursor(c) };
};

const updates = [];
const seen = new Set();
for (const person of plan.people) {
  if (!person.anchor) { console.error(`[ERR] resp=${person.resp}: no anchor date`); process.exit(1); }
  let cursor = makeCursor(new Date(person.anchor + 'T00:00:00'));
  let prev = null;
  let finish = null;
  for (const t of person.tasks) {
    if (seen.has(String(t.id))) { console.error(`[ERR] Task ${t.id} appears twice in plan`); process.exit(1); }
    seen.add(String(t.id));
    const hours = Math.max(1, Math.ceil(Number(t.cx) || 0));
    const span = advanceHours(cursor, hours);
    cursor = normalizeCursor(cloneCursor(span.end));
    finish = span.end;
    const start = cursorIso(span.start);
    const end = cursorIso(span.end);
    updates.push({ id: t.id, title: t.title, start, end, resp: person.resp, pred: prev ? prev.id : null, hours });
    prev = { id: t.id, endIso: end };
  }
  console.log(`resp=${person.resp}: ${person.tasks.length} tasks, finish ${finish ? `${iso(finish.date)} ${padTime(finish.minute)}` : '(no tasks)'}`);
}

fs.writeFileSync(arg('out', 'updates.json'), JSON.stringify(updates, null, 2), 'utf8');

if (orig) {
  const old = new Map();
  for (const p of orig.people) {
    let prev = null;
    for (const t of p.tasks) { old.set(String(t.id), { start: t.start, end: t.end, resp: p.resp, pred: prev }); prev = String(t.id); }
  }
  const inst = s => new Date(s).getTime(); // compare instants: portal may echo dates in another TZ
  const changesDates = updates.filter(u => {
    const o = old.get(String(u.id));
    return !o || inst(o.start) !== inst(u.start) || inst(o.end) !== inst(u.end) || Number(o.resp) !== Number(u.resp);
  });
  const changesLinks = updates.filter(u => {
    const o = old.get(String(u.id));
    return !o || String(o.pred) !== String(u.pred); // includes pred -> null (became chain head)
  });
  fs.writeFileSync(arg('changes-dates', 'changes-dates.json'), JSON.stringify(changesDates, null, 2), 'utf8');
  fs.writeFileSync(arg('changes-links', 'changes-links.json'), JSON.stringify(changesLinks, null, 2), 'utf8');
  console.log(`\nTotal ${updates.length}; changed dates/resp: ${changesDates.length}, changed links: ${changesLinks.length}`);
} else {
  console.log(`\nTotal ${updates.length} (no --orig: no change sets, apply full updates.json)`);
}
