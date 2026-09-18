#!/usr/bin/env node
'use strict';
// b24fetch.js — deterministic Bitrix24 raw-data fetch for the team-stats skill.
// Tasks + per-task history (batched x50) + users -> one raw JSON cache file.
// Webhook from env KBPRO_BITRIX24_WEBHOOK_BASE only; never logged.
//
// Cycle-time correctness: tasks CLOSED inside the range may have been created
// long before it, so we fetch (created in [from-180d .. to]) UNION (closed in
// [from .. to]) and merge by id.

const path = require('path');
const fs = require('fs');
const c = require('./lib/b24common.js');

const HELP = `b24fetch.js — fetch Bitrix24 tasks/history/users into a raw cache

Usage:
  node b24fetch.js --from YYYY-MM-DD --to YYYY-MM-DD --out <raw.json>
                   [--responsible <id1,id2>] [--force]

Options:
  --from/--to        Analysis range (usually min(phase.from)..max(phase.to)).
  --responsible      Bitrix user ids; omit to fetch ALL (needed for project
                     inference and group task-mix context).
  --out              Output file (non-C path). Reused as cache unless --force.
  --history-fields   Comma list kept from history (default: NEW,STATUS,DEADLINE).
  --force            Ignore existing cache file.
  --help             Print this and exit 0.`;

const SELECT = ['ID', 'TITLE', 'GROUP_ID', 'RESPONSIBLE_ID', 'CREATED_BY',
  'CREATED_DATE', 'CLOSED_DATE', 'DEADLINE', 'STATUS',
  'TIME_ESTIMATE', 'TIME_SPENT_IN_LOGS', 'TAGS', 'UF_TASKS_TASK_1783529349965'];

async function fetchTasks(filter) {
  return c.b24All('tasks.task.list', { filter, select: SELECT },
    body => body.result?.tasks || []);
}

function compactTask(t) {
  return {
    id: Number(t.id),
    title: t.title || '',
    groupId: Number(t.groupId) || 0,
    responsibleId: Number(t.responsibleId) || 0,
    createdBy: Number(t.createdBy) || 0,
    createdDate: t.createdDate || null,
    closedDate: t.closedDate || null,
    deadline: t.deadline || null,
    status: Number(t.status) || 0,
    timeEstimate: Number(t.timeEstimate) || 0,
    timeSpentInLogs: Number(t.timeSpentInLogs) || 0,
    tags: c.taskTags(t),
    complexity: t[c.COMPLEXITY_UF] != null && t[c.COMPLEXITY_UF] !== ''
      ? Number(t[c.COMPLEXITY_UF]) || null : null,
  };
}

async function fetchHistories(taskIds, keepFields, flags) {
  const histories = {};
  const errors = [];
  for (let i = 0; i < taskIds.length; i += c.BATCH_SIZE) {
    const slice = taskIds.slice(i, i + c.BATCH_SIZE);
    const cmds = {};
    for (const id of slice) cmds[`h${id}`] = `tasks.task.history.list?${c.qs({ taskId: id })}`;
    const res = await c.b24Batch(cmds, errors);
    for (const id of slice) {
      const rows = res[`h${id}`]?.list || [];
      const compact = [];
      let firstByResponsible = null;
      for (const r of rows) {
        const row = {
          date: r.createdDate || '',
          field: r.field || '',
          from: r.value?.from ?? null,
          to: r.value?.to ?? null,
          userId: Number(r.user?.id) || 0,
        };
        if (keepFields.has(row.field)) compact.push(row);
        if (!firstByResponsible) firstByResponsible = row; // rows come oldest-first
      }
      histories[id] = { rows: compact, first_row: firstByResponsible };
      if (res[`h${id}`]?.list && rows.length >= 50) flags.add(`HISTORY_PAGE_CAP:${id}`);
    }
    console.log(`  history: ${Math.min(i + c.BATCH_SIZE, taskIds.length)}/${taskIds.length}`);
  }
  if (errors.length) {
    flags.add(`HISTORY_ERRORS:${errors.length}`);
    console.warn(`WARN: ${errors.length} history sub-call errors (first: ${errors[0]})`);
  }
  return histories;
}

async function main() {
  const args = c.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  for (const req of ['from', 'to', 'out']) {
    if (!args[req]) { console.error(`missing --${req}\n\n${HELP}`); process.exit(1); }
  }
  const outFile = path.resolve(String(args.out));
  c.assertNotDriveC(outFile, 'raw cache');
  if (fs.existsSync(outFile) && !args.force) {
    console.log(`cache exists, skipping fetch: ${outFile} (use --force to refetch)`);
    process.exit(0);
  }
  c.webhookBase(); // fail fast if env missing

  const from = String(args.from);
  const to = String(args.to);
  const responsible = args.responsible
    ? String(args.responsible).split(',').map(s => Number(s.trim())).filter(Boolean)
    : null;
  const keepFields = new Set(String(args['history-fields'] || 'NEW,STATUS,DEADLINE').split(','));
  const flags = new Set();

  const baseFilter = responsible ? { RESPONSIBLE_ID: responsible } : {};
  console.log('fetching tasks (created window)...');
  const createdFrom = c.addDays(from, -180);
  const byCreated = await fetchTasks({
    ...baseFilter, '>=CREATED_DATE': createdFrom, '<=CREATED_DATE': `${to}T23:59:59`,
  });
  console.log(`  ${byCreated.length} tasks by created date`);
  console.log('fetching tasks (closed window)...');
  const byClosed = await fetchTasks({
    ...baseFilter, '>=CLOSED_DATE': from, '<=CLOSED_DATE': `${to}T23:59:59`,
  });
  console.log(`  ${byClosed.length} tasks by closed date`);

  const byId = new Map();
  for (const t of [...byCreated, ...byClosed]) byId.set(Number(t.id), compactTask(t));
  const tasks = [...byId.values()].sort((a, b) => a.id - b.id);
  console.log(`merged unique tasks: ${tasks.length}`);

  console.log('fetching users...');
  const users = (await c.b24All('user.get', { FILTER: { ACTIVE: true } }, b => b.result || []))
    .map(u => ({
      id: Number(u.ID),
      name: `${u.NAME || ''} ${u.LAST_NAME || ''}`.trim(),
      position: u.WORK_POSITION || '',
    }));
  console.log(`  ${users.length} active users`);

  console.log('fetching task histories (batched)...');
  const histories = await fetchHistories(tasks.map(t => t.id), keepFields, flags);

  c.writeJson(outFile, {
    schema_version: c.SCHEMA_VERSION,
    range: { from, to },
    responsible: responsible || 'all',
    fetched_at: new Date().toISOString(),
    tasks,
    histories,
    users,
    flags: [...flags].sort(),
  });
  console.log(`wrote ${outFile} (tasks=${tasks.length}, users=${users.length}, flags=${flags.size})`);
}

main().catch(e => { console.error(`b24fetch.js: ${e.stack || e.message}`); process.exit(1); });
