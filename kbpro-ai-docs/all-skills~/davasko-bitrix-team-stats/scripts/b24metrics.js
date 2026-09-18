#!/usr/bin/env node
'use strict';
// b24metrics.js — deterministic Bitrix24 metrics b1-b12 for ONE employee.
// Input: raw cache (b24fetch.js) + run config. Output: bitrix-summary.json
// (windows for trends + pooled phase aggregates + deltas). No LLM here.
//
// Conventions (user decisions 2026-07-16):
//   - dev task = tag "Dev" OR Dev token in the title (b24common.isDevTask);
//   - bugs: heuristic candidates (BUG_TITLE_RE) minus user-excluded ids plus
//     user-confirmed ids from mapping-config;
//   - closure metrics attribute to the window of CLOSED_DATE, creation
//     metrics to CREATED_DATE;
//   - phase values are pooled over phase tasks (not averages of window
//     medians) and normalized per month; windows overlapping declared
//     absences (>=10d or >=30%) are EXCLUDED_ABSENCE.

const path = require('path');
const fs = require('fs');
const c = require('./lib/b24common.js');

const HELP = `b24metrics.js — compute b1-b12 for one employee

Usage:
  node b24metrics.js --raw <raw.json> --config <run-config.json>
                     --employee <slug> --out <bitrix-summary.json>

The config supplies: author entry (slug -> display + b24_ids), phases,
absences per employee, optional bug rule overrides (mapping-config merged in).`;

function median(values) {
  const a = values.filter(Number.isFinite).slice().sort((x, y) => x - y);
  if (!a.length) return 0;
  const mid = Math.floor(a.length / 2);
  return a.length % 2 ? a[mid] : (a[mid - 1] + a[mid]) / 2;
}

function round(n, d = 3) {
  if (!Number.isFinite(n)) return 0;
  const k = 10 ** d;
  return Math.round(n * k) / k;
}

function daysDiff(fromIso, toIso) {
  return (Date.parse(toIso) - Date.parse(fromIso)) / 86400000;
}

function inRange(iso, from, to) {
  if (!iso) return false;
  const d = c.isoDay(iso);
  return d >= from && d <= to;
}

// ---------------------------------------------------------------------------
// Per-task derived facts (computed once, reused by windows and phases)
// ---------------------------------------------------------------------------

function taskFacts(task, history, ids, bugRule) {
  const h = history || { rows: [], first_row: null };
  const statusRows = h.rows.filter(r => r.field === 'STATUS');
  const deadlineShifts = h.rows.filter(r => r.field === 'DEADLINE').length;
  const reopenEvents = statusRows.filter(r => {
    const from = Number(r.from), to = Number(r.to);
    return (from === c.STATUS.SUPPOSEDLY_COMPLETED || from === c.STATUS.COMPLETED)
      && (to === c.STATUS.PENDING || to === c.STATUS.IN_PROGRESS);
  }).length;
  const firstInProgress = statusRows.find(r => Number(r.to) === c.STATUS.IN_PROGRESS);
  const firstActionByResp = h.rows.find(r => ids.has(r.userId) && r.field !== 'NEW');

  const isBug = bugRule.confirmed_ids.has(task.id)
    || (bugRule.heuristic && c.isBugCandidate(task) && !bugRule.excluded_ids.has(task.id));

  const closed = task.status === c.STATUS.COMPLETED && task.closedDate;
  return {
    isDev: c.isDevTask(task),
    isBug,
    closed,
    cycleDays: closed && task.createdDate ? daysDiff(task.createdDate, task.closedDate) : null,
    startToCloseDays: closed && firstInProgress ? daysDiff(firstInProgress.date, task.closedDate) : null,
    overdueDays: closed && task.deadline ? daysDiff(task.deadline, task.closedDate) : null,
    deadlineShifts,
    reopenEvents,
    estimateRatio: closed && task.timeEstimate > 0 && task.timeSpentInLogs > 0
      ? task.timeSpentInLogs / task.timeEstimate : null,
    selfAssigned: task.createdBy === task.responsibleId,
    timeToFirstActionDays: firstActionByResp && task.createdDate
      ? Math.max(0, daysDiff(task.createdDate, firstActionByResp.date)) : null,
  };
}

// ---------------------------------------------------------------------------
// Aggregation over a set of tasks bounded by [from, to]
// ---------------------------------------------------------------------------

function aggregate(tasks, facts, from, to, days) {
  const months = days / c.DAYS_PER_MONTH;
  const pm = v => months ? round(v / months) : 0;

  const devTasks = tasks.filter(t => facts.get(t.id).isDev);
  const closedDev = devTasks.filter(t => facts.get(t.id).closed && inRange(t.closedDate, from, to));
  const createdDev = devTasks.filter(t => inRange(t.createdDate, from, to));
  const closedBugs = closedDev.filter(t => facts.get(t.id).isBug);

  const f = id => facts.get(id);
  const withDeadline = closedDev.filter(t => t.deadline);
  const onTime = withDeadline.filter(t => f(t.id).overdueDays <= 0);
  const overdueDays = withDeadline.map(t => f(t.id).overdueDays).filter(v => v > 0);
  const estimateRatios = closedDev.map(t => f(t.id).estimateRatio).filter(v => v !== null);
  const complexityVals = closedDev.map(t => t.complexity).filter(v => v !== null && Number.isFinite(v));
  const reopened = closedDev.filter(t => f(t.id).reopenEvents > 0);
  const firstActions = createdDev.map(t => f(t.id).timeToFirstActionDays).filter(v => v !== null);

  // b7 WIP: mean over working days of open dev tasks (created <= day, not yet
  // closed). Status-interval precision is traded for cacheability — documented.
  let wipSum = 0, wipDays = 0;
  for (let d = from; d <= to; d = c.addDays(d, 1)) {
    if (!c.isWorkingDay(d)) continue;
    wipDays++;
    wipSum += devTasks.filter(t => {
      const created = c.isoDay(t.createdDate);
      const closedD = t.closedDate ? c.isoDay(t.closedDate) : null;
      return created <= d && (!closedD || closedD > d);
    }).length;
  }

  // b8 task mix over ALL tasks (not only dev) active in range.
  const mix = {};
  for (const t of tasks) {
    if (!(inRange(t.createdDate, from, to) || inRange(t.closedDate, from, to))) continue;
    const tags = t.tags.length ? t.tags : ['(no tag)'];
    for (const tag of tags) mix[tag] = (mix[tag] || 0) + 1;
  }

  // Per-project breakdown of closed dev tasks, with human names from the
  // shipped registry (references/b24-projects.json).
  const projects = {};
  for (const t of closedDev) {
    const name = c.groupName(PROJECT_REGISTRY, t.groupId);
    projects[name] = (projects[name] || 0) + 1;
  }

  return {
    b1: { closed: closedDev.length, closed_pm: pm(closedDev.length) },
    b2: {
      cycle_days_median: round(median(closedDev.map(t => f(t.id).cycleDays)), 1),
      start_to_close_days_median: round(median(closedDev.map(t => f(t.id).startToCloseDays).filter(v => v !== null)), 1),
    },
    b3: {
      with_deadline_share: closedDev.length ? round(withDeadline.length / closedDev.length) : 0,
      hit_rate: withDeadline.length ? round(onTime.length / withDeadline.length) : 0,
      overdue_days_median: round(median(overdueDays), 1),
    },
    b4: { deadline_shifts_avg: round(closedDev.length ? closedDev.reduce((s, t) => s + f(t.id).deadlineShifts, 0) / closedDev.length : 0, 2) },
    b5: {
      reopen_rate: closedDev.length ? round(reopened.length / closedDev.length) : 0,
      reopened: reopened.length,
      reopen_events: closedDev.reduce((s, t) => s + f(t.id).reopenEvents, 0),
    },
    b6: {
      estimate_ratio_median: round(median(estimateRatios), 2),
      sample: estimateRatios.length,
    },
    b7: { wip_avg: round(wipDays ? wipSum / wipDays : 0, 2) },
    b8: mix,
    b9: {
      complexity_avg: round(complexityVals.length ? complexityVals.reduce((s, v) => s + v, 0) / complexityVals.length : 0, 2),
      complexity_sum_pm: pm(complexityVals.reduce((s, v) => s + v, 0)),
      filled_share: closedDev.length ? round(complexityVals.length / closedDev.length) : 0,
    },
    b10: { self_assigned_share: createdDev.length ? round(createdDev.filter(t => f(t.id).selfAssigned).length / createdDev.length) : 0 },
    b11: {
      bugs_closed: closedBugs.length,
      bugs_closed_pm: pm(closedBugs.length),
      bug_mttr_days_median: round(median(closedBugs.map(t => f(t.id).cycleDays)), 1),
    },
    b12: { time_to_first_action_days_median: round(median(firstActions), 1), sample: firstActions.length },
    projects,
    counts: { dev_closed: closedDev.length, dev_created: createdDev.length, all_seen: tasks.length },
  };
}

const PROJECT_REGISTRY = c.loadProjectRegistry();

// Flat key list for deltas/series.
const SERIES = [
  ['b1.closed_pm', a => a.b1.closed_pm],
  ['b2.cycle_days_median', a => a.b2.cycle_days_median],
  ['b3.hit_rate', a => a.b3.hit_rate],
  ['b4.deadline_shifts_avg', a => a.b4.deadline_shifts_avg],
  ['b5.reopen_rate', a => a.b5.reopen_rate],
  ['b6.estimate_ratio_median', a => a.b6.estimate_ratio_median],
  ['b7.wip_avg', a => a.b7.wip_avg],
  ['b9.complexity_avg', a => a.b9.complexity_avg],
  ['b10.self_assigned_share', a => a.b10.self_assigned_share],
  ['b11.bugs_closed_pm', a => a.b11.bugs_closed_pm],
  ['b11.bug_mttr_days_median', a => a.b11.bug_mttr_days_median],
  ['b12.time_to_first_action_days_median', a => a.b12.time_to_first_action_days_median],
];

function overlapDays(aFrom, aTo, bFrom, bTo) {
  const from = aFrom > bFrom ? aFrom : bFrom;
  const to = aTo < bTo ? aTo : bTo;
  return from > to ? 0 : c.daysBetween(from, to);
}

function main() {
  const args = c.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  for (const req of ['raw', 'config', 'employee', 'out']) {
    if (!args[req]) { console.error(`missing --${req}\n\n${HELP}`); process.exit(1); }
  }
  const raw = c.readJson(path.resolve(String(args.raw)));
  const config = c.readJson(path.resolve(String(args.config)));
  const slug = String(args.employee);
  const outFile = path.resolve(String(args.out));
  c.assertNotDriveC(outFile, 'summary output');

  const emp = (config.employees || []).find(e => e.slug === slug);
  if (!emp) { console.error(`employee "${slug}" not found in config.employees`); process.exit(1); }
  const ids = new Set((emp.b24_ids || []).map(Number));
  if (!ids.size) { console.error(`employee "${slug}" has no b24_ids`); process.exit(1); }

  const bugRule = {
    heuristic: config.bug_rule?.heuristic !== false,
    confirmed_ids: new Set(config.bug_rule?.confirmed_ids || []),
    excluded_ids: new Set(config.bug_rule?.excluded_ids || []),
  };

  const myTasks = (raw.tasks || []).filter(t => ids.has(t.responsibleId));
  const facts = new Map();
  for (const t of myTasks) facts.set(t.id, taskFacts(t, raw.histories?.[t.id], ids, bugRule));

  const flags = new Set(raw.flags || []);
  const absences = (emp.absences || []);
  const phases = [];
  const windowSeries = [];

  for (const p of config.phases || []) {
    const windows = c.sliceWindows(p.id, p.from, p.to);
    const included = [];
    for (const w of windows) {
      let overlap = 0;
      for (const a of absences) overlap += overlapDays(w.from, w.to, String(a.from), String(a.to));
      const excluded = overlap >= 10 || (w.days > 0 && overlap / w.days >= 0.3);
      const agg = aggregate(myTasks, facts, w.from, w.to, w.days);
      windowSeries.push({
        window_id: w.window_id, phase: p.id, from: w.from, to: w.to,
        excluded_absence: excluded,
        values: Object.fromEntries(SERIES.map(([k, fn]) => [k, fn(agg)])),
      });
      if (excluded) flags.add(`EXCLUDED_ABSENCE:${w.window_id}`);
      else included.push(w);
    }
    // Pooled phase aggregate over the INCLUDED windows' union range days.
    const inclDays = included.reduce((s, w) => s + w.days, 0);
    const phaseAgg = included.length
      ? aggregateOverWindows(myTasks, facts, included, inclDays)
      : null;
    phases.push({
      id: p.id, name: p.name || p.id, from: p.from, to: p.to,
      months: round(inclDays / c.DAYS_PER_MONTH, 2),
      metrics: phaseAgg,
      windows: windows.map(w => w.window_id),
      excluded_windows: windows.filter(w => !included.includes(w)).map(w => w.window_id),
    });
  }

  // Deltas between consecutive phases.
  for (let i = 1; i < phases.length; i++) {
    const prev = phases[i - 1], cur = phases[i];
    if (!prev.metrics || !cur.metrics) continue;
    const deltas = {};
    for (const [key, fn] of SERIES) {
      deltas[key] = round(fn(cur.metrics) - fn(prev.metrics));
    }
    cur.metrics.b20 = { compared_to: prev.id, deltas_pm: deltas };
  }

  const anyComplexity = phases.some(p => p.metrics && p.metrics.b9.filled_share > 0);
  if (!anyComplexity) flags.add('PARTIAL_FIELD_COMPLEXITY');
  const anyEstimates = phases.some(p => p.metrics && p.metrics.b6.sample > 0);
  if (!anyEstimates) flags.add('PARTIAL_FIELD_ESTIMATE');
  if (bugRule.heuristic && !bugRule.confirmed_ids.size) flags.add('BUG_RULE_HEURISTIC_UNCONFIRMED');

  c.writeJsonPretty(outFile, {
    schema_version: c.SCHEMA_VERSION,
    author: { display: emp.display || slug, slug, b24_ids: [...ids] },
    innovation: config.innovation || null,
    generated_at: new Date().toISOString(),
    analyzed_range: raw.range,
    phases,
    window_series: windowSeries,
    flags: [...flags].sort(),
  });
  console.log(`wrote ${outFile} (phases=${phases.length}, windows=${windowSeries.length}, tasks=${myTasks.length})`);

  function aggregateOverWindows(tasks, factsMap, windows, totalDays) {
    // Pool tasks over the union of window ranges (windows are contiguous per
    // phase except absence holes — aggregate() is range-based, so run it per
    // window and merge counts; medians are recomputed over pooled task sets
    // via a single wide pass with a membership filter).
    const inAnyWindow = iso => windows.some(w => inRange(iso, w.from, w.to));
    const pooled = tasks.filter(t => inAnyWindow(t.createdDate) || inAnyWindow(t.closedDate));
    // Range-based aggregate over the full span but restricted task set: use
    // min/max of included windows; WIP uses only included working days.
    const from = windows[0].from;
    const to = windows[windows.length - 1].to;
    return aggregate(pooled, factsMap, from, to, totalDays);
  }
}

try { main(); } catch (e) { console.error(`b24metrics.js: ${e.stack || e.message}`); process.exit(1); }
