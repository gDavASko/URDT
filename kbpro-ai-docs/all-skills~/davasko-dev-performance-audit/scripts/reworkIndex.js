#!/usr/bin/env node
'use strict';
// reworkIndex.js v2 — blame-based rework engine (plan v2 §3.2 A/B/C).
//
// What changed vs v1 (which produced garbage):
//   - v1 counted every PAIR of touches of the same file within the horizon as a
//     "rework candidate" (O(N^2), punished normal iterative development).
//     v2 counts LINES: a line added by the subject that a later commit deletes
//     within REWORK_HORIZON_DAYS, attributed via `git blame` of the deleting
//     commit's parent. Ratios, not absolute link counts.
//   - v1 attributed rework to the window of the FIX date; v2 attributes to the
//     window of the ORIGINAL commit (where the defect was born) — otherwise a
//     before/after innovation comparison blames the "after" phase for "before"
//     defects.
//   - v1 matched fix subjects with /(fix|хотфикс|bug)/i (hits "prefix",
//     "suffix"); v2 uses word boundaries.
//   - Hub files (shared wiring touched by everyone) are EXCLUDED, not weighted.
//   - Self-rework distinguishes fix-rework (own fix commits rewriting own
//     recent lines — quality signal, m8.self_fix_*) from churn (own non-fix
//     commits rewriting own recent lines — neutral iteration, m8.churn_*).
//
// Outputs:
//   - <registry>/rework-index.json  (hub files, evidence links, flags)
//   - updates window-*.json: m8 (self rework), m9 (fixed by others), m16.
//
// Only .cs files participate: asset/prefab "rework" is meaningless YAML churn.
//
// Node stdlib only. All output is descriptive evidence for a human — never a
// standalone "reliability score".

const fs = require('fs');
const path = require('path');
const { spawnSync } = require('child_process');
const common = require('./lib/common.js');

const HELP = `reworkIndex.js — blame-based rework metrics (m8/m9/m16)

Usage:
  node reworkIndex.js --registry <dir> --authors <a,b,c> --cache <dir> [--out <file>]

Options:
  --registry <dir>   Registry dir with dump-*.json and window-*.json.
  --authors <list>   Comma-separated subject author emails/ids (case-insensitive).
  --cache <dir>      Root with bare (partial) clones of analyzed repos.
  --out <file>       Output file (default: <registry>/rework-index.json).
  --blame-budget <n> Max blame operations per run (default 2000).
  --help             Print this message and exit 0.`;

const EVIDENCE_CAP_PER_WINDOW = 50;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function die(msg, code = 1) { console.error(msg); process.exit(code); }
function isoDate(iso) { return String(iso).slice(0, 10); }

function addDaysIso(iso, n) {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + n);
  return d.toISOString().slice(0, 10);
}

function daysBetweenIso(fromIso, toIso) {
  return Math.round((Date.parse(toIso) - Date.parse(fromIso)) / 86400000);
}

function percentile(sortedNums, p) {
  if (!sortedNums.length) return 0;
  const idx = (sortedNums.length - 1) * p;
  const lo = Math.floor(idx);
  const hi = Math.ceil(idx);
  if (lo === hi) return sortedNums[lo];
  return sortedNums[lo] + (sortedNums[hi] - sortedNums[lo]) * (idx - lo);
}

// Word-boundary fix detection (v1 defect D: /(fix|bug)/i matched "prefix").
const FIX_LATIN_RE = /\b(fix(es|ed)?|bug(fix)?|hotfix)\b/i;
const FIX_CYR_RE = /(^|[^а-яё])(хотфикс|баг|фикс)([^а-яё]|$)/i;

function isFixText(subject) {
  if (!subject) return false;
  return FIX_LATIN_RE.test(subject) || FIX_CYR_RE.test(subject);
}

function isFixCommit(ccType, subject) {
  if (String(ccType || '').toLowerCase() === 'fix') return true;
  return isFixText(subject);
}

// ---------------------------------------------------------------------------
// Repo discovery in cache dir (unchanged from v1)
// ---------------------------------------------------------------------------

function isGitRepo(p) {
  if (!fs.existsSync(p)) return false;
  const asBare = fs.existsSync(path.join(p, 'HEAD')) && fs.existsSync(path.join(p, 'config'));
  const asWork = fs.existsSync(path.join(p, '.git'));
  return asBare || asWork;
}

function extractRepoIdFromRemote(url) {
  if (!url) return null;
  let s = url.trim().replace(/\.git$/, '');
  s = s.replace(/^[a-z]+:\/\//i, '');
  s = s.replace(/^[^@]+@/, '');
  s = s.replace(/^[^:/]+[:/]/, '');
  const parts = s.split('/').filter(Boolean);
  if (parts.length < 2) return parts.join('/') || null;
  return parts.slice(-2).join('/');
}

function discoverRepos(cacheDir) {
  const map = new Map(); // repoId -> local path (both "group/project" and dir-name keys)
  if (!fs.existsSync(cacheDir)) return map;
  for (const e of fs.readdirSync(cacheDir, { withFileTypes: true })) {
    if (!e.isDirectory()) continue;
    const full = path.join(cacheDir, e.name);
    if (!isGitRepo(full)) continue;
    let repoId = null;
    try {
      const r = common.git(full, ['remote', 'get-url', 'origin']);
      if (r.status === 0) repoId = extractRepoIdFromRemote(r.stdout);
    } catch (_) { /* ignore */ }
    map.set(e.name.replace(/\.git$/, ''), full);
    if (repoId) map.set(repoId, full);
  }
  return map;
}

function isShallow(repoPath) {
  try {
    const r = common.git(repoPath, ['rev-parse', '--is-shallow-repository']);
    return r.status === 0 && r.stdout.trim() === 'true';
  } catch (_) { return false; }
}

// ---------------------------------------------------------------------------
// Scan: all commits in range touching .cs files (for hub detection and
// fixed-by-others discovery).
// ---------------------------------------------------------------------------

const REC_SEP = '--REC--';

function loadRepoScan(repoPath, sinceIso, untilIso) {
  // No --find-renames: inexact rename detection reads blob contents and
  // lazy-fetch-storms blobless clones (2026-07-17 fix). Tree-only pass.
  const args = [
    'log', '--all', '--no-merges',
    `--since=${sinceIso}`, `--until=${untilIso}T23:59:59`,
    `--pretty=format:${REC_SEP}%n%H|%ae|%an|%aI|%s`,
    '--name-only',
  ];
  const res = common.git(repoPath, args);
  if (res.status !== 0) throw new Error(`git log failed in ${repoPath}: ${res.stderr}`);
  const commits = [];
  const blocks = res.stdout.split(REC_SEP + '\n').slice(1);
  for (const block of blocks) {
    const lines = block.split(/\r?\n/);
    const header = lines.shift();
    if (!header) continue;
    const parts = header.split('|');
    if (parts.length < 4) continue;
    const files = [];
    for (const rawLine of lines) {
      const line = rawLine.trim();
      if (!line) continue;
      const renameIdx = line.indexOf(' => ');
      const p = renameIdx >= 0 ? line.slice(renameIdx + 4) : line;
      if (common.classifyFile(p) !== 'cs') continue;
      files.push(p);
    }
    commits.push({
      sha: parts[0],
      ae: (parts[1] || '').toLowerCase(),
      an: parts[2] || '',
      date: parts[3] || '',
      subject: parts.slice(4).join('|'),
      files,
    });
  }
  return commits;
}

// ---------------------------------------------------------------------------
// Deleted-line ranges of a commit for one file: `git show --unified=0`.
// Returns [{start, count}] on the OLD (parent) side.
// ---------------------------------------------------------------------------

function deletedRanges(repoPath, sha, filePath) {
  const res = spawnSync('git', ['-C', repoPath, 'show', '--unified=0', '--no-color', sha, '--', filePath],
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  if (res.status !== 0) return [];
  const ranges = [];
  const re = /^@@ -(\d+)(?:,(\d+))? \+\d+(?:,\d+)? @@/gm;
  let m;
  while ((m = re.exec(res.stdout)) !== null) {
    const start = parseInt(m[1], 10);
    const count = m[2] === undefined ? 1 : parseInt(m[2], 10);
    if (count > 0) ranges.push({ start, count });
  }
  return ranges;
}

// Blame the PARENT of `sha` for the given ranges of `filePath`.
// Returns Map<originSha, lineCount> for the deleted lines.
function blameDeletedLines(repoPath, sha, filePath, ranges) {
  if (!ranges.length) return new Map();
  const args = ['-C', repoPath, 'blame', '--line-porcelain', '-w'];
  for (const r of ranges) args.push('-L', `${r.start},+${r.count}`);
  args.push(`${sha}^`, '--', filePath);
  const res = spawnSync('git', args,
    { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024, windowsHide: true });
  if (res.status !== 0) return new Map();
  const byOrigin = new Map();
  for (const line of res.stdout.split('\n')) {
    const m = /^([0-9a-f]{40}) \d+ \d+/.exec(line);
    if (m) byOrigin.set(m[1], (byOrigin.get(m[1]) || 0) + 1);
  }
  return byOrigin;
}

// ---------------------------------------------------------------------------
// Reverts (kept from v1 — attribution to the SUBJECT commit window was already
// correct there).
// ---------------------------------------------------------------------------

function loadRepoReverts(repoPath, sinceIso, untilIso) {
  const REV_SEP = '--REV--';
  const REV_END = '--ENDREV--';
  const args = [
    'log', '--all', '--no-merges',
    `--since=${sinceIso}`, `--until=${untilIso}T23:59:59`,
    '--grep=This reverts commit',
    `--pretty=format:${REV_SEP}%n%H|%ae|%aI|%s%n%b%n${REV_END}`,
  ];
  const res = common.git(repoPath, args);
  if (res.status !== 0) return [];
  const out = [];
  const blocks = res.stdout.split(REV_SEP + '\n').slice(1);
  for (const block of blocks) {
    const endIdx = block.indexOf(REV_END);
    const body = endIdx >= 0 ? block.slice(0, endIdx) : block;
    const lines = body.split(/\r?\n/);
    const header = lines.shift() || '';
    const parts = header.split('|');
    if (parts.length < 4) continue;
    const revertedShas = [];
    const re = /This reverts commit ([0-9a-f]{7,40})/gi;
    let m;
    while ((m = re.exec(lines.join('\n'))) !== null) revertedShas.push(m[1]);
    if (parts[0] && revertedShas.length) {
      out.push({ sha: parts[0], ae: (parts[1] || '').toLowerCase(), date: parts[2] || '', revertedShas });
    }
  }
  return out;
}

// ---------------------------------------------------------------------------
// Registry IO
// ---------------------------------------------------------------------------

function loadDumps(registryDir) {
  return fs.readdirSync(registryDir)
    .filter(f => /^dump-.*\.json$/.test(f))
    .map(f => ({ file: path.join(registryDir, f), data: common.readJson(path.join(registryDir, f)) }))
    .sort((a, b) => String(a.data.range?.from).localeCompare(String(b.data.range?.from)));
}

function loadWindows(registryDir) {
  return fs.readdirSync(registryDir)
    .filter(f => /^window-.*\.json$/.test(f))
    .map(f => ({ file: path.join(registryDir, f), data: common.readJson(path.join(registryDir, f)) }))
    .sort((a, b) => String(a.data.range?.from).localeCompare(String(b.data.range?.from)));
}

function findWindow(windows, iso) {
  const d = isoDate(iso);
  for (const w of windows) {
    if (d >= String(w.data.range.from) && d <= String(w.data.range.to)) return w;
  }
  return null;
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  if (!args.registry) die('missing --registry');
  if (!args.authors) die('missing --authors');
  if (!args.cache) die('missing --cache');
  const registryDir = path.resolve(String(args.registry));
  const cacheDir = path.resolve(String(args.cache));
  const subjectAuthors = new Set(String(args.authors).split(',').map(s => s.trim().toLowerCase()).filter(Boolean));
  const outFile = args.out ? path.resolve(String(args.out)) : path.join(registryDir, 'rework-index.json');
  let blameBudget = Number(args['blame-budget']) || 2000;

  if (!fs.existsSync(registryDir)) die(`registry dir not found: ${registryDir}`);
  const dumps = loadDumps(registryDir);
  const windows = loadWindows(registryDir);
  if (!dumps.length) die(`no dump-*.json in ${registryDir}`);
  const flags = [];

  // Global range: rework horizon only forward (we attribute to origin date).
  let globalFrom = dumps[0].data.range.from;
  let globalTo = dumps[0].data.range.to;
  for (const d of dumps) {
    if (d.data.range.from < globalFrom) globalFrom = d.data.range.from;
    if (d.data.range.to > globalTo) globalTo = d.data.range.to;
  }
  const scanTo = addDaysIso(globalTo, common.REWORK_HORIZON_DAYS);

  // Subject commit index from dumps: `${repo}::${sha}` -> meta.
  // Also: subject cs files (for filtering others' fixes) and per-window added
  // cs lines (ratio denominators).
  const subjectBySha = new Map();
  const subjectFilesByRepo = new Map(); // repoId -> Set<path>
  const addedCsByWindow = new Map();    // window file -> added cs lines
  for (const d of dumps) {
    const w = findWindow(windows, d.data.range.from);
    for (const c of (d.data.commits || [])) {
      const files = [];
      for (const f of (c.files || [])) {
        const p = (typeof f.p === 'number' && Array.isArray(d.data.path_table))
          ? d.data.path_table[f.p] : (f.path || null);
        if (!p || f.kind !== 'cs') continue;
        files.push({ path: p, loc_add: Number(f.loc_add) || 0, loc_del: Number(f.loc_del) || 0, change: f.change });
        if (!subjectFilesByRepo.has(c.repo)) subjectFilesByRepo.set(c.repo, new Set());
        subjectFilesByRepo.get(c.repo).add(p);
        if (w) addedCsByWindow.set(w.file, (addedCsByWindow.get(w.file) || 0) + (Number(f.loc_add) || 0));
      }
      subjectBySha.set(`${c.repo}::${c.sha}`, {
        sha: c.sha, repo: c.repo, date: c.date, cc_type: c.cc_type || 'unclassified',
        subject: c.subject || '', files,
      });
    }
  }

  const repoMap = discoverRepos(cacheDir);
  const dumpRepoIds = new Set([...subjectBySha.values()].map(s => s.repo));

  // Per-window accumulators keyed by window file path.
  const acc = new Map();
  for (const w of windows) {
    acc.set(w.file, {
      churn_lines: 0, self_fix_lines: 0,
      fixed_lines_by_others: 0, fix_commits_by_others: new Set(),
      reverted_by_others: 0,
      links: [],
    });
  }
  const pushLink = (w, link) => {
    const a = acc.get(w.file);
    if (a && a.links.length < EVIDENCE_CAP_PER_WINDOW) a.links.push(link);
  };

  const hubFiles = [];
  let blameOps = 0;

  for (const repoId of dumpRepoIds) {
    const repoPath = repoMap.get(repoId);
    if (!repoPath) {
      console.warn(`WARN: repo not found in cache: ${repoId}`);
      flags.push(`REPO_MISSING:${repoId}`);
      continue;
    }
    if (isShallow(repoPath)) flags.push(`SHALLOW_LIMITED:${repoId}`);

    let scan;
    try {
      scan = loadRepoScan(repoPath, globalFrom, scanTo);
    } catch (e) {
      console.warn(`WARN: log failed for ${repoId}: ${e.message}`);
      continue;
    }

    // Hub detection (cs files): touches > P90 AND authors >= 3 -> EXCLUDED.
    const touchesByFile = new Map();
    for (const c of scan) {
      for (const f of c.files) {
        let t = touchesByFile.get(f);
        if (!t) { t = { touches: 0, authors: new Set() }; touchesByFile.set(f, t); }
        t.touches++;
        t.authors.add(c.ae);
      }
    }
    const counts = [...touchesByFile.values()].map(t => t.touches).sort((a, b) => a - b);
    const p90 = percentile(counts, 0.9);
    const hubSet = new Set();
    for (const [f, t] of touchesByFile.entries()) {
      if (t.touches > p90 && t.authors.size >= 3) {
        hubSet.add(f);
        hubFiles.push({ repo: repoId, path: f, touches: t.touches, authors: t.authors.size });
      }
    }

    const subjectFiles = subjectFilesByRepo.get(repoId) || new Set();

    // Reworker commits:
    //  (a) subject's own commits from dumps (self churn / self fix),
    //  (b) others' fix commits from the scan touching subject files.
    const reworkers = [];
    for (const s of subjectBySha.values()) {
      if (s.repo !== repoId) continue;
      const delFiles = s.files.filter(f => f.loc_del > 0 && f.change !== 'A' && !hubSet.has(f.path));
      if (delFiles.length) {
        reworkers.push({
          sha: s.sha, date: s.date, bySubject: true,
          isFix: isFixCommit(s.cc_type, s.subject),
          files: delFiles.map(f => f.path),
        });
      }
    }
    for (const c of scan) {
      if (subjectAuthors.has(c.ae)) continue;
      if (!isFixText(c.subject)) continue;
      const relevant = c.files.filter(f => subjectFiles.has(f) && !hubSet.has(f));
      if (relevant.length) {
        reworkers.push({ sha: c.sha, date: c.date, bySubject: false, isFix: true, files: relevant });
      }
    }

    // Prefetch all in-range versions of the files we are about to blame —
    // blame on a blobless clone would otherwise lazy-fetch every historical
    // version one by one (2026-07-17 fix).
    const blameFiles = [...new Set(reworkers.flatMap(rw => rw.files))];
    const pf = common.prefetchFileHistoryBlobs(repoPath, blameFiles, addDaysIso(globalFrom, -90));
    if (pf.fetched > 0) console.log(`  prefetched ${pf.fetched}/${pf.needed} blame blobs for ${repoId}`);

    // Blame pass: for each reworker commit and file, whose recent lines died?
    for (const rw of reworkers) {
      for (const f of rw.files) {
        if (blameOps >= blameBudget) break;
        const ranges = deletedRanges(repoPath, rw.sha, f);
        if (!ranges.length) continue;
        blameOps++;
        const byOrigin = blameDeletedLines(repoPath, rw.sha, f, ranges);
        for (const [originSha, lineCount] of byOrigin.entries()) {
          if (originSha === rw.sha) continue;
          const origin = subjectBySha.get(`${repoId}::${originSha}`);
          if (!origin) continue; // deleted lines belong to someone else / older code
          const age = daysBetweenIso(isoDate(origin.date), isoDate(rw.date));
          if (age < 0 || age > common.REWORK_HORIZON_DAYS) continue;
          // Attribute to the ORIGIN window (plan v2 §3.2C).
          const w = findWindow(windows, origin.date);
          if (!w) continue;
          const a = acc.get(w.file);
          if (!a) continue;
          if (rw.bySubject) {
            if (rw.isFix) a.self_fix_lines += lineCount;
            else a.churn_lines += lineCount;
          } else {
            a.fixed_lines_by_others += lineCount;
            a.fix_commits_by_others.add(rw.sha);
          }
          pushLink(w, {
            kind: rw.bySubject ? (rw.isFix ? 'self_fix' : 'self_churn') : 'fixed_by_others',
            origin_sha: originSha, origin_date: origin.date,
            rework_sha: rw.sha, rework_date: rw.date,
            repo: repoId, file: f, lines: lineCount, age_days: age,
          });
        }
      }
      if (blameOps >= blameBudget) break;
    }
    if (blameOps >= blameBudget) flags.push('BLAME_BUDGET_EXHAUSTED');

    // m16: reverts of subject commits by others (origin-window attribution).
    for (const r of loadRepoReverts(repoPath, globalFrom, scanTo)) {
      if (subjectAuthors.has(r.ae)) continue;
      for (const rs of r.revertedShas) {
        for (const [key, s] of subjectBySha.entries()) {
          if (s.repo !== repoId) continue;
          if (!(s.sha.startsWith(rs) || rs.startsWith(s.sha))) continue;
          const w = findWindow(windows, s.date);
          if (!w) continue;
          acc.get(w.file).reverted_by_others++;
          pushLink(w, {
            kind: 'reverted_by_others', origin_sha: s.sha, origin_date: s.date,
            rework_sha: r.sha, rework_date: r.date, repo: repoId, file: '', lines: 0,
            age_days: daysBetweenIso(isoDate(s.date), isoDate(r.date)),
          });
        }
      }
    }
  }

  // Write windows.
  const round3 = n => Math.round(n * 1000) / 1000;
  for (const w of windows) {
    const a = acc.get(w.file);
    const added = addedCsByWindow.get(w.file) || 0;
    const win = w.data;
    win.metrics = win.metrics || {};
    win.metrics.m8 = {
      added_cs_lines: added,
      churn_lines: a.churn_lines,
      self_fix_lines: a.self_fix_lines,
      churn_ratio: added ? round3(a.churn_lines / added) : 0,
      self_fix_ratio: added ? round3(a.self_fix_lines / added) : 0,
    };
    win.metrics.m9 = {
      added_cs_lines: added,
      fixed_lines_by_others: a.fixed_lines_by_others,
      fix_commits_by_others: a.fix_commits_by_others.size,
      ratio: added ? round3(a.fixed_lines_by_others / added) : 0,
    };
    win.metrics.m16 = win.metrics.m16 || {};
    win.metrics.m16.reverted_by_others = a.reverted_by_others;
    if (typeof win.metrics.m16.self_reverts !== 'number') win.metrics.m16.self_reverts = 0;
    win.rework_links = a.links;
    common.writeJson(w.file, win);
  }

  common.writeJson(outFile, {
    schema_version: common.SCHEMA_VERSION,
    engine: 'blame-lines-v2',
    horizon_days: common.REWORK_HORIZON_DAYS,
    blame_ops: blameOps,
    hub_files_excluded: hubFiles,
    flags,
  });
  console.log(`wrote ${outFile} (blame_ops=${blameOps}, hubs_excluded=${hubFiles.length}, flags=${flags.length})`);
  console.log(`updated ${windows.length} window files`);
}

if (require.main === module) {
  try { main(); } catch (e) { die(`reworkIndex.js: ${e.stack || e.message}`); }
}
