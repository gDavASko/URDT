#!/usr/bin/env node
'use strict';
// dumpWindow.js — deterministic per-window git dump for the perf-audit skill.
// Node stdlib only. Consumes one or more bare clones under --cache and emits
// the compact `dump-<window_id>.json` contract from data-contracts.md.
//
// The mechanical layer of the pipeline: no LLM tokens, no side effects except
// writing the output JSON (or stdout when --out is omitted).

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');

const {
  parseArgs, git, run, classifyFile, BULK_LINES,
  SCHEMA_VERSION, writeJson, addDays,
  ANALYZED_PATHSPECS, prefetchDiffBlobs,
} = require('./lib/common.js');

// ---------------------------------------------------------------------------
// CLI
// ---------------------------------------------------------------------------

const HELP = `dumpWindow.js — collect a per-window git dump across bare-clone repos.

Usage:
  node dumpWindow.js --from YYYY-MM-DD --to YYYY-MM-DD --authors a,b,c \\
                     --cache <dir with bare clones> \\
                     [--repos id1,id2] [--window-id str] [--phase str] \\
                     [--out file.json] [--systems-map map.json]

Notes:
  - --authors: comma-separated identifiers. Strings containing '@' are matched
    against the commit author email (case-insensitive equality); other strings
    are matched against the commit author NAME (case-insensitive equality).
  - --cache: directory that contains one bare clone per repo, e.g. dentistry-cow.git.
    Each subdirectory ending in .git (or containing HEAD) is treated as a repo.
  - --systems-map: JSON file with an object of ordered {pathRegex: systemName}
    rules. When omitted, a KBPro-aware default heuristic is used (see below).
  - Output is compact JSON that follows data-contracts.md → dump-<window_id>.json.
  - When --out is omitted the JSON is printed to stdout.

Author-date vs committer-date (plan §6, mandatory choice):
  git --since/--until filters by COMMITTER date. Rebases/cherry-picks rewrite
  the committer date but preserve the author date, so we treat the AUTHOR date
  as canonical, widen the git-log window by ±30 days, and post-filter by the
  author date falling inside [from, to]. m2 may therefore differ slightly from
  a naive 'git rev-list --count --since --until'; the delta is expected.`;

const args = parseArgs(process.argv);
if (args.help || args.h) { console.log(HELP); process.exit(0); }

for (const req of ['from', 'to', 'authors', 'cache']) {
  if (!args[req]) { console.error(`missing --${req}\n\n${HELP}`); process.exit(2); }
}

const FROM = String(args.from);
const TO = String(args.to);
const AUTHORS_RAW = String(args.authors).split(',').map(s => s.trim()).filter(Boolean);
const CACHE_DIR = path.resolve(String(args.cache));
const REPO_FILTER = args.repos
  ? new Set(String(args.repos).split(',').map(s => s.trim()).filter(Boolean))
  : null;
const WINDOW_ID = args['window-id'] ? String(args['window-id']) : `AD_HOC_${FROM}`;
const PHASE = args.phase ? String(args.phase) : 'AD_HOC';
const OUT_FILE = args.out ? path.resolve(String(args.out)) : null;
const SYSTEMS_MAP = args['systems-map']
  ? loadSystemsMap(path.resolve(String(args['systems-map'])))
  : null;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

const AUTHOR_EMAILS = new Set(
  AUTHORS_RAW.filter(a => a.includes('@')).map(a => a.toLowerCase()),
);
const AUTHOR_NAMES = new Set(
  AUTHORS_RAW.filter(a => !a.includes('@')).map(a => a.toLowerCase()),
);

function matchesAuthor(name, email) {
  if (email && AUTHOR_EMAILS.has(email.toLowerCase())) return true;
  if (name && AUTHOR_NAMES.has(name.toLowerCase())) return true;
  return false;
}

function loadSystemsMap(file) {
  const raw = JSON.parse(fs.readFileSync(file, 'utf8'));
  return Object.entries(raw).map(([re, sys]) => ({ re: new RegExp(re), sys }));
}

// Discover bare clones under --cache. Each entry becomes a "repo" with id
// derived from the folder name (strip trailing .git).
function discoverRepos(dir) {
  const entries = fs.readdirSync(dir, { withFileTypes: true }).filter(e => e.isDirectory());
  const repos = [];
  for (const e of entries) {
    const p = path.join(dir, e.name);
    const looksBare = e.name.endsWith('.git') || fs.existsSync(path.join(p, 'HEAD'));
    if (!looksBare) continue;
    const id = e.name.replace(/\.git$/, '');
    if (REPO_FILTER && !REPO_FILTER.has(id)) continue;
    repos.push({ id, path: p });
  }
  return repos;
}

function parseCcType(subject) {
  const subj = String(subject || '').trim();
  if (/^revert[:\s"]/i.test(subj) || /^Revert\s+"/i.test(subj)) {
    return { cc_type: 'revert', is_revert: true };
  }

  // 1. Try standard Conventional Commits regex
  const m = /^(feat|fix|refactor|perf|test|docs|chore|style|build|ci|revert)(?:\([^)]*\))?!?:/i
    .exec(subj);
  if (m) {
    return { cc_type: m[1].toLowerCase(), is_revert: false };
  }

  // 2. Heuristic keywords search (English & Russian)
  const lower = subj.toLowerCase();

  // Revert heuristics
  if (lower.includes('revert') || lower.includes('откат') || lower.includes('откатил')) {
    return { cc_type: 'revert', is_revert: true };
  }

  // Fix heuristics
  if (
    lower.includes('fix') || lower.includes('bug') || lower.includes('issue') || 
    lower.includes('error') || lower.includes('crash') || lower.includes('solve') || 
    lower.includes('правка') || lower.includes('правки') || lower.includes('исправ') || 
    lower.includes('почин') || lower.includes('фикс') || lower.includes('устранил') || 
    lower.includes('ошибка') || lower.includes('баг') || lower.includes('полишинг')
  ) {
    return { cc_type: 'fix', is_revert: false };
  }

  // Feat heuristics
  if (
    lower.includes('feat') || lower.includes('add') || lower.includes('new') || 
    lower.includes('implement') || lower.includes('create') || lower.includes('added') || 
    lower.includes('добав') || lower.includes('нов') || lower.includes('создал') || 
    lower.includes('создан') || lower.includes('внедр') || lower.includes('реализ') || 
    lower.includes('сделал') || lower.includes('база') || lower.includes('брекеты')
  ) {
    return { cc_type: 'feat', is_revert: false };
  }

  // Refactor heuristics
  if (
    lower.includes('refactor') || lower.includes('clean') || lower.includes('cleanup') || 
    lower.includes('rewrite') || lower.includes('рефактор') || lower.includes('перепис') || 
    lower.includes('передел') || lower.includes('перенос') || lower.includes('чистк')
  ) {
    return { cc_type: 'refactor', is_revert: false };
  }

  // Docs heuristics
  if (
    lower.includes('docs') || lower.includes('readme') || lower.includes('wiki') || 
    lower.includes('comment') || lower.includes('документ') || lower.includes('доки') || 
    lower.includes('коммент') || lower.includes('описани')
  ) {
    return { cc_type: 'docs', is_revert: false };
  }

  // Test heuristics
  if (
    lower.includes('test') || lower.includes('testing') || lower.includes('unit') || 
    lower.includes('тест') || lower.includes('тестир')
  ) {
    return { cc_type: 'test', is_revert: false };
  }

  // Perf heuristics
  if (
    lower.includes('perf') || lower.includes('speed') || lower.includes('fast') || 
    lower.includes('ускор') || lower.includes('производ')
  ) {
    return { cc_type: 'perf', is_revert: false };
  }

  // Chore / Style / Build / CI heuristics
  if (
    lower.includes('chore') || lower.includes('style') || lower.includes('format') || 
    lower.includes('lint') || lower.includes('deps') || lower.includes('merge') || 
    lower.includes('update') || lower.includes('upgrade') || lower.includes('asset') || 
    lower.includes('обновл') || lower.includes('мердж') || lower.includes('слиян') || 
    lower.includes('анимац') || lower.includes('звук')
  ) {
    return { cc_type: 'chore', is_revert: false };
  }

  return { cc_type: 'unclassified', is_revert: false };
}

function isSquashSubject(subject) {
  return /See merge request/i.test(subject) || /![\d]+/.test(subject);
}

// System resolution: apply user-provided map first (ordered), else the default
// KBPro heuristic. See plan §3 m3.
function mapSystem(filePath, repoId) {
  if (SYSTEMS_MAP) {
    for (const rule of SYSTEMS_MAP) if (rule.re.test(filePath)) return rule.sys;
  }
  const parts = filePath.split('/').filter(Boolean);
  if (parts[0] === 'Assets') {
    if (parts[1] === 'KBPro' && parts[2]) return parts[2];
    if (parts[1] === 'Core' && parts[2]) return `Core/${parts[2]}`;
    if (parts[1] === 'Dentistry-cow' && parts[2]) return `Game/${parts[2]}`;
    if (parts[1]) return `Assets/${parts[1]}`;
    return `${repoId}/Assets`;
  }
  if (parts[0] === 'src' || parts[0] === 'packages') {
    return parts[1] ? `${parts[0]}/${parts[1]}` : parts[0];
  }
  return parts[0] || repoId;
}

// ---------------------------------------------------------------------------
// git log parsing
// ---------------------------------------------------------------------------

// Record marker + field separator carefully chosen to avoid clashing with
// subject text (\x1f = ASCII unit separator).
const REC = '@@COMMIT@@';
const FS = '\x1f';
const PRETTY = `tformat:${REC}%H${FS}%an${FS}%ae${FS}%aI${FS}%cI${FS}%P${FS}%s`;

function widenedGitRange() {
  // Author-date aware widening (see HELP). ±30 days is plenty for typical rebases.
  return { since: addDays(FROM, -30), until: addDays(TO, 30) };
}

// Partial-clone economy (2026-07-17): every git selection is narrowed with
// --author (server-side commit filter; JS matchesAuthor stays the precise
// filter) and content passes are pathspec-limited to analyzed extensions,
// with the needed blobs PREFETCHED in batches (common.prefetchDiffBlobs).
// --find-renames is deliberately NOT used: inexact rename detection compares
// blob contents and triggers lazy-fetch storms on blobless clones; renames
// appear as A+D pairs (accepted accuracy trade-off, noted in dump.notes).
function escapeAuthorRegex(s) {
  return String(s).replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function authorSelectionArgs() {
  return ['-i', ...AUTHORS_RAW.map(a => `--author=${escapeAuthorRegex(a)}`)];
}

function selectionArgs() {
  const { since, until } = widenedGitRange();
  return [`--since=${since}`, `--until=${until}T23:59:59`, ...authorSelectionArgs()];
}

// Two log passes: (1) numstat for LOC (analyzed paths only, blobs prefetched),
// (2) name-status for A/M/D and the full changed-file list (tree-only, cheap).
function runLogNumstat(repoPath) {
  const res = git(repoPath, [
    'log', '--all', '--no-merges', '--numstat',
    '--date=iso-strict', ...selectionArgs(),
    `--pretty=${PRETTY}`,
    '--', ...ANALYZED_PATHSPECS,
  ]);
  if (res.status !== 0) throw new Error(`git log numstat failed: ${res.stderr}`);
  return res.stdout;
}

function runLogNameStatus(repoPath) {
  const res = git(repoPath, [
    'log', '--all', '--no-merges', '--name-status',
    '--date=iso-strict', ...selectionArgs(),
    `--pretty=${PRETTY}`,
  ]);
  if (res.status !== 0) throw new Error(`git log name-status failed: ${res.stderr}`);
  return res.stdout;
}

function parseLogChunks(raw) {
  // Split on the record marker and yield {header, body[]} per commit.
  const parts = raw.split(REC).map(s => s.replace(/\r/g, '')).filter(s => s.trim());
  return parts.map(chunk => {
    const lines = chunk.split('\n');
    const header = lines.shift();
    const body = lines.filter(l => l.length > 0);
    return { header, body };
  });
}

function parseHeader(headerLine) {
  const parts = headerLine.split(FS);
  return {
    sha: parts[0],
    author_name: parts[1] || '',
    author_email: parts[2] || '',
    author_date: parts[3] || '',
    commit_date: parts[4] || '',
    parents: (parts[5] || '').split(' ').filter(Boolean),
    subject: parts[6] || '',
  };
}

// Parse numstat body line: "add\tdel\tpath". Handles renames of form
// "old => new", "prefix/{old => new}/suffix" and "{ => new}/x".
function parseNumstatLine(line) {
  const parts = line.split('\t');
  if (parts.length < 3) return null;
  const [addStr, delStr, ...rest] = parts;
  let filePath = rest.join('\t');
  let oldPath = null;
  if (filePath.includes(' => ')) {
    const brace = /\{([^{}]*) => ([^{}]*)\}/.exec(filePath);
    if (brace) {
      oldPath = filePath.replace(brace[0], brace[1]).replace(/\/+/g, '/').replace(/^\/|\/$/g, '');
      filePath = filePath.replace(brace[0], brace[2]).replace(/\/+/g, '/').replace(/^\/|\/$/g, '');
    } else {
      const m = /^(.*) => (.*)$/.exec(filePath);
      if (m) { oldPath = m[1]; filePath = m[2]; }
    }
  }
  const binary = addStr === '-' || delStr === '-';
  return {
    loc_add: binary ? 0 : parseInt(addStr, 10) || 0,
    loc_del: binary ? 0 : parseInt(delStr, 10) || 0,
    path: filePath,
    old_path: oldPath,
    binary,
  };
}

// Parse name-status body line: "A\tpath" | "M\tpath" | "D\tpath"
// | "R100\told\tnew" | "C90\told\tnew".
function parseNameStatusLine(line) {
  const parts = line.split('\t');
  if (parts.length < 2) return null;
  const status = parts[0][0];
  if (status === 'R' || status === 'C') {
    return { change: 'R', path: parts[2], old_path: parts[1] };
  }
  return { change: status, path: parts.slice(1).join('\t') };
}

// ---------------------------------------------------------------------------
// patch-id (deterministic dedup across branches, plan §6)
// ---------------------------------------------------------------------------

// Batched patch-id (v1 defect J: two spawned processes PER COMMIT were
// unbearably slow on Windows). `git show sha1 sha2 ... | git patch-id --stable`
// emits one "patch-id sha" line per commit; we batch to keep command lines and
// buffers bounded.
const PATCH_ID_BATCH = 50;

function batchPatchIds(repoPath, shas) {
  const result = new Map(); // sha -> patch-id (or absent on failure)
  for (let i = 0; i < shas.length; i += PATCH_ID_BATCH) {
    const batch = shas.slice(i, i + PATCH_ID_BATCH);
    // Pathspec-limited: patch-id over analyzed content only — full-diff show
    // would lazily fetch every binary blob on a blobless clone.
    const show = spawnSync('git', ['-C', repoPath, 'show', ...batch, '--', ...ANALYZED_PATHSPECS],
      { encoding: 'buffer', maxBuffer: 1024 * 1024 * 1024, windowsHide: true });
    if (show.status !== 0) continue;
    const pid = spawnSync('git', ['patch-id', '--stable'],
      { input: show.stdout, encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
    if (pid.status !== 0) continue;
    for (const line of (pid.stdout || '').split('\n')) {
      const [id, sha] = line.trim().split(/\s+/);
      if (id && sha) result.set(sha, id);
    }
  }
  return result;
}

// ---------------------------------------------------------------------------
// Unity YAML asset effective LOC
// ---------------------------------------------------------------------------

const YAML_HEADER_RE = /^---\s*!u!(\d+)\s+&(-?\d+)/;
const ASSET_SIZE_CAP = 2 * 1024 * 1024;

function buildFileIdMap(content) {
  const lines = content.split(/\r?\n/);
  const map = new Array(lines.length + 2);
  let cur = null;
  for (let i = 0; i < lines.length; i++) {
    const m = YAML_HEADER_RE.exec(lines[i]);
    if (m) cur = `${m[1]}/${m[2]}`;
    map[i + 1] = cur;
  }
  return map;
}

function parseAssetDiff(diffText, newLineMap) {
  const touched = new Set();
  let addedHeaders = 0;
  let removedHeaders = 0;
  let hunkCount = 0;
  const lines = diffText.split(/\r?\n/);
  let curNew = 0;
  let inHunk = false;
  for (const line of lines) {
    if (line.startsWith('@@')) {
      const h = /^@@ -\d+(?:,\d+)? \+(\d+)(?:,\d+)? @@/.exec(line);
      if (h) { curNew = parseInt(h[1], 10); inHunk = true; hunkCount++; }
      continue;
    }
    if (!inHunk) continue;
    if (line.startsWith('+++') || line.startsWith('---')) continue;
    const c = line[0];
    if (c === '\\') continue;
    if (c === '+') {
      const body = line.slice(1);
      const hdr = YAML_HEADER_RE.exec(body);
      if (hdr) { addedHeaders++; touched.add(`${hdr[1]}/${hdr[2]}`); }
      else { const id = newLineMap[curNew]; if (id) touched.add(id); }
      curNew++;
    } else if (c === '-') {
      const body = line.slice(1);
      const hdr = YAML_HEADER_RE.exec(body);
      if (hdr) { removedHeaders++; touched.add(`${hdr[1]}/${hdr[2]}`); }
      else { const id = newLineMap[curNew]; if (id) touched.add(id); }
    } else if (c === ' ') {
      curNew++;
    }
  }
  return { touched: touched.size, addedHeaders, removedHeaders, hunkCount };
}

function assetEffLoc(repoPath, sha, filePath) {
  try {
    const show = spawnSync('git', ['-C', repoPath, 'show', `${sha}:${filePath}`],
      { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024, windowsHide: true });
    if (show.status !== 0) return { eff_loc: 0, note: 'no_new_content' };
    if (show.stdout.length > ASSET_SIZE_CAP) {
      const diff = spawnSync('git', ['-C', repoPath, 'show', '--unified=0', sha, '--', filePath],
        { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024, windowsHide: true });
      const hunks = (diff.stdout.match(/^@@ /gm) || []).length;
      return { eff_loc: Math.min(hunks, 50), note: 'size_cap_fallback' };
    }
    const map = buildFileIdMap(show.stdout);
    const diff = spawnSync('git', ['-C', repoPath, 'show', sha, '--', filePath],
      { encoding: 'utf8', maxBuffer: 128 * 1024 * 1024, windowsHide: true });
    if (diff.status !== 0) return { eff_loc: 0, note: 'no_diff' };
    const parsed = parseAssetDiff(diff.stdout, map);
    const eff = parsed.touched + parsed.addedHeaders + parsed.removedHeaders;
    if (eff === 0) {
      const hunks = (diff.stdout.match(/^@@ /gm) || []).length;
      return { eff_loc: Math.min(hunks, 50), note: 'parse_empty_fallback' };
    }
    return { eff_loc: eff, note: null };
  } catch (e) {
    return { eff_loc: 0, note: `parse_error:${(e && e.message) || 'x'}` };
  }
}

// ---------------------------------------------------------------------------
// Class extraction for .cs files
// ---------------------------------------------------------------------------

const CLASS_RE = /^[+-].*\b(?:class|interface|struct)\s+([A-Za-z_][A-Za-z0-9_]*)/gm;

function extractClasses(diffText) {
  const names = new Set();
  let m;
  CLASS_RE.lastIndex = 0;
  while ((m = CLASS_RE.exec(diffText)) !== null) names.add(m[1]);
  return [...names];
}

function csFileDiff(repoPath, sha, filePath) {
  const res = spawnSync('git', ['-C', repoPath, 'show', sha, '--', filePath],
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  return res.status === 0 ? res.stdout : '';
}

// ---------------------------------------------------------------------------
// Repo tips (for cache key downstream)
// ---------------------------------------------------------------------------

function repoTips(repoPath) {
  const res = git(repoPath, ['for-each-ref', '--format=%(refname:short) %(objectname)', 'refs/heads/']);
  const out = {};
  if (res.status !== 0) return out;
  for (const line of res.stdout.split('\n')) {
    const [b, sha] = line.trim().split(' ');
    if (b && sha) out[b] = sha;
  }
  return out;
}

// ---------------------------------------------------------------------------
// Core pipeline
// ---------------------------------------------------------------------------

function collectRepoCommits(repo) {
  // Prefetch exactly the blobs the analyzed-path diffs will need (batched,
  // instead of per-blob lazy fetches that stall blobless clones for hours).
  const pf = prefetchDiffBlobs(repo.path, selectionArgs(), ANALYZED_PATHSPECS);
  if (pf.fetched > 0) {
    console.error(`  prefetched ${pf.fetched}/${pf.needed} blobs for ${repo.id}`);
  }

  const numOut = runLogNumstat(repo.path);
  const nsOut = runLogNameStatus(repo.path);
  const numChunks = parseLogChunks(numOut);
  const nsChunks = parseLogChunks(nsOut);

  // LOC map from the pathspec-limited numstat pass: sha -> path -> numstat.
  const locMap = new Map();
  for (const { header, body } of numChunks) {
    const h = parseHeader(header);
    const perFile = new Map();
    for (const line of body) {
      const nf = parseNumstatLine(line);
      if (nf) perFile.set(nf.path, nf);
    }
    locMap.set(h.sha, perFile);
  }

  // Name-status is the authoritative changed-file list (tree-only diff: sees
  // ALL files including the "other" bucket without touching blob contents).
  const results = [];
  for (const { header, body } of nsChunks) {
    const h = parseHeader(header);
    if (!matchesAuthor(h.author_name, h.author_email)) continue;
    const authorDay = (h.author_date || '').slice(0, 10);
    if (authorDay < FROM || authorDay > TO) continue;
    const locs = locMap.get(h.sha) || new Map();
    const rawFiles = [];
    for (const line of body) {
      const p = parseNameStatusLine(line);
      if (!p || !p.path) continue;
      const nf = locs.get(p.path);
      rawFiles.push({
        path: p.path,
        old_path: p.old_path || (nf && nf.old_path) || null,
        change: p.change,
        loc_add: nf ? nf.loc_add : 0,
        loc_del: nf ? nf.loc_del : 0,
        binary: nf ? nf.binary : false,
      });
    }
    results.push({ header: h, rawFiles });
  }
  return results;
}

function selectDiffSamples(commits) {
  // Prefer fix commits, then largest cs commits, cap at 15.
  const withCs = commits
    .map(c => {
      const csFiles = c.files.filter(f => f.kind === 'cs');
      const csLoc = csFiles.reduce((s, f) => s + f.loc_add + f.loc_del, 0);
      return { c, csFiles, csLoc };
    })
    .filter(x => x.csFiles.length > 0);
  const fixes = withCs.filter(x => x.c.cc_type === 'fix');
  const rest = withCs.filter(x => x.c.cc_type !== 'fix').sort((a, b) => b.csLoc - a.csLoc);
  const chosen = [];
  for (const x of fixes) { if (chosen.length >= 15) break; chosen.push(x); }
  for (const x of rest) { if (chosen.length >= 15) break; chosen.push(x); }
  return chosen;
}

function main() {
  if (!fs.existsSync(CACHE_DIR)) {
    console.error(`cache dir not found: ${CACHE_DIR}`); process.exit(2);
  }
  const repos = discoverRepos(CACHE_DIR);
  if (repos.length === 0) {
    console.error(`no bare clones under ${CACHE_DIR}`); process.exit(2);
  }

  const dump = {
    schema_version: SCHEMA_VERSION,
    window_id: WINDOW_ID,
    phase: PHASE,
    range: { from: FROM, to: TO, days: (Date.parse(`${TO}T00:00:00Z`) - Date.parse(`${FROM}T00:00:00Z`)) / 86400000 + 1 },
    authors: AUTHORS_RAW,
    repos: [],
    path_table: [],
    commits: [],
    new_modules_signals: [],
    excluded: { merges: 0, dedup_sha: 0, dedup_patchid: 0, other_files: 0, bulk_files: 0 },
    diff_samples: [],
    notes: ['RENAMES_AS_ADD_DELETE'],
  };

  const pathIndex = new Map();
  const pushPath = (p) => {
    if (pathIndex.has(p)) return pathIndex.get(p);
    const i = pathIndex.size;
    pathIndex.set(p, i);
    dump.path_table.push(p);
    return i;
  };

  // (a) sha-level dedup across repos (submodule + superproject).
  const seenShas = new Set();
  // (b) patch-id dedup per repo across branches.
  const seenPatchIdsByRepo = new Map();

  // Budget for cs full-file diff fetches (plan §3 classes note).
  let csDiffBudget = 200;

  // Diff cache per (sha,path) to reuse across sample selection.
  const diffCache = new Map();
  const cacheDiff = (repoPath, sha, filePath) => {
    const key = `${sha}|${filePath}`;
    if (diffCache.has(key)) return diffCache.get(key);
    const text = csFileDiff(repoPath, sha, filePath);
    diffCache.set(key, text);
    return text;
  };

  // First pass: collect candidate commits per repo, drop merge/other files,
  // classify, and compute patch-id (needed for cross-repo dedup and squash).
  const perRepo = [];
  for (const repo of repos) {
    dump.repos.push({ id: repo.id, tips: repoTips(repo.path) });
    const commits = collectRepoCommits(repo);
    perRepo.push({ repo, commits });
  }

  // Second pass: enrich each commit with files[], asset eff_loc, classes,
  // squash flags. Do dedup on the fly.
  const collected = []; // { repo, header, files, cc_type, is_revert, subject, dedup?: reason }
  for (const { repo, commits } of perRepo) {
    if (!seenPatchIdsByRepo.has(repo.id)) seenPatchIdsByRepo.set(repo.id, new Map());
    const seenPidRepo = seenPatchIdsByRepo.get(repo.id);
    const pidMap = batchPatchIds(repo.path, commits.map(c => c.header.sha));
    for (const c of commits) {
      const h = c.header;
      if (seenShas.has(h.sha)) { dump.excluded.dedup_sha++; continue; }
      seenShas.add(h.sha);

      const files = [];
      let otherCount = 0;
      let bulkCount = 0;
      for (const rf of c.rawFiles) {
        const kind = classifyFile(rf.path);
        if (kind === 'other') { otherCount++; continue; }
        const size = (rf.loc_add || 0) + (rf.loc_del || 0);
        if (size > BULK_LINES && kind !== 'cs') { bulkCount++; continue; }
        files.push({
          _rawPath: rf.path, _oldPath: rf.old_path,
          kind, change: rf.change,
          loc_add: rf.loc_add, loc_del: rf.loc_del,
          eff_loc: 0,
          classes: [],
          prefab: kind === 'prefab' ? path.basename(rf.path, '.prefab') : '',
          _binary: rf.binary,
        });
        // Signal for m4: added .asmdef files. .asmdef is 'other', so track
        // separately BEFORE dropping it.
      }
      // Track asmdef signals from raw files.
      for (const rf of c.rawFiles) {
        if (/\.asmdef$/i.test(rf.path) && rf.change === 'A') {
          dump.new_modules_signals.push({
            sha: h.sha, path: rf.path, dir: path.posix.dirname(rf.path.replace(/\\/g, '/')),
            reason: 'asmdef_added',
          });
        }
      }

      dump.excluded.other_files += otherCount;
      dump.excluded.bulk_files += bulkCount;

      // Compute eff_loc per file
      for (const f of files) {
        if (f.kind === 'cs' || f.kind === 'shader' || f.kind === 'code') {
          f.eff_loc = f.loc_add + f.loc_del;
        } else {
          // Skip binary / deleted files (git show sha:path fails for deletes).
          if (f._binary || f.change === 'D') {
            f.eff_loc = 0;
          } else {
            const r = assetEffLoc(repo.path, h.sha, f._rawPath);
            f.eff_loc = r.eff_loc;
            if (r.note) f._note = r.note;
          }
        }
      }

      // Extract classes for cs files (budgeted).
      for (const f of files) {
        if (f.kind !== 'cs') continue;
        if (f._binary || f.change === 'D') continue;
        if (csDiffBudget <= 0) continue;
        csDiffBudget--;
        const diff = cacheDiff(repo.path, h.sha, f._rawPath);
        f.classes = extractClasses(diff);
        // m4 signal (plan v2 §3.2I): only classes that INHERIT the module base
        // types count. Name-suffix matching ("...System", "...Service") caught
        // nearly every class in KBPro code style and inflated raw m4.
        if (f.change === 'A') {
          const dir = path.posix.dirname(f._rawPath.replace(/\\/g, '/'));
          const inheritRe = /^\+.*\bclass\s+([A-Za-z_][A-Za-z0-9_]*)(?:<[^>]*>)?\s*:\s*[^{\r\n]*\b(LogicSystem|GameComponent)\b/gm;
          let im;
          while ((im = inheritRe.exec(diff)) !== null) {
            dump.new_modules_signals.push({
              sha: h.sha, path: f._rawPath, dir, reason: 'cs_module_class',
              class_name: im[1], base: im[2],
            });
          }
        }
      }
      if (csDiffBudget <= 0 && !dump.notes.includes('CS_DIFF_BUDGET_EXHAUSTED')) {
        dump.notes.push('CS_DIFF_BUDGET_EXHAUSTED');
      }

      const cc = parseCcType(h.subject);
      const pid = pidMap.get(h.sha) || null;
      let flags = [];
      let excluded = null;

      if (pid) {
        if (seenPidRepo.has(pid)) {
          // Same patch-id already accounted for on another ref (cherry-pick etc.).
          excluded = 'dedup_patchid';
        } else {
          seenPidRepo.set(pid, h.sha);
        }
      }

      if (excluded === 'dedup_patchid') {
        dump.excluded.dedup_patchid++;
        continue;
      }

      collected.push({
        repo, header: h, files, cc_type: cc.cc_type, is_revert: cc.is_revert,
        subject: h.subject, flags, patch_id: pid,
      });
    }
  }

  // Squash heuristic (plan §6): if subject looks like a squash-merge AND all its
  // files are covered by other non-squash commits in the window from the same
  // repo, drop it as dedup_patchid. Otherwise keep with SQUASH_OPAQUE flag.
  const nonSquashFilesByRepo = new Map();
  for (const c of collected) {
    if (isSquashSubject(c.subject)) continue;
    if (!nonSquashFilesByRepo.has(c.repo.id)) nonSquashFilesByRepo.set(c.repo.id, new Set());
    const set = nonSquashFilesByRepo.get(c.repo.id);
    for (const f of c.files) set.add(f._rawPath);
  }
  const finalCommits = [];
  for (const c of collected) {
    if (isSquashSubject(c.subject) && c.files.length > 0) {
      const set = nonSquashFilesByRepo.get(c.repo.id) || new Set();
      const covered = c.files.every(f => set.has(f._rawPath));
      if (covered && c.files.length > 0) {
        dump.excluded.dedup_patchid++;
        continue;
      } else {
        c.flags.push('SQUASH_OPAQUE');
      }
    }
    finalCommits.push(c);
  }

  // Emit compact commits + diff samples.
  const samples = selectDiffSamples(
    finalCommits.map(c => ({
      cc_type: c.cc_type,
      files: c.files.map(f => ({ kind: f.kind, loc_add: f.loc_add, loc_del: f.loc_del, path: f._rawPath })),
      repo: c.repo, header: c.header,
    })),
  );
  const samplesByRepoSha = new Map();
  for (const s of samples) samplesByRepoSha.set(`${s.c.repo.id}|${s.c.header.sha}`, s);

  for (const c of finalCommits) {
    const commit = {
      repo: c.repo.id,
      sha: c.header.sha,
      date: c.header.author_date,
      commit_date: c.header.commit_date,
      subject: c.subject,
      cc_type: c.cc_type,
      is_revert: c.is_revert,
      is_merge_excluded_dup: false,
      flags: c.flags,
      branch_context: '', // best-effort skipped in v1 (plan note; too slow per commit)
      files: c.files.map(f => ({
        p: pushPath(f._rawPath),
        kind: f.kind,
        system: mapSystem(f._rawPath, c.repo.id),
        change: f.change,
        loc_add: f.loc_add,
        loc_del: f.loc_del,
        eff_loc: f.eff_loc,
        classes: f.classes,
        prefab: f.prefab,
      })),
    };
    dump.commits.push(commit);

    const s = samplesByRepoSha.get(`${c.repo.id}|${c.header.sha}`);
    if (s) {
      let budget = 150;
      const parts = [];
      const primary = s.csFiles[0];
      for (const cf of s.csFiles) {
        if (budget <= 0) break;
        const diff = cacheDiff(c.repo.path, c.header.sha, cf.path);
        const lines = diff.split('\n');
        const take = Math.min(budget, lines.length);
        parts.push(lines.slice(0, take).join('\n'));
        budget -= take;
      }
      dump.diff_samples.push({
        sha: c.header.sha,
        file: primary ? primary.path : '',
        diff: parts.join('\n'),
      });
    }
  }

  // Strip helper fields from files (kept during build only).
  for (const commit of dump.commits) {
    for (const f of commit.files) {
      // No helper fields survived the map() above.
    }
  }

  if (OUT_FILE) {
    writeJson(OUT_FILE, dump);
    console.error(`wrote ${OUT_FILE} (commits=${dump.commits.length})`);
  } else {
    process.stdout.write(JSON.stringify(dump));
  }
}

main();
