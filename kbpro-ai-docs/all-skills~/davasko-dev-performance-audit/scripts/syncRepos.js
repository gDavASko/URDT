#!/usr/bin/env node
'use strict';
// syncRepos.js — clone/fetch confirmed repos into the local cache outside
// Assets/. Plan v2 §3.1: BLOBLESS PARTIAL bare clones (`--filter=blob:none`) —
// full commit/tree history without file contents (~50-300 MB instead of 5-8 GB
// for Unity repos); blobs are lazily fetched by git when diff/blame needs them.
// Pre-existing FULL clones are detected and re-created as partial with
// --recreate-full (otherwise a warning is printed and the full clone is kept).
//
// Usage:
//   node syncRepos.js --projects <discovery.json|url1,url2,...> [--cache <dir>] [--recreate-full] [--out file.json]
//   node syncRepos.js --help

const fs = require('fs');
const path = require('path');
const common = require('./lib/common.js');

const HELP = `syncRepos.js — clone/fetch GitLab repos into the local perf-audit cache

Usage:
  node syncRepos.js --projects <discovery.json|clone-url1,clone-url2,...> [options]

Options:
  --projects <arg>       Either a path to a discoverRepos.js JSON output file,
                           or a comma-separated list of clone URLs (required).
  --cache <dir>           Cache root directory (default: %PERF_AUDIT_CACHE%).
                           MUST NOT be on drive C: (hard user requirement).
  --recreate-full         Re-clone as blobless partial any cached repo that was
                           previously cloned with full blobs (frees disk space).
  --out <file>            Write JSON summary to this file (in addition to stdout).
  --help                  Print this message and exit 0.

All clones are bare + '--filter=blob:none' (blobless partial). Requires git >= 2.27.

Auth: GITLAB_TOKEN (if set) is embedded into the clone/fetch URL as
oauth2:<token>@host for the duration of the git command only; it is NEVER
written to disk and is stripped from any logged/echoed strings.
Clones live under the cache dir, never under Assets/.
`;

function printHelp() {
  console.log(HELP);
}

function dirNameFor(pathWithNamespace) {
  return pathWithNamespace.replace(/\//g, '_');
}

// Redact any embedded credential (user:pass@ or token@) from a URL for logging.
function redact(url) {
  return String(url).replace(/:\/\/[^@/]*@/, '://***@');
}

function gitAuthArgs(token) {
  if (!token) return [];
  const basic = Buffer.from(`oauth2:${token}`, 'utf8').toString('base64');
  return ['-c', `http.extraHeader=Authorization: Basic ${basic}`];
}

function loadProjectsArg(arg) {
  // Path to a discoverRepos.js JSON file?
  if (fs.existsSync(arg)) {
    const data = common.readJson(arg);
    const list = Array.isArray(data.projects) ? data.projects : [];
    return list.map((p) => ({
      id: p.path_with_namespace || p.id,
      url: p.http_url_to_repo,
    }));
  }
  // Comma-separated clone URLs.
  return arg.split(',').map((s) => s.trim()).filter(Boolean).map((url) => {
    const m = url.match(/([^/:]+\/[^/]+?)(\.git)?$/);
    return { id: m ? m[1] : url, url };
  });
}

function isPartialClone(dir) {
  const res = common.run('git', ['-C', dir, 'config', '--get', 'remote.origin.partialclonefilter']);
  return res.status === 0 && /blob:none/.test(res.stdout || '');
}

function syncOne(entry, cacheDir, opts) {
  const { id, url } = entry;
  const dir = path.join(cacheDir, dirNameFor(id));
  const token = common.gitlabToken();

  const result = { id, dir, partial: false, ok: false, error: null };

  try {
    const gitRun = (args) => common.run('git', [
      '-c', 'http.version=HTTP/1.1',
      '-c', 'http.lowSpeedLimit=0',
      '-c', 'http.lowSpeedTime=999999',
      ...gitAuthArgs(token),
      ...args,
    ]);
    const safeRemoveIncomplete = () => {
      const resolvedDir = path.resolve(dir);
      const resolvedCache = path.resolve(cacheDir);
      if (!resolvedDir.startsWith(resolvedCache + path.sep)) {
        throw new Error(`refusing to clean path outside cache: ${resolvedDir}`);
      }
      if (fs.existsSync(resolvedDir) && !fs.existsSync(path.join(resolvedDir, 'HEAD'))) {
        fs.rmSync(resolvedDir, { recursive: true, force: true });
      }
    };
    const retry = (label, fn) => {
      let last = null;
      for (let attempt = 1; attempt <= 3; attempt++) {
        if (label === 'clone') safeRemoveIncomplete();
        const res = fn();
        if (res.status === 0) return res;
        last = res;
        console.log(`   retry ${attempt}/3 failed for ${label}: ${redact((res.stderr || '').split(/\r?\n/).slice(-2).join(' '))}`);
      }
      return last;
    };

    let exists = fs.existsSync(dir) && fs.existsSync(path.join(dir, 'HEAD'));

    // Full clone found where a partial is wanted: re-create only with --recreate-full.
    if (exists && !isPartialClone(dir)) {
      if (opts.recreateFull) {
        console.log(`   full clone detected, re-creating as blobless partial: ${dir}`);
        fs.rmSync(dir, { recursive: true, force: true });
        exists = false;
      } else {
        console.log(`   WARN: ${id} is a FULL clone (heavy). Re-run with --recreate-full to convert to blobless partial.`);
      }
    }

    if (!exists) {
      // Fresh blobless partial bare clone (plan v2 §3.1).
      common.ensureDir(path.dirname(dir));
      const args = ['clone', '--bare', '--filter=blob:none', url, dir];
      const res = retry('clone', () => gitRun(args));
      if (res.status !== 0) {
        throw new Error(redact(res.stderr || 'git clone failed'));
      }
      result.partial = true;
      result.ok = true;
    } else {
      // Existing clone: fetch --all --prune.
      const fetchUrlRes = common.run('git', ['-C', dir, 'remote', 'set-url', 'origin', url]);
      if (fetchUrlRes.status !== 0) {
        throw new Error(redact(fetchUrlRes.stderr || 'git remote set-url failed'));
      }
      const fetchRes = retry('fetch', () => gitRun(['-C', dir, 'fetch', '--all', '--prune']));
      if (fetchRes.status !== 0) {
        throw new Error(redact(fetchRes.stderr || 'git fetch failed'));
      }

      // A leftover shallow clone (legacy v1 fallback) truncates history and
      // breaks blame attribution — unshallow it (tolerate failure, flag it).
      const shallowFlagPath = path.join(dir, 'shallow');
      if (fs.existsSync(shallowFlagPath)) {
        const unshallowRes = gitRun(['-C', dir, 'fetch', '--unshallow', '--filter=blob:none']);
        if (unshallowRes.status !== 0 && fs.existsSync(shallowFlagPath)) {
          result.shallow_leftover = true;
        }
      }

      result.partial = isPartialClone(dir);
      result.ok = true;

      // Ensure the stored remote URL is clean.
      common.run('git', ['-C', dir, 'remote', 'set-url', 'origin', url]);
    }

    // Cache-repo hygiene (2026-07-17): background gc/repack competes with
    // analysis passes, and a stalled lazy fetch must time out instead of
    // hanging the pipeline forever.
    for (const [k, v] of [['gc.auto', '0'], ['maintenance.auto', 'false'],
      ['http.lowSpeedLimit', '1000'], ['http.lowSpeedTime', '60']]) {
      common.run('git', ['-C', dir, 'config', k, v]);
    }
  } catch (e) {
    result.error = e.message;
    result.ok = false;
  }

  return result;
}

async function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) {
    printHelp();
    process.exit(0);
  }

  if (typeof args.projects !== 'string') {
    console.error('syncRepos.js: missing required arg --projects <discovery.json|url1,url2,...>');
    printHelp();
    process.exit(1);
  }

  const cacheDir = typeof args.cache === 'string' ? path.resolve(args.cache) : common.cacheRoot();
  common.assertNotDriveC(cacheDir, 'cache');
  common.ensureDir(cacheDir);

  let entries;
  try {
    entries = loadProjectsArg(args.projects);
  } catch (e) {
    console.error(`syncRepos.js: failed to read --projects: ${e.message}`);
    process.exit(1);
  }

  if (entries.length === 0) {
    console.error('syncRepos.js: no projects to sync (empty --projects input).');
    process.exit(1);
  }

  console.log(`syncRepos.js: syncing ${entries.length} repo(s) into ${cacheDir}`);

  const repos = [];
  for (const entry of entries) {
    console.log(`syncRepos.js: -> ${entry.id} (${redact(entry.url)})`);
    const result = syncOne(entry, cacheDir, { recreateFull: Boolean(args['recreate-full']) });
    repos.push(result);
    if (result.ok) {
      console.log(`   ok (partial=${result.partial})`);
    } else {
      console.log(`   FAILED: ${result.error}`);
    }
  }

  const summary = { schema_version: common.SCHEMA_VERSION, repos };
  console.log('');
  console.log(JSON.stringify(summary));

  if (typeof args.out === 'string') {
    common.writeJsonPretty(args.out, summary);
    console.log(`syncRepos.js: summary written to ${args.out}`);
  }

  const anyFailed = repos.some((r) => !r.ok);
  process.exit(anyFailed ? 1 : 0);
}

main().catch((e) => {
  console.error(`syncRepos.js fatal error: ${e.message}`);
  process.exit(1);
});
