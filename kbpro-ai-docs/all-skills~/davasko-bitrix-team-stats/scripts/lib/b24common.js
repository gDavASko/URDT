'use strict';
// Shared library for davasko-bitrix-team-stats. Self-contained (no imports
// from other skills). Node stdlib only, Node >= 18.
//
// Hard rules inherited from the perf-audit v2 doctrine:
//   - webhook URL from env only, never logged/persisted;
//   - NOTHING heavy on drive C (cache/reports on user-chosen paths);
//   - per-month normalization (30.4375 days), monthly windows with the
//     remainder merged into the last month;
//   - rhythm/time math over RU working days approximation.

const fs = require('fs');
const path = require('path');

const SCHEMA_VERSION = 'b24-1.0';
const DAYS_PER_MONTH = 30.4375;
const THROTTLE_MS = 600;          // Bitrix ~2 rps
const BATCH_SIZE = 50;            // batch method hard limit

// Bitrix task STATUS codes (documented semantics).
const STATUS = {
  NEW: 1, PENDING: 2, IN_PROGRESS: 3, SUPPOSEDLY_COMPLETED: 4,
  COMPLETED: 5, DEFERRED: 6, DECLINED: 7,
};

// Dev-task convention (user decision 2026-07-16): tag "Dev" OR the word Dev
// as a prefix/postfix-ish token in the title.
const DEV_TITLE_RE = /(^|[\s\[\](){}._-])dev([\s\[\](){}._-]|$)/i;

// Bug detection. The team DOES have a live convention: tag "Баг"/"Bug"/"BUG"
// (observed in groups 68/46/60, screenshots + probe 2026-07-16) — that is the
// primary signal. Title words are the fallback heuristic; candidates are
// confirmed by the user on first run, then cached in bug_rule.
const BUG_TAG_RE = /^(баг|bug)$/i;
const BUG_TITLE_RE = /(bug|hotfix|crash|баг|фикс|хотфикс|краш|ошибк)/i;

const COMPLEXITY_UF = 'ufTasksTask1783529349965'; // camelCase as returned by tasks.task.list

// ---------------------------------------------------------------------------
// Paths (no drive C)
// ---------------------------------------------------------------------------

function isOnDriveC(p) {
  return /^[cC]:[\\/]/.test(path.resolve(String(p)));
}

function assertNotDriveC(p, label) {
  if (isOnDriveC(p)) {
    throw new Error(`${label} path "${p}" is on drive C: — forbidden. Use e.g. E:\\perf-audit\\...`);
  }
}

function reportsRoot() {
  const env = process.env.PERF_AUDIT_REPORTS;
  if (!env) throw new Error('PERF_AUDIT_REPORTS is not set (non-C dir, e.g. E:\\perf-audit\\reports)');
  assertNotDriveC(env, 'PERF_AUDIT_REPORTS');
  return path.join(env, 'bitrix');
}

function ensureDir(p) {
  fs.mkdirSync(p, { recursive: true });
  return p;
}

// ---------------------------------------------------------------------------
// CLI args / JSON IO (same conventions as perf-audit)
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const out = { _: [] };
  const args = argv.slice(2);
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const next = args[i + 1];
      if (next !== undefined && !next.startsWith('--')) { out[key] = next; i++; }
      else out[key] = true;
    } else out._.push(a);
  }
  return out;
}

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^﻿/, ''));
}

function writeJson(file, data) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(data));
}

function writeJsonPretty(file, data) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(data, null, 1));
}

// ---------------------------------------------------------------------------
// Bitrix REST via webhook (env only; URL never logged)
// ---------------------------------------------------------------------------

function webhookBase() {
  const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/+$/, '');
  if (!base) throw new Error('KBPRO_BITRIX24_WEBHOOK_BASE is not set');
  return base;
}

const sleep = ms => new Promise(r => setTimeout(r, ms));
let _lastCallAt = 0;

async function throttled() {
  const wait = _lastCallAt + THROTTLE_MS - Date.now();
  if (wait > 0) await sleep(wait);
  _lastCallAt = Date.now();
}

async function b24(method, params = {}) {
  await throttled();
  const res = await fetch(`${webhookBase()}/${method}.json`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(params),
  });
  const body = await res.json().catch(() => null);
  if (!body) throw new Error(`${method}: HTTP ${res.status}, non-JSON body`);
  if (body.error) throw new Error(`${method}: ${body.error} — ${String(body.error_description || '').slice(0, 200)}`);
  return body;
}

// Paginated list call. extract(body) -> array of items for this page.
async function b24All(method, params, extract, { maxPages = 200 } = {}) {
  const items = [];
  let start = 0;
  for (let page = 0; page < maxPages; page++) {
    const body = await b24(method, { ...params, start });
    items.push(...(extract(body) || []));
    if (body.next === undefined || body.next === null) break;
    start = body.next;
  }
  return items;
}

// batch method: up to 50 sub-calls per request. cmds: {key: "method?query"}.
// Returns { key: resultOrNull }; sub-call errors are collected, not thrown.
async function b24Batch(cmds, errors) {
  const keys = Object.keys(cmds);
  const out = {};
  for (let i = 0; i < keys.length; i += BATCH_SIZE) {
    const slice = keys.slice(i, i + BATCH_SIZE);
    const cmd = {};
    for (const k of slice) cmd[k] = cmds[k];
    const body = await b24('batch', { halt: 0, cmd });
    const results = body.result?.result || {};
    const errs = body.result?.result_error || {};
    for (const k of slice) {
      out[k] = results[k] ?? null;
      if (errs[k] && Array.isArray(errors)) errors.push(`${k}: ${errs[k].error || JSON.stringify(errs[k]).slice(0, 120)}`);
    }
  }
  return out;
}

function qs(params) {
  // Bitrix batch sub-calls take PHP-style query strings.
  const parts = [];
  const walk = (prefix, v) => {
    if (v === null || v === undefined) return;
    if (typeof v === 'object') {
      for (const [k, vv] of Object.entries(v)) walk(`${prefix}[${encodeURIComponent(k)}]`, vv);
    } else {
      parts.push(`${prefix}=${encodeURIComponent(String(v))}`);
    }
  };
  for (const [k, v] of Object.entries(params)) {
    if (typeof v === 'object' && v !== null) walk(encodeURIComponent(k), v);
    else if (v !== undefined) parts.push(`${encodeURIComponent(k)}=${encodeURIComponent(String(v))}`);
  }
  return parts.join('&');
}

// ---------------------------------------------------------------------------
// Dates, windows, working days (RU approximation, same as perf-audit v2)
// ---------------------------------------------------------------------------

function addDays(isoDate, n) {
  const dt = new Date(`${isoDate}T00:00:00Z`);
  dt.setUTCDate(dt.getUTCDate() + n);
  return dt.toISOString().slice(0, 10);
}

function addMonths(isoDate, n) {
  const [y, m, d] = isoDate.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1 + n, d));
  if (dt.getUTCMonth() !== ((m - 1 + n) % 12 + 12) % 12) dt.setUTCDate(0);
  return dt.toISOString().slice(0, 10);
}

function daysBetween(fromIso, toIso) {
  return Math.round((Date.parse(`${toIso}T00:00:00Z`) - Date.parse(`${fromIso}T00:00:00Z`)) / 86400000) + 1;
}

function sliceWindows(phaseId, fromIso, toIso) {
  const windows = [];
  let cursor = fromIso;
  while (cursor <= toIso) {
    const nextStart = addMonths(cursor, 1);
    const end = addDays(nextStart, -1);
    if (end >= toIso) {
      windows.push({ from: cursor, to: toIso });
      cursor = addDays(toIso, 1);
    } else {
      const remainderFullMonthEnd = addDays(addMonths(nextStart, 1), -1);
      if (remainderFullMonthEnd > toIso) {
        windows.push({ from: cursor, to: toIso });
        cursor = addDays(toIso, 1);
      } else {
        windows.push({ from: cursor, to: end });
        cursor = nextStart;
      }
    }
  }
  return windows.map((w, i) => ({
    window_id: `${phaseId}_W${i + 1}_${w.from}`,
    phase: phaseId,
    from: w.from,
    to: w.to,
    days: daysBetween(w.from, w.to),
  }));
}

function perMonth(value, days) {
  if (!days) return 0;
  return value * (DAYS_PER_MONTH / days);
}

const RU_FIXED_HOLIDAYS_MMDD = new Set([
  '01-01', '01-02', '01-03', '01-04', '01-05', '01-06', '01-07', '01-08',
  '02-23', '03-08', '05-01', '05-09', '06-12', '11-04',
]);

function isWorkingDay(isoDate) {
  const dt = new Date(`${isoDate}T00:00:00Z`);
  const dow = dt.getUTCDay();
  if (dow === 0 || dow === 6) return false;
  return !RU_FIXED_HOLIDAYS_MMDD.has(isoDate.slice(5, 10));
}

function workingDaysBetween(fromIso, toIso) {
  let n = 0;
  for (let d = fromIso; d <= toIso; d = addDays(d, 1)) if (isWorkingDay(d)) n++;
  return n;
}

function isoDay(iso) { return String(iso || '').slice(0, 10); }

// ---------------------------------------------------------------------------
// Task classification (user conventions)
// ---------------------------------------------------------------------------

function taskTags(task) {
  const t = task.tags;
  if (!t) return [];
  if (Array.isArray(t)) return t.map(x => (typeof x === 'string' ? x : x.title)).filter(Boolean);
  return Object.values(t).map(x => x.title).filter(Boolean);
}

function isDevTask(task) {
  if (taskTags(task).some(tag => String(tag).toLowerCase() === 'dev')) return true;
  return DEV_TITLE_RE.test(String(task.title || ''));
}

function isBugCandidate(task) {
  if (taskTags(task).some(tag => BUG_TAG_RE.test(String(tag)))) return true;
  return BUG_TITLE_RE.test(String(task.title || ''));
}

// Known project (workgroup) registry shipped with the skill:
// references/b24-projects.json — groupId -> confirmed/proposed name.
function loadProjectRegistry() {
  const file = path.resolve(__dirname, '..', '..', 'references', 'b24-projects.json');
  try {
    const data = JSON.parse(fs.readFileSync(file, 'utf8'));
    const map = new Map();
    for (const g of data.groups || []) map.set(Number(g.groupId), g);
    return map;
  } catch (_) {
    return new Map();
  }
}

function groupName(registry, groupId) {
  const g = registry.get(Number(groupId));
  return g ? g.name : `group #${groupId}`;
}

// ---------------------------------------------------------------------------

module.exports = {
  SCHEMA_VERSION, DAYS_PER_MONTH, THROTTLE_MS, BATCH_SIZE, STATUS,
  DEV_TITLE_RE, BUG_TAG_RE, BUG_TITLE_RE, COMPLEXITY_UF,
  loadProjectRegistry, groupName,
  isOnDriveC, assertNotDriveC, reportsRoot, ensureDir,
  parseArgs, readJson, writeJson, writeJsonPretty,
  webhookBase, b24, b24All, b24Batch, qs, sleep,
  addDays, addMonths, daysBetween, sliceWindows, perMonth,
  isWorkingDay, workingDaysBetween, isoDay,
  taskTags, isDevTask, isBugCandidate,
};
