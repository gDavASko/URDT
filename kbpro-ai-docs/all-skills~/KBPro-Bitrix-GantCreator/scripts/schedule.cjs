// Gantt scheduler: lays tasks out on a workday calendar per person, module-sequential,
// with a sounds/voiceover tail and in-chain zero-complexity markers (Dev pattern).
// Input: tasks.json (fetch-tasks.cjs) + config.json. Output: updates.json + summary.
// Usage: node schedule.cjs [--tasks tasks.json] [--config config.json] [--out updates.json]
//
// config.json shape (see examples/project94-config.json):
// {
//   "timezone": "+05:00", "holidays": ["2026-11-04"],
//   "titleRegex": "^\\[Dev\\]\\[([^\\]]+)\\]\\s*",          // group 1 = task type
//   "typePriority": ["логика","туторы","графика","анимации","эффекты","звуки","озвучка","отладка"],
//   "tailTypes": ["звуки","озвучка"],                        // scheduled at the person's chain end
//   "people": { "Андрей": { "id": 20, "start": "2026-07-15" }, ... },
//   "plan": [ { "person": "Андрей",
//               "blocks": [ { "module": "...", "submodule": null|"...", "pd": 7 } ] }, ... ]
// }
// "pd" per block is optional; when present it is validated against the sum of complexities.
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const stripBom = s => s.replace(/^﻿/, '');
const tasks = JSON.parse(stripBom(fs.readFileSync(arg('tasks', 'tasks.json'), 'utf8')));
const cfg = JSON.parse(stripBom(fs.readFileSync(arg('config', 'config.json'), 'utf8')));
const out = arg('out', 'updates.json');

const TZ = cfg.timezone || '+05:00';
const TYPE_RE = new RegExp(cfg.titleRegex || '^\\[Dev\\]\\[([^\\]]+)\\]\\s*');
const TYPE_PRIO = Object.fromEntries((cfg.typePriority || []).map((t, i) => [t, i + 1]));
const TAIL = new Set(cfg.tailTypes || []);
const HOLIDAYS = new Set(cfg.holidays || []);

// ── Parse type / module / submodule from titles ─────────────────────
for (const t of tasks) {
  const m = t.title.match(TYPE_RE);
  t.type = m ? m[1] : '???';
  const rest = t.title.replace(TYPE_RE, '');
  const parts = rest.split(' - ');
  t.module = parts[0].trim();
  t.submodule = parts.slice(1).join(' - ').trim() || null;
  t.cx = Number(t.complexity) || 0;
}

// ── Workday calendar ────────────────────────────────────────────────
const iso = d => `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, '0')}-${String(d.getDate()).padStart(2, '0')}`;
const isWorkday = d => d.getDay() !== 0 && d.getDay() !== 6 && !HOLIDAYS.has(iso(d));
const nextWorkday = d => { const x = new Date(d); do { x.setDate(x.getDate() + 1); } while (!isWorkday(x)); return x; };
const toWorkday = d => { const x = new Date(d); while (!isWorkday(x)) x.setDate(x.getDate() + 1); return x; };
const fmt = (d, time) => `${iso(d)}T${time}:00${TZ}`;
const WORK_START = cfg.workStart || '10:00';
const WORK_END = cfg.workEnd || '18:00';
const WORK_HOURS = Number(cfg.workHours || 8);
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

// ── Layout ──────────────────────────────────────────────────────────
const updates = []; // {id,title,start,end,resp,pred} — pred drives set-links.cjs
const used = new Set();
let errors = 0;

for (const { person, blocks } of cfg.plan) {
  const p = cfg.people[person];
  if (!p) { console.error(`[ERR] Unknown person "${person}" in plan`); errors++; continue; }
  let cursor = makeCursor(new Date(p.start + 'T00:00:00'));
  let prev = null;        // previous chain element {id, endIso}
  let personEnd = null;
  console.log(`\n=== ${person} (id=${p.id}), start ${iso(cursor.date)} ${WORK_START} ===`);

  const push = (t, start, end, hours) => {
    updates.push({ id: t.id, title: t.title, start, end, resp: p.id, pred: prev ? prev.id : null, hours });
    prev = { id: t.id, endIso: end };
  };
  const scheduleReal = t => {
    const hours = Math.max(1, Math.ceil(t.cx));
    const span = advanceHours(cursor, hours);
    cursor = normalizeCursor(cloneCursor(span.end));
    personEnd = span.end;
    push(t, cursorIso(span.start), cursorIso(span.end), hours);
  };
  const scheduleMarker = t => {
    const span = advanceHours(cursor, 1);
    cursor = normalizeCursor(cloneCursor(span.end));
    personEnd = span.end;
    push(t, cursorIso(span.start), cursorIso(span.end), 1);
  };

  const tail = [];
  for (const b of blocks) {
    const blockTasks = tasks.filter(t =>
      t.module === b.module && (b.submodule == null || t.submodule === b.submodule) && !used.has(t.id));
    blockTasks.sort((a, x) => (TYPE_PRIO[a.type] || 99) - (TYPE_PRIO[x.type] || 99) || Number(a.id) - Number(x.id));
    blockTasks.forEach(t => used.add(t.id));
    const hourSum = blockTasks.reduce((s, t) => s + t.cx, 0);
    if (b.hours != null && hourSum !== b.hours) { console.error(`[ERR] ${person} / ${b.module}${b.submodule ? ' / ' + b.submodule : ''}: cx sum=${hourSum}h, plan=${b.hours}h`); errors++; }
    const main = blockTasks.filter(t => !TAIL.has(t.type));
    tail.push(...blockTasks.filter(t => TAIL.has(t.type)));
    const blockStart = `${iso(cursor.date)} ${padTime(cursor.minute)}`;
    for (const t of main) (t.cx === 0 ? scheduleMarker : scheduleReal)(t);
    console.log(`  ${b.module}${b.submodule ? ' / ' + b.submodule : ''} (${main.reduce((s, t) => s + t.cx, 0)} h main): ${blockStart} -> ${personEnd ? `${iso(personEnd.date)} ${padTime(personEnd.minute)}` : blockStart} [${main.length} tasks]`);
  }
  if (tail.length) {
    const tailStart = `${iso(cursor.date)} ${padTime(cursor.minute)}`;
    for (const t of tail) (t.cx === 0 ? scheduleMarker : scheduleReal)(t);
    console.log(`  TAIL ${[...TAIL].join('/')} (${tail.reduce((s, t) => s + t.cx, 0)} h): ${tailStart} -> ${iso(personEnd.date)} ${padTime(personEnd.minute)} [${tail.length} tasks]`);
  }
  console.log(`  FINISH: ${personEnd ? `${iso(personEnd.date)} ${padTime(personEnd.minute)}` : '(no tasks)'}`);
}

const missed = tasks.filter(t => !used.has(t.id));
if (missed.length) { console.error(`[ERR] Tasks not covered by plan: ${missed.map(t => t.id).join(', ')}`); errors++; }

fs.writeFileSync(out, JSON.stringify(updates, null, 2), 'utf8');
console.log(`\nUpdates: ${updates.length} of ${tasks.length} tasks, validation errors: ${errors} -> ${out}`);
if (errors) process.exit(1);
