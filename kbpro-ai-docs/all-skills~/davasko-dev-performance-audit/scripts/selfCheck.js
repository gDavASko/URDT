#!/usr/bin/env node
'use strict';
// selfCheck.js — preflight for davasko-dev-performance-audit v2.
// Runs 6 check groups (env, gitlab, git, agents, judge, cache), prints
// [PASS]/[WARN]/[FAIL] lines and writes selfcheck-report.json next to the cache.
// v2 additions: drive-C prohibition for cache/reports/temp (plan v2 §3.5),
// git >= 2.27 for blobless partial clones (plan v2 §3.1).
//
// Usage:
//   node selfCheck.js [--skip-gitlab] [--probe-repo <path>] [--self-check-only]
//   node selfCheck.js --help

const fs = require('fs');
const path = require('path');
const os = require('os');
const common = require('./lib/common.js');

const HELP = `selfCheck.js — preflight checks for davasko-dev-performance-audit

Usage:
  node selfCheck.js [options]

Options:
  --probe-repo <path>   Local git repo to smoke-test log/patch-id/blame
                         (default: current working directory's project root,
                         falls back to this skill's ai-docs root).
  --skip-gitlab          Skip live GitLab API calls; gitlab group becomes WARN.
  --self-check-only       Alias with identical behavior (explicit diagnostic run).
  --help                  Print this message and exit 0.

Exit code: 1 if any FAIL in groups env/gitlab/git, else 0.
Report written to: <cache>/selfcheck-report.json (cache = %PERF_AUDIT_CACHE%).
`;

function printHelp() {
  console.log(HELP);
}

function fmtBytes(n) {
  if (!Number.isFinite(n)) return 'unknown';
  const units = ['B', 'KB', 'MB', 'GB', 'TB'];
  let i = 0;
  let v = n;
  while (v >= 1024 && i < units.length - 1) {
    v /= 1024;
    i++;
  }
  return `${v.toFixed(1)} ${units[i]}`;
}

// ---------------------------------------------------------------------------
// Group: env
// ---------------------------------------------------------------------------

function checkEnv(checks) {
  // git presence/version.
  try {
    const res = common.run('git', ['--version']);
    if (res.status === 0) {
      checks.push(common.check('env:git', 'PASS', res.stdout.trim()));
    } else {
      checks.push(common.check('env:git', 'FAIL', `git --version exited ${res.status}: ${res.stderr}`));
    }
  } catch (e) {
    checks.push(common.check('env:git', 'FAIL', `git not found — install git and ensure it is on PATH (${e.message})`));
  }

  // node version.
  const nodeVersion = process.version;
  const major = parseInt(nodeVersion.slice(1).split('.')[0], 10);
  if (major >= 18) {
    checks.push(common.check('env:node', 'PASS', nodeVersion));
  } else {
    checks.push(common.check('env:node', 'FAIL', `${nodeVersion} — need Node >= 18`));
  }

  // Sibling scripts respond to --help with exit 0.
  const scriptsDir = path.join(common.SKILL_ROOT, 'scripts');
  let siblingScripts = [];
  try {
    siblingScripts = fs.readdirSync(scriptsDir).filter((f) => f.endsWith('.js') && f !== 'selfCheck.js');
  } catch (e) {
    checks.push(common.check('env:scripts-dir', 'FAIL', `cannot read ${scriptsDir}: ${e.message}`));
  }
  for (const f of siblingScripts) {
    const full = path.join(scriptsDir, f);
    try {
      const res = common.run(process.execPath, [full, '--help']);
      if (res.status === 0) {
        checks.push(common.check(`env:script-help:${f}`, 'PASS', 'exit 0'));
      } else {
        checks.push(common.check(`env:script-help:${f}`, 'FAIL', `--help exited ${res.status}: ${(res.stderr || '').slice(0, 200)}`));
      }
    } catch (e) {
      checks.push(common.check(`env:script-help:${f}`, 'FAIL', `could not execute: ${e.message}`));
    }
  }
  if (siblingScripts.length === 0) {
    checks.push(common.check('env:script-help', 'WARN', 'no sibling scripts found to probe yet'));
  }

  // git >= 2.27 required for --filter=blob:none partial clones (plan v2 §3.1).
  try {
    const res = common.run('git', ['--version']);
    const m = /(\d+)\.(\d+)/.exec(res.stdout || '');
    if (m && (Number(m[1]) > 2 || (Number(m[1]) === 2 && Number(m[2]) >= 27))) {
      checks.push(common.check('env:git-partial-clone', 'PASS', `git ${m[1]}.${m[2]} supports --filter=blob:none`));
    } else {
      checks.push(common.check('env:git-partial-clone', 'FAIL', 'git >= 2.27 required for blobless partial clones'));
    }
  } catch (e) {
    checks.push(common.check('env:git-partial-clone', 'FAIL', e.message));
  }

  // Cache and reports roots: must be SET, writable, and NOT on drive C
  // (plan v2 §3.5 — drive C is overloaded, hard user requirement).
  const roots = [];
  try { roots.push(['env:write-cache', common.cacheRoot()]); }
  catch (e) { checks.push(common.check('env:write-cache', 'FAIL', e.message)); }
  try { roots.push(['env:write-reports', common.reportsRoot()]); }
  catch (e) { checks.push(common.check('env:write-reports', 'FAIL', e.message)); }
  for (const [name, dir] of roots) {
    try {
      common.assertNotDriveC(dir, name);
      common.ensureDir(dir);
      const probe = path.join(dir, `.selfcheck-write-probe-${process.pid}.tmp`);
      fs.writeFileSync(probe, 'ok');
      fs.unlinkSync(probe);
      checks.push(common.check(name, 'PASS', dir));
    } catch (e) {
      checks.push(common.check(name, 'FAIL', `${dir}: ${e.message}`));
    }
  }

  // Free disk estimate on the CACHE drive (best-effort).
  try {
    const cacheDrive = roots.length ? path.parse(roots[0][1]).root.replace(/[:\\]/g, '') : null;
    if (!cacheDrive) throw new Error('cache root unknown');
    const res = common.run('powershell.exe', [
      '-NoProfile', '-NonInteractive', '-Command',
      `(Get-PSDrive -Name ${cacheDrive}).Free`,
    ]);
    if (res.status === 0 && res.stdout.trim()) {
      const free = Number(res.stdout.trim());
      if (Number.isFinite(free)) {
        const status = free < 5 * 1024 * 1024 * 1024 ? 'WARN' : 'PASS';
        checks.push(common.check('env:disk-free', status, `~${fmtBytes(free)} free on ${cacheDrive}: (partial clones need ~0.3 GB/repo)`));
      } else {
        checks.push(common.check('env:disk-free', 'WARN', 'could not parse free-space output (best-effort, non-blocking)'));
      }
    } else {
      checks.push(common.check('env:disk-free', 'WARN', 'could not query free disk space (best-effort, non-blocking)'));
    }
  } catch (e) {
    checks.push(common.check('env:disk-free', 'WARN', `disk estimate unavailable (best-effort): ${e.message}`));
  }
}

// ---------------------------------------------------------------------------
// Group: gitlab
// ---------------------------------------------------------------------------

async function checkGitlab(checks, opts) {
  if (opts.skipGitlab) {
    checks.push(common.check('gitlab', 'WARN', 'skipped (--skip-gitlab)'));
    return;
  }

  const token = common.gitlabToken();
  if (!token) {
    checks.push(common.check('gitlab:token', 'FAIL', 'GITLAB_TOKEN is not set — export GITLAB_TOKEN=<personal access token with read_api, read_repository> or pass --skip-gitlab to defer this check'));
    return;
  }
  checks.push(common.check('gitlab:token', 'PASS', 'GITLAB_TOKEN is set'));

  try {
    const { status, body } = await common.gitlabGet('/version');
    if (status === 200 && body && body.version) {
      checks.push(common.check('gitlab:version', 'PASS', `GitLab ${body.version}`));
    } else {
      checks.push(common.check('gitlab:version', 'FAIL', `GET /version -> HTTP ${status}: ${JSON.stringify(body).slice(0, 200)}`));
    }
  } catch (e) {
    checks.push(common.check('gitlab:version', 'FAIL', `GET /version failed: ${e.message} — check GITLAB_URL/network`));
  }

  try {
    const { status, body } = await common.gitlabGet('/user');
    if (status === 200 && body && body.username) {
      checks.push(common.check('gitlab:user', 'PASS', `authenticated as ${body.username}`));
    } else {
      checks.push(common.check('gitlab:user', 'FAIL', `GET /user -> HTTP ${status} — token invalid or expired`));
    }
  } catch (e) {
    checks.push(common.check('gitlab:user', 'FAIL', `GET /user failed: ${e.message}`));
  }

  // Token scopes: best-effort, tolerate 404 (some GitLab versions/tiers hide this endpoint).
  try {
    const { status, body } = await common.gitlabGet('/personal_access_tokens/self');
    if (status === 200 && body && Array.isArray(body.scopes)) {
      const needed = ['read_api', 'read_repository'];
      const missing = needed.filter((s) => !body.scopes.includes(s) && !body.scopes.includes('api'));
      if (missing.length === 0) {
        checks.push(common.check('gitlab:scopes', 'PASS', `scopes: ${body.scopes.join(', ')}`));
      } else {
        checks.push(common.check('gitlab:scopes', 'WARN', `missing scopes ${missing.join(', ')} (have: ${body.scopes.join(', ')})`));
      }
    } else if (status === 404) {
      checks.push(common.check('gitlab:scopes', 'WARN', 'token introspection endpoint not available (404) — scopes not verified'));
    } else {
      checks.push(common.check('gitlab:scopes', 'WARN', `could not read token scopes (HTTP ${status})`));
    }
  } catch (e) {
    checks.push(common.check('gitlab:scopes', 'WARN', `token scope probe failed: ${e.message}`));
  }

  // Visible projects count (membership=true, first page total).
  try {
    const { status, headers, body } = await common.gitlabGet('/projects', { params: { membership: true, per_page: 1 } });
    if (status === 200) {
      const total = headers.get('x-total') || (Array.isArray(body) ? String(body.length) : 'unknown');
      checks.push(common.check('gitlab:projects-visible', 'PASS', `${total} project(s) visible via membership=true`));
    } else {
      checks.push(common.check('gitlab:projects-visible', 'FAIL', `GET /projects -> HTTP ${status}`));
    }
  } catch (e) {
    checks.push(common.check('gitlab:projects-visible', 'FAIL', `GET /projects failed: ${e.message}`));
  }
}

// ---------------------------------------------------------------------------
// Group: git (local repo smoke test)
// ---------------------------------------------------------------------------

function checkGit(checks, probeRepo) {
  if (!fs.existsSync(probeRepo)) {
    checks.push(common.check('git:probe-repo', 'FAIL', `--probe-repo path does not exist: ${probeRepo}`));
    return;
  }
  const gitDirRes = common.git(probeRepo, ['rev-parse', '--git-dir']);
  if (gitDirRes.status !== 0) {
    checks.push(common.check('git:probe-repo', 'FAIL', `${probeRepo} is not a git repository: ${gitDirRes.stderr.trim()}`));
    return;
  }
  checks.push(common.check('git:probe-repo', 'PASS', probeRepo));

  // log --numstat --find-renames -1
  let headSha = '';
  try {
    const res = common.git(probeRepo, ['log', '--numstat', '--find-renames', '-1']);
    if (res.status === 0 && res.stdout.trim().length > 0) {
      checks.push(common.check('git:log-numstat', 'PASS', 'parsed 1 commit with --numstat --find-renames'));
    } else {
      checks.push(common.check('git:log-numstat', 'FAIL', `git log --numstat --find-renames -1 failed: ${res.stderr.trim()}`));
    }
    const shaRes = common.git(probeRepo, ['rev-parse', 'HEAD']);
    if (shaRes.status === 0) headSha = shaRes.stdout.trim();
  } catch (e) {
    checks.push(common.check('git:log-numstat', 'FAIL', e.message));
  }

  // patch-id on HEAD
  try {
    if (!headSha) throw new Error('no HEAD sha resolved');
    const showRes = common.git(probeRepo, ['show', headSha]);
    if (showRes.status !== 0) throw new Error(`git show HEAD failed: ${showRes.stderr.trim()}`);
    const patchIdRes = common.run('git', ['patch-id'], { input: showRes.stdout, cwd: probeRepo, encoding: 'utf8' });
    if (patchIdRes.status === 0 && patchIdRes.stdout.trim()) {
      checks.push(common.check('git:patch-id', 'PASS', patchIdRes.stdout.trim().split(/\s+/)[0].slice(0, 12)));
    } else {
      // A patch-id can legitimately be empty for a merge/empty commit; only WARN.
      checks.push(common.check('git:patch-id', 'WARN', 'patch-id produced no output for HEAD (may be an empty/merge commit)'));
    }
  } catch (e) {
    checks.push(common.check('git:patch-id', 'FAIL', e.message));
  }

  // blame on one tracked file
  try {
    const lsRes = common.git(probeRepo, ['ls-files']);
    if (lsRes.status !== 0 || !lsRes.stdout.trim()) throw new Error('git ls-files returned no tracked files');
    const firstFile = lsRes.stdout.split(/\r?\n/).find((f) => f.trim().length > 0);
    if (!firstFile) throw new Error('no trackable file found');
    const blameRes = common.git(probeRepo, ['blame', '--line-porcelain', firstFile]);
    if (blameRes.status === 0) {
      checks.push(common.check('git:blame', 'PASS', `blamed ${firstFile}`));
    } else {
      checks.push(common.check('git:blame', 'FAIL', `git blame ${firstFile} failed: ${blameRes.stderr.trim()}`));
    }
  } catch (e) {
    checks.push(common.check('git:blame', 'FAIL', e.message));
  }
}

// ---------------------------------------------------------------------------
// Group: agents/judge — cannot probe LLM tools from Node.
// ---------------------------------------------------------------------------

function checkAgentsJudge(checks) {
  checks.push(common.check('agents', 'WARN', 'verified by orchestrator at runtime'));
  checks.push(common.check('judge', 'WARN', 'verified by orchestrator at runtime'));
}

// ---------------------------------------------------------------------------
// Group: cache — validate JSON parse of any existing registry files.
// ---------------------------------------------------------------------------

function checkCache(checks) {
  let reportsRoot;
  try { reportsRoot = common.reportsRoot(); }
  catch (_) {
    checks.push(common.check('cache:registry', 'WARN', 'PERF_AUDIT_REPORTS not set — nothing to validate yet'));
    return;
  }
  if (!fs.existsSync(reportsRoot)) {
    checks.push(common.check('cache:registry', 'PASS', 'no reports yet — nothing to validate'));
    return;
  }
  const corrupt = [];
  let scanned = 0;

  function walkAuthors(root) {
    let authors = [];
    try {
      authors = fs.readdirSync(root, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);
    } catch (_) {
      return;
    }
    for (const author of authors) {
      const authorDir = path.join(root, author);
      let runs = [];
      try {
        runs = fs.readdirSync(authorDir, { withFileTypes: true }).filter((d) => d.isDirectory()).map((d) => d.name);
      } catch (_) {
        continue;
      }
      for (const run of runs) {
        const registryDir = path.join(authorDir, run, 'registry');
        if (!fs.existsSync(registryDir)) continue;
        let files = [];
        try {
          files = fs.readdirSync(registryDir).filter((f) => f.endsWith('.json'));
        } catch (_) {
          continue;
        }
        for (const f of files) {
          scanned++;
          const full = path.join(registryDir, f);
          try {
            JSON.parse(fs.readFileSync(full, 'utf8'));
          } catch (e) {
            corrupt.push(full);
          }
        }
      }
    }
  }
  walkAuthors(reportsRoot);

  if (corrupt.length === 0) {
    checks.push(common.check('cache:registry', 'PASS', `${scanned} registry file(s) validated, 0 corrupt`));
  } else {
    checks.push(common.check('cache:registry', 'WARN', `corrupt JSON in: ${corrupt.join(', ')}`));
  }
}

// ---------------------------------------------------------------------------
// Main
// ---------------------------------------------------------------------------

async function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) {
    printHelp();
    process.exit(0);
  }

  const selfCheckOnly = Boolean(args['self-check-only']) || Boolean(args['self-check']);
  const skipGitlab = Boolean(args['skip-gitlab']);
  const probeRepo = typeof args['probe-repo'] === 'string'
    ? path.resolve(args['probe-repo'])
    : path.resolve(process.cwd());

  const checks = [];

  checkEnv(checks);
  await checkGitlab(checks, { skipGitlab });
  checkGit(checks, probeRepo);
  checkAgentsJudge(checks);
  checkCache(checks);

  const groupOf = (label) => label.split(':')[0];
  const blockingGroups = new Set(['env', 'gitlab', 'git']);
  const hasBlockingFail = checks.some((c) => c.status === 'FAIL' && blockingGroups.has(groupOf(c.label)));

  const verdict = hasBlockingFail
    ? 'FAIL'
    : (checks.some((c) => c.status === 'FAIL' || c.status === 'WARN') ? 'WARN' : 'PASS');

  const report = {
    schema_version: common.SCHEMA_VERSION,
    checked_at: new Date().toISOString(),
    verdict,
    checks: checks.map((c) => ({ group: groupOf(c.label), label: c.label, status: c.status, detail: c.detail })),
  };

  console.log('');
  console.log(`selfCheck verdict: ${verdict} (${selfCheckOnly ? 'explicit --self-check-only run' : 'standard run'})`);
  try {
    const reportFile = common.selfCheckReportPath();
    common.writeJsonPretty(reportFile, report);
    console.log(`report written to: ${reportFile}`);
  } catch (e) {
    console.log(`report NOT written (cache path unavailable): ${e.message}`);
  }

  process.exit(hasBlockingFail ? 1 : 0);
}

main().catch((e) => {
  console.error(`selfCheck.js fatal error: ${e.message}`);
  process.exit(1);
});
