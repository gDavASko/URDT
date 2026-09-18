'use strict';
// Shared contract module for davasko-dev-performance-audit scripts.
// Node stdlib only (Node >= 18). No external dependencies.

const fs = require('fs');
const path = require('path');
const os = require('os');
const { spawnSync } = require('child_process');

// ---------------------------------------------------------------------------
// Constants (source of truth: PLAN-dev-performance-audit-skill.md §2.6, §3, §6)
// ---------------------------------------------------------------------------

const SCHEMA_VERSION = '2.0';
const RUBRICS_VERSION = '2.0';

// File scope: analyzed kinds. Everything else -> "other" bucket (counted, no LOC).
const CS_EXTS = ['.cs'];
const ASSET_EXTS = ['.asset', '.prefab'];
const SHADER_EXTS = ['.shader', '.hlsl', '.cginc', '.compute', '.shadergraph'];
// Non-Unity source code: contributes counters + volume, no Unity LLM rubrics (plan v2 §3.6).
const CODE_EXTS = ['.ts', '.tsx', '.js', '.jsx', '.mjs', '.cjs', '.py', '.sh', '.ps1',
  '.go', '.rs', '.cpp', '.h', '.hpp', '.c', '.java', '.kt', '.sql'];

// Bulk threshold: non-code files with more changed lines -> "bulk" bucket.
const BULK_LINES = 5000;

// LOW_SAMPLE gate: adaptive (plan v2 §3.2K). A window gets LLM scores when it has
// >= LOW_SAMPLE_MIN_CS_COMMITS cs-commits OR >= LOW_SAMPLE_MIN_ASSET_EFF_LOC
// effective asset LOC (content-developer profile).
const LOW_SAMPLE_MIN_CS_COMMITS = 10;
const LOW_SAMPLE_MIN_ASSET_EFF_LOC = 30;

// Rework horizon, days (plan v2 §3.2A: lines rewritten within this window count).
const REWORK_HORIZON_DAYS = 21;

// Average month length used for per-month normalization (§8а).
const DAYS_PER_MONTH = 30.4375;

// Parallel analyzer waves only above this window count (§4).
const SEQUENTIAL_WINDOW_LIMIT = 6;

function classifyFile(filePath) {
  const ext = path.extname(filePath).toLowerCase();
  if (CS_EXTS.includes(ext)) return 'cs';
  if (ext === '.prefab') return 'prefab';
  if (ext === '.asset') return 'asset';
  if (SHADER_EXTS.includes(ext)) return 'shader';
  if (CODE_EXTS.includes(ext)) return 'code';
  return 'other';
}

function isAnalyzedKind(kind) {
  return kind === 'cs' || kind === 'prefab' || kind === 'asset' || kind === 'shader'
    || kind === 'code';
}

// ---------------------------------------------------------------------------
// Paths
// ---------------------------------------------------------------------------

// scripts/lib/common.js -> skill root is two levels up from lib/.
const SKILL_ROOT = path.resolve(__dirname, '..', '..');
// all-skills~/davasko-dev-performance-audit -> kbpro-ai-docs is two levels up.
const AI_DOCS_ROOT = path.resolve(SKILL_ROOT, '..', '..');

// Plan v2 §3.5: reports live OUTSIDE any git repo, on a user-chosen path (never
// drive C). PERF_AUDIT_REPORTS holds that path for the current run; scripts
// also accept explicit --run/--out. The self-check report lives next to the cache.
function reportsRoot() {
  const env = process.env.PERF_AUDIT_REPORTS;
  if (!env) {
    throw new Error(
      'PERF_AUDIT_REPORTS is not set. Ask the user where to store reports '
      + '(NOT on drive C:, NOT inside a git repo), e.g. E:\\perf-audit\\reports');
  }
  assertNotDriveC(env, 'PERF_AUDIT_REPORTS');
  return env;
}

function selfCheckReportPath() {
  return path.join(cacheRoot(), 'selfcheck-report.json');
}

// Hard user requirement (plan v2 §3.5): NOTHING heavy on drive C — no cache,
// no reports, no temp dumps. There is deliberately NO silent default under
// LOCALAPPDATA/os.tmpdir() anymore.
function isOnDriveC(p) {
  return /^[cC]:[\\/]/.test(path.resolve(String(p)));
}

function assertNotDriveC(p, label) {
  if (isOnDriveC(p)) {
    throw new Error(
      `${label} path "${p}" is on drive C: — forbidden (drive C is overloaded). `
      + 'Pick a path on another drive, e.g. E:\\perf-audit\\...');
  }
}

function cacheRoot() {
  const env = process.env.PERF_AUDIT_CACHE;
  if (!env) {
    throw new Error(
      'PERF_AUDIT_CACHE is not set and no --cache was given. '
      + 'Set it to a directory NOT on drive C:, e.g. E:\\perf-audit\\cache');
  }
  assertNotDriveC(env, 'PERF_AUDIT_CACHE');
  return env;
}

function runDir(authorSlug, runDate) {
  return path.join(reportsRoot(), authorSlug, runDate);
}

function ensureDir(p) {
  fs.mkdirSync(p, { recursive: true });
  return p;
}

// ---------------------------------------------------------------------------
// CLI args: --key value | --flag  ->  { key: value, flag: true, _: [...] }
// ---------------------------------------------------------------------------

function parseArgs(argv) {
  const out = { _: [] };
  const args = argv.slice(2);
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a.startsWith('--')) {
      const key = a.slice(2);
      const next = args[i + 1];
      if (next !== undefined && !next.startsWith('--')) {
        out[key] = next;
        i++;
      } else {
        out[key] = true;
      }
    } else {
      out._.push(a);
    }
  }
  return out;
}

// ---------------------------------------------------------------------------
// Process / git helpers
// ---------------------------------------------------------------------------

const MAX_BUF = 512 * 1024 * 1024;

function run(cmd, args, opts = {}) {
  const res = spawnSync(cmd, args, {
    encoding: 'utf8',
    maxBuffer: MAX_BUF,
    windowsHide: true,
    ...opts,
  });
  if (res.error) throw res.error;
  return res; // { status, stdout, stderr }
}

function git(repoPath, args, opts = {}) {
  return run('git', ['-C', repoPath, ...args], opts);
}

function gitOk(repoPath, args) {
  const res = git(repoPath, args);
  if (res.status !== 0) {
    throw new Error(`git ${args.join(' ')} failed in ${repoPath}: ${res.stderr}`);
  }
  return res.stdout;
}

// ---------------------------------------------------------------------------
// Partial-clone blob prefetch (2026-07-17 fix). On blobless clones any
// content-diffing command (log --numstat, show, blame) lazily fetches missing
// blobs ONE round-trip per occurrence — thousands of round trips hang the
// pipeline for hours. Instead we list exactly the blob ids a pass will need,
// then fetch the missing ones from the promisor remote in a few big batches.
// ---------------------------------------------------------------------------

// Analyzed-extension pathspecs for content passes (numstat/show/patch-id).
const ANALYZED_PATHSPECS = [...CS_EXTS, ...ASSET_EXTS, ...SHADER_EXTS, ...CODE_EXTS]
  .map(ext => `*${ext}`);

// Which of `ids` are NOT present locally (git cat-file --batch-check).
function listMissingObjects(repoPath, ids) {
  const missing = [];
  const BATCH = 20000;
  for (let i = 0; i < ids.length; i += BATCH) {
    const input = ids.slice(i, i + BATCH).join('\n') + '\n';
    const res = spawnSync('git', ['-C', repoPath, 'cat-file',
      '--batch-check=%(objectname) %(objecttype)', '--buffer'],
      { input, encoding: 'utf8', maxBuffer: MAX_BUF, windowsHide: true });
    for (const line of (res.stdout || '').split('\n')) {
      const m = /^([0-9a-f]{40}) missing$/.exec(line.trim());
      if (m) missing.push(m[1]);
    }
  }
  return missing;
}

// Fetch explicit object ids from the promisor remote (the same command git
// uses for lazy fetch, but batched under our control).
function fetchObjects(repoPath, ids) {
  const BATCH = 5000;
  let fetched = 0;
  for (let i = 0; i < ids.length; i += BATCH) {
    const input = ids.slice(i, i + BATCH).join('\n') + '\n';
    const res = spawnSync('git', ['-C', repoPath,
      '-c', 'fetch.negotiationAlgorithm=noop',
      'fetch', 'origin', '--no-tags', '--no-write-fetch-head',
      '--recurse-submodules=no', '--filter=blob:none', '--stdin'],
      { input, encoding: 'utf8', maxBuffer: MAX_BUF, windowsHide: true });
    if (res.status === 0) fetched += Math.min(BATCH, ids.length - i);
  }
  return fetched;
}

// Collect old+new blob ids from `git log --raw` (tree-level, NO content) for
// the given selection args, then prefetch whatever is missing locally.
// Returns { needed, fetched }.
function prefetchDiffBlobs(repoPath, selectionArgs, pathspecs) {
  const args = ['log', '--all', '--no-merges', '--raw', '--no-abbrev',
    '--pretty=format:%H', ...selectionArgs];
  if (pathspecs && pathspecs.length) args.push('--', ...pathspecs);
  const res = git(repoPath, args);
  if (res.status !== 0) return { needed: 0, fetched: 0 };
  const ids = new Set();
  const re = /^:\d{6} \d{6} ([0-9a-f]{40}) ([0-9a-f]{40})/;
  for (const line of res.stdout.split('\n')) {
    const m = re.exec(line);
    if (!m) continue;
    if (!/^0+$/.test(m[1])) ids.add(m[1]);
    if (!/^0+$/.test(m[2])) ids.add(m[2]);
  }
  const missing = listMissingObjects(repoPath, [...ids]);
  const fetched = missing.length ? fetchObjects(repoPath, missing) : 0;
  return { needed: ids.size, fetched };
}

// Prefetch all historical versions of specific files (for blame): rev-list
// --objects with pathspecs is a local tree walk that lists the blob ids.
function prefetchFileHistoryBlobs(repoPath, files, sinceIso) {
  if (!files.length) return { needed: 0, fetched: 0 };
  const ids = new Set();
  const BATCH_FILES = 50;
  for (let i = 0; i < files.length; i += BATCH_FILES) {
    const args = ['rev-list', '--objects', '--all', `--since=${sinceIso}`,
      '--', ...files.slice(i, i + BATCH_FILES)];
    const res = git(repoPath, args);
    if (res.status !== 0) continue;
    for (const line of res.stdout.split('\n')) {
      const m = /^([0-9a-f]{40}) .+/.exec(line);
      if (m) ids.add(m[1]);
    }
  }
  const missing = listMissingObjects(repoPath, [...ids]);
  const fetched = missing.length ? fetchObjects(repoPath, missing) : 0;
  return { needed: ids.size, fetched };
}

// ---------------------------------------------------------------------------
// JSON IO
// ---------------------------------------------------------------------------

function readJson(file) {
  return JSON.parse(fs.readFileSync(file, 'utf8').replace(/^\uFEFF/, ''));
}

function writeJson(file, data) {
  ensureDir(path.dirname(file));
  // Compact JSON (token economy §4а.4): no pretty-printing for machine files.
  fs.writeFileSync(file, JSON.stringify(data));
}

function writeJsonPretty(file, data) {
  ensureDir(path.dirname(file));
  fs.writeFileSync(file, JSON.stringify(data, null, 1));
}

// ---------------------------------------------------------------------------
// GitLab API (token from GITLAB_TOKEN only; never persisted)
// ---------------------------------------------------------------------------

function gitlabBase() {
  const raw = process.env.GITLAB_URL || 'https://gitlab.kbpro.ru';
  return raw.replace(/\/+$/, '');
}

function gitlabToken() {
  return process.env.GITLAB_TOKEN || '';
}

async function gitlabGet(apiPath, { params = {}, token = gitlabToken(), base = gitlabBase() } = {}) {
  const url = new URL(`${base}/api/v4${apiPath}`);
  for (const [k, v] of Object.entries(params)) {
    if (v !== undefined && v !== null) url.searchParams.set(k, String(v));
  }
  const res = await fetch(url, { headers: { 'PRIVATE-TOKEN': token } });
  const text = await res.text();
  let body = null;
  try { body = JSON.parse(text); } catch (_) { body = text; }
  return { status: res.status, headers: res.headers, body };
}

// Iterate all pages of a paginated GitLab endpooint. onPage(items) may return
// false to stop early.
async function gitlabGetAll(apiPath, { params = {}, perPage = 100, maxPages = 1000, ...rest } = {}) {
  const items = [];
  for (let page = 1; page <= maxPages; page++) {
    const { status, headers, body } = await gitlabGet(apiPath, {
      params: { ...params, per_page: perPage, page },
      ...rest,
    });
    if (status !== 200) {
      throw new Error(`GitLab GET ${apiPath} page ${page} -> HTTP ${status}: ${JSON.stringify(body).slice(0, 300)}`);
    }
    if (!Array.isArray(body)) throw new Error(`GitLab GET ${apiPath}: expected array`);
    items.push(...body);
    const next = headers.get('x-next-page');
    if (!next) break;
  }
  return items;
}

// ---------------------------------------------------------------------------
// Window slicing (§2.4): monthly windows from phase start; a remainder shorter
// than one month is merged into the last full month. Phase < 1 month = single
// window. Dates are ISO `YYYY-MM-DD`, ranges inclusive [from, to].
// ---------------------------------------------------------------------------

function addMonths(isoDate, n) {
  const [y, m, d] = isoDate.split('-').map(Number);
  const dt = new Date(Date.UTC(y, m - 1 + n, d));
  // Clamp day overflow (e.g. Jan 31 + 1 month) to the last day of the target month.
  if (dt.getUTCMonth() !== ((m - 1 + n) % 12 + 12) % 12) dt.setUTCDate(0);
  return dt.toISOString().slice(0, 10);
}

function addDays(isoDate, n) {
  const dt = new Date(`${isoDate}T00:00:00Z`);
  dt.setUTCDate(dt.getUTCDate() + n);
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
      // If what's left after this window is shorter than a month, absorb it.
      const remainderStart = nextStart;
      const remainderEnd = toIso;
      const remainderFullMonthEnd = addDays(addMonths(remainderStart, 1), -1);
      if (remainderFullMonthEnd > toIso) {
        windows.push({ from: cursor, to: remainderEnd });
        cursor = addDays(remainderEnd, 1);
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

// ---------------------------------------------------------------------------
// Working-day calendar (plan v2 §3.2H): rhythm metrics count WORKING days only.
// Approximation: Mon-Fri minus fixed RU federal holidays. Exact transfer days
// vary by year; override/extend via a holidays JSON next to the cache when
// precision matters. Weekends misclassified as workdays only WIDEN bands.
// ---------------------------------------------------------------------------

const RU_FIXED_HOLIDAYS_MMDD = new Set([
  '01-01', '01-02', '01-03', '01-04', '01-05', '01-06', '01-07', '01-08',
  '02-23', '03-08', '05-01', '05-09', '06-12', '11-04',
]);

let _extraHolidays = null; // Set of 'YYYY-MM-DD', lazily loaded.

function loadExtraHolidays() {
  if (_extraHolidays) return _extraHolidays;
  _extraHolidays = new Set();
  try {
    const file = process.env.PERF_AUDIT_HOLIDAYS
      || (process.env.PERF_AUDIT_CACHE && path.join(process.env.PERF_AUDIT_CACHE, 'holidays.json'));
    if (file && fs.existsSync(file)) {
      const data = JSON.parse(fs.readFileSync(file, 'utf8'));
      for (const d of (Array.isArray(data) ? data : data.holidays || [])) _extraHolidays.add(String(d));
    }
  } catch (_) { /* optional file, ignore */ }
  return _extraHolidays;
}

function isWorkingDay(isoDate) {
  const dt = new Date(`${isoDate}T00:00:00Z`);
  const dow = dt.getUTCDay(); // 0 Sun .. 6 Sat
  if (dow === 0 || dow === 6) return false;
  if (RU_FIXED_HOLIDAYS_MMDD.has(isoDate.slice(5, 10))) return false;
  if (loadExtraHolidays().has(isoDate)) return false;
  return true;
}

function workingDaysBetween(fromIso, toIso) {
  let n = 0;
  for (let d = fromIso; d <= toIso; d = addDays(d, 1)) if (isWorkingDay(d)) n++;
  return n;
}

// ---------------------------------------------------------------------------
// Self-check style logging
// ---------------------------------------------------------------------------

function check(label, status, detail) {
  const line = `[${status}] ${label}${detail ? ` — ${detail}` : ''}`;
  console.log(line);
  return { label, status, detail: detail || '' };
}

module.exports = {
  SCHEMA_VERSION, RUBRICS_VERSION,
  CS_EXTS, ASSET_EXTS, SHADER_EXTS, CODE_EXTS, BULK_LINES,
  LOW_SAMPLE_MIN_CS_COMMITS, LOW_SAMPLE_MIN_ASSET_EFF_LOC,
  REWORK_HORIZON_DAYS, DAYS_PER_MONTH,
  SEQUENTIAL_WINDOW_LIMIT,
  SKILL_ROOT, AI_DOCS_ROOT,
  reportsRoot, selfCheckReportPath,
  isOnDriveC, assertNotDriveC,
  classifyFile, isAnalyzedKind,
  cacheRoot, runDir, ensureDir,
  parseArgs, run, git, gitOk,
  ANALYZED_PATHSPECS, listMissingObjects, fetchObjects,
  prefetchDiffBlobs, prefetchFileHistoryBlobs,
  readJson, writeJson, writeJsonPretty,
  gitlabBase, gitlabToken, gitlabGet, gitlabGetAll,
  addMonths, addDays, daysBetween, sliceWindows, perMonth,
  isWorkingDay, workingDaysBetween,
  check,
};
