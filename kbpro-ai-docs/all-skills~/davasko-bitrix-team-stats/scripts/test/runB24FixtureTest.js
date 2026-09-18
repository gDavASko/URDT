#!/usr/bin/env node
'use strict';
// runB24FixtureTest.js — machine judge for b24metrics.js. Builds a mocked raw
// cache with KNOWN facts (no network), runs the real metrics script, asserts
// exact numbers: dev filter, reopen detection, deadline discipline, bug MTTR,
// self-assigned share, estimate accuracy, complexity partiality.
//
// Usage: node scripts/test/runB24FixtureTest.js   (exit 0 = all pass)

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const SCRIPTS = path.resolve(__dirname, '..');
const DEV1 = 316, LEAD = 400, OTHER = 555;

function st(from, to, date, userId = DEV1) {
  return { date, field: 'STATUS', from: String(from), to: String(to), userId };
}
function dl(date, userId = DEV1) {
  return { date, field: 'DEADLINE', from: '1', to: '2', userId };
}

function buildRaw() {
  const tasks = [
    { id: 1, title: 'Build inventory system', groupId: 80, responsibleId: DEV1, createdBy: LEAD,
      createdDate: '2025-01-02T09:00:00+05:00', closedDate: '2025-01-10T09:00:00+05:00',
      deadline: '2025-01-12T09:00:00+05:00', status: 5,
      timeEstimate: 36000, timeSpentInLogs: 28800, tags: ['Dev'], complexity: null },
    { id: 2, title: 'Fix crash in loader Dev', groupId: 80, responsibleId: DEV1, createdBy: DEV1,
      createdDate: '2025-01-05T09:00:00+05:00', closedDate: '2025-01-20T09:00:00+05:00',
      deadline: '2025-01-15T09:00:00+05:00', status: 5,
      timeEstimate: 0, timeSpentInLogs: 3600, tags: [], complexity: null },
    { id: 3, title: 'Cow walk animation', groupId: 86, responsibleId: DEV1, createdBy: LEAD,
      createdDate: '2025-01-03T09:00:00+05:00', closedDate: '2025-01-15T09:00:00+05:00',
      deadline: null, status: 5, timeEstimate: 0, timeSpentInLogs: 0, tags: ['ANIM'], complexity: null },
    { id: 4, title: 'Refactor save service', groupId: 80, responsibleId: DEV1, createdBy: DEV1,
      createdDate: '2025-02-03T09:00:00+05:00', closedDate: '2025-02-10T09:00:00+05:00',
      deadline: null, status: 5, timeEstimate: 0, timeSpentInLogs: 0, tags: ['Dev'], complexity: 5 },
    { id: 5, title: 'Dev spike: addressables audit', groupId: 80, responsibleId: DEV1, createdBy: LEAD,
      createdDate: '2025-02-05T09:00:00+05:00', closedDate: null,
      deadline: null, status: 3, timeEstimate: 0, timeSpentInLogs: 0, tags: ['Dev'], complexity: null },
    { id: 6, title: 'Fix bug in other guy task Dev', groupId: 80, responsibleId: OTHER, createdBy: LEAD,
      createdDate: '2025-01-06T09:00:00+05:00', closedDate: '2025-01-09T09:00:00+05:00',
      deadline: null, status: 5, timeEstimate: 0, timeSpentInLogs: 0, tags: ['Dev'], complexity: null },
  ];
  const histories = {
    1: { rows: [st(2, 3, '2025-01-03T09:00:00+05:00'), st(3, 4, '2025-01-09T09:00:00+05:00'),
                st(4, 5, '2025-01-10T09:00:00+05:00')], first_row: null },
    2: { rows: [st(2, 3, '2025-01-06T09:00:00+05:00'), dl('2025-01-07T09:00:00+05:00', LEAD),
                dl('2025-01-08T09:00:00+05:00', LEAD), st(3, 4, '2025-01-10T09:00:00+05:00'),
                st(4, 2, '2025-01-12T09:00:00+05:00', LEAD), st(2, 3, '2025-01-13T09:00:00+05:00'),
                st(3, 5, '2025-01-20T09:00:00+05:00')], first_row: null },
    3: { rows: [st(2, 5, '2025-01-15T09:00:00+05:00')], first_row: null },
    4: { rows: [st(2, 3, '2025-02-04T09:00:00+05:00'), st(3, 5, '2025-02-10T09:00:00+05:00')], first_row: null },
    5: { rows: [st(2, 3, '2025-02-06T09:00:00+05:00')], first_row: null },
    6: { rows: [], first_row: null },
  };
  return {
    schema_version: 'b24-1.0',
    range: { from: '2025-01-01', to: '2025-02-28' },
    responsible: 'all', fetched_at: 'test',
    tasks, histories,
    users: [{ id: DEV1, name: 'Dev One', position: 'Programmer' }],
    flags: [],
  };
}

function buildConfig() {
  return {
    employees: [{ slug: 'dev1', display: 'Dev One', b24_ids: [DEV1], absences: [] }],
    phases: [
      { id: 'P1', from: '2025-01-01', to: '2025-01-31' },
      { id: 'P2', from: '2025-02-01', to: '2025-02-28' },
    ],
    innovation: { name: 'test innovation' },
    bug_rule: { heuristic: true, confirmed_ids: [], excluded_ids: [] },
  };
}

function main() {
  // The drive-C guard in b24common applies to the metrics script too, so the
  // test work dir must live off C: — use the skill's own drive.
  const driveRoot = path.parse(SCRIPTS).root;
  const work = path.join(driveRoot, 'perf-audit', '_tmp', `b24-fixture-${Date.now()}`);
  fs.mkdirSync(work, { recursive: true });
  const rawFile = path.join(work, 'raw.json');
  const cfgFile = path.join(work, 'config.json');
  const outFile = path.join(work, 'summary.json');
  fs.writeFileSync(rawFile, JSON.stringify(buildRaw()));
  fs.writeFileSync(cfgFile, JSON.stringify(buildConfig()));

  const res = spawnSync(process.execPath, [path.join(SCRIPTS, 'b24metrics.js'),
    '--raw', rawFile, '--config', cfgFile, '--employee', 'dev1', '--out', outFile],
    { encoding: 'utf8', windowsHide: true, env: { ...process.env, PERF_AUDIT_REPORTS: work } });
  if (res.status !== 0) {
    console.error(`b24metrics failed:\n${res.stderr || res.stdout}`);
    process.exit(1);
  }

  const s = JSON.parse(fs.readFileSync(outFile, 'utf8'));
  const p1 = s.phases.find(p => p.id === 'P1').metrics;
  const p2 = s.phases.find(p => p.id === 'P2').metrics;

  let failures = 0;
  const eq = (label, actual, expected) => {
    const ok = actual === expected;
    console.log(`[${ok ? 'PASS' : 'FAIL'}] ${label}: expected ${expected}, got ${actual}`);
    if (!ok) failures++;
  };

  eq('P1 b1.closed (dev filter: ANIM task and other guy excluded)', p1.b1.closed, 2);
  eq('P1 b2.cycle_days_median (8 & 15 -> 11.5)', p1.b2.cycle_days_median, 11.5);
  eq('P1 b3.hit_rate (1 of 2 with deadline on time)', p1.b3.hit_rate, 0.5);
  eq('P1 b3.overdue_days_median (task 2: +5d)', p1.b3.overdue_days_median, 5);
  eq('P1 b4.deadline_shifts_avg ((0+2)/2)', p1.b4.deadline_shifts_avg, 1);
  eq('P1 b5.reopen_rate (task 2 bounced 4->2)', p1.b5.reopen_rate, 0.5);
  eq('P1 b5.reopen_events', p1.b5.reopen_events, 1);
  eq('P1 b6.estimate_ratio_median (28800/36000)', p1.b6.estimate_ratio_median, 0.8);
  eq('P1 b10.self_assigned_share (task2 self of 2 created)', p1.b10.self_assigned_share, 0.5);
  eq('P1 b11.bugs_closed (heuristic: task 2)', p1.b11.bugs_closed, 1);
  eq('P1 b11.bug_mttr_days_median', p1.b11.bug_mttr_days_median, 15);
  eq('P1 b12.time_to_first_action_days_median (1d both)', p1.b12.time_to_first_action_days_median, 1);
  eq('P2 b1.closed', p2.b1.closed, 1);
  eq('P2 b9.complexity_avg (task 4 = 5)', p2.b9.complexity_avg, 5);
  eq('P2 b9.filled_share', p2.b9.filled_share, 1);
  eq('P2 b10.self_assigned_share (task4 self, task5 not)', p2.b10.self_assigned_share, 0.5);
  eq('P2 has b20 deltas vs P1', typeof p2.b20?.deltas_pm?.['b1.closed_pm'], 'number');
  eq('flag BUG_RULE_HEURISTIC_UNCONFIRMED present', s.flags.includes('BUG_RULE_HEURISTIC_UNCONFIRMED'), true);

  if (failures === 0) { try { fs.rmSync(work, { recursive: true, force: true }); } catch (_) {} }
  else console.log(`work dir kept: ${work}`);
  console.log(failures === 0 ? 'B24 FIXTURE TEST: ALL PASS' : `B24 FIXTURE TEST: ${failures} FAILURE(S)`);
  process.exit(failures === 0 ? 0 : 1);
}

try { main(); } catch (e) { console.error(`runB24FixtureTest.js: ${e.stack || e.message}`); process.exit(1); }
