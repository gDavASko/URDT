#!/usr/bin/env node
'use strict';
// runFixtureTest.js — machine judge for the v2 rework engine (plan v2 §7.1).
//
// Builds a synthetic git repo with KNOWN rework facts, runs the real pipeline
// (dumpWindow -> computeMetrics -> reworkIndex) and asserts exact numbers:
//   - iterative development WITHOUT fixes  -> churn, NOT self-fix rework;
//   - own fix commits rewriting own lines  -> m8.self_fix_lines;
//   - others' fix commits on subject lines -> m9.fixed_lines_by_others;
//   - hub files (3+ authors, hot)          -> fully excluded;
//   - revert by another author             -> m16.reverted_by_others,
//     attributed to the SUBJECT commit's window.
//
// Usage: node scripts/test/runFixtureTest.js [--work <dir>] [--keep]
// Exit 0 = all assertions pass; 1 = any failure.

const fs = require('fs');
const os = require('os');
const path = require('path');
const { spawnSync } = require('child_process');

const SCRIPTS = path.resolve(__dirname, '..');
const SUBJECT = 'subject@test.com';

function sh(cwd, cmd, args, env) {
  const res = spawnSync(cmd, args, {
    cwd, encoding: 'utf8', windowsHide: true,
    env: { ...process.env, ...env },
  });
  if (res.status !== 0) {
    throw new Error(`${cmd} ${args.join(' ')} failed:\n${res.stderr || res.stdout}`);
  }
  return res.stdout;
}

function writeFileLines(repoDir, rel, lines) {
  const full = path.join(repoDir, rel);
  fs.mkdirSync(path.dirname(full), { recursive: true });
  fs.writeFileSync(full, lines.join('\n') + '\n');
}

function commit(repoDir, { name, email, date, message }) {
  const env = {
    GIT_AUTHOR_NAME: name, GIT_AUTHOR_EMAIL: email, GIT_AUTHOR_DATE: `${date}T12:00:00+03:00`,
    GIT_COMMITTER_NAME: name, GIT_COMMITTER_EMAIL: email, GIT_COMMITTER_DATE: `${date}T12:00:00+03:00`,
  };
  sh(repoDir, 'git', ['add', '-A'], env);
  sh(repoDir, 'git', ['commit', '-m', message], env);
  return sh(repoDir, 'git', ['rev-parse', 'HEAD']).trim();
}

function fooLines(overrides) {
  const lines = [];
  for (let i = 1; i <= 10; i++) lines.push(overrides[i] || `int value${i} = ${i};`);
  return lines;
}

function main() {
  const keep = process.argv.includes('--keep');
  const workArgIdx = process.argv.indexOf('--work');
  const work = workArgIdx >= 0
    ? path.resolve(process.argv[workArgIdx + 1])
    : path.join(os.tmpdir(), `perf-audit-fixture-${Date.now()}`);
  const srcRepo = path.join(work, 'src', 'fixture');
  const cache = path.join(work, 'cache');
  const registry = path.join(work, 'registry');
  fs.mkdirSync(srcRepo, { recursive: true });
  fs.mkdirSync(cache, { recursive: true });
  fs.mkdirSync(registry, { recursive: true });

  console.log(`fixture work dir: ${work}`);

  // --- Build the synthetic history -----------------------------------------
  sh(srcRepo, 'git', ['init', '-q', '-b', 'main']);

  const subj = { name: 'Subject', email: SUBJECT };
  const other1 = { name: 'OtherOne', email: 'other1@test.com' };
  const other2 = { name: 'OtherTwo', email: 'other2@test.com' };

  // A (subject, feat): Foo.cs born with 10 lines.
  writeFileLines(srcRepo, 'Assets/Foo.cs', fooLines({}));
  const shaA = commit(srcRepo, { ...subj, date: '2025-01-10', message: 'feat: add foo' });

  // H1 (subject): Hub.cs born (hub file — will be touched by 3 authors, 6 times).
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1();', 'hub2();', 'hub3();', 'hub4();', 'hub5();']);
  commit(srcRepo, { ...subj, date: '2025-01-10', message: 'feat: add hub' });

  // B (subject, feat — ITERATION, not a fix): rewrites Foo lines 3-6 -> churn 4.
  writeFileLines(srcRepo, 'Assets/Foo.cs', fooLines({
    3: 'int redone3 = 30;', 4: 'int redone4 = 40;', 5: 'int redone5 = 50;', 6: 'int redone6 = 60;',
  }));
  commit(srcRepo, { ...subj, date: '2025-01-11', message: 'feat: continue foo' });

  // C (subject, FIX): rewrites Foo lines 1-2 (authored by A) -> self_fix 2.
  writeFileLines(srcRepo, 'Assets/Foo.cs', fooLines({
    1: 'int fixed1 = 100;', 2: 'int fixed2 = 200;',
    3: 'int redone3 = 30;', 4: 'int redone4 = 40;', 5: 'int redone5 = 50;', 6: 'int redone6 = 60;',
  }));
  commit(srcRepo, { ...subj, date: '2025-01-13', message: 'fix: correct foo calculation' });

  // Hub churn by others: other1 x3 (one is a "fix" on subject's hub lines —
  // must NOT count: hub is excluded), other2 x2. Touches: 1+3+2 = 6, authors 3.
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1();', 'hub2();', 'hub3();', 'hub4();', 'hub5();', 'hub6();']);
  commit(srcRepo, { ...other1, date: '2025-01-12', message: 'feat: extend hub' });
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1fixed();', 'hub2();', 'hub3();', 'hub4();', 'hub5();', 'hub6();']);
  commit(srcRepo, { ...other1, date: '2025-01-14', message: 'fix: hub tweak' });
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1fixed();', 'hub2();', 'hub3();', 'hub4();', 'hub5();', 'hub6();', 'hub7();']);
  commit(srcRepo, { ...other1, date: '2025-01-15', message: 'feat: more hub' });
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1fixed();', 'hub2b();', 'hub3();', 'hub4();', 'hub5();', 'hub6();', 'hub7();']);
  commit(srcRepo, { ...other2, date: '2025-01-15', message: 'chore: hub cleanup' });
  writeFileLines(srcRepo, 'Assets/Hub.cs', ['hub1fixed();', 'hub2b();', 'hub3c();', 'hub4();', 'hub5();', 'hub6();', 'hub7();']);
  commit(srcRepo, { ...other2, date: '2025-01-16', message: 'chore: hub cleanup 2' });

  // D (other1, FIX): rewrites Foo lines 8-10 (authored by A) -> m9 fixed 3.
  writeFileLines(srcRepo, 'Assets/Foo.cs', fooLines({
    1: 'int fixed1 = 100;', 2: 'int fixed2 = 200;',
    3: 'int redone3 = 30;', 4: 'int redone4 = 40;', 5: 'int redone5 = 50;', 6: 'int redone6 = 60;',
    8: 'int otherfix8 = 800;', 9: 'int otherfix9 = 900;', 10: 'int otherfix10 = 1000;',
  }));
  commit(srcRepo, { ...other1, date: '2025-01-15', message: 'fix: foo boundary bug' });

  // R (other2, revert of A): message-level revert -> m16.reverted_by_others = 1.
  writeFileLines(srcRepo, 'Assets/Other.cs', ['void Noop() {}']);
  commit(srcRepo, {
    ...other2, date: '2025-01-20',
    message: `Revert "feat: add foo"\n\nThis reverts commit ${shaA}.`,
  });

  // --- Bare clone into the cache (as syncRepos would produce) --------------
  sh(work, 'git', ['clone', '-q', '--bare', srcRepo, path.join(cache, 'fixture.git')]);

  // --- Run the real pipeline ------------------------------------------------
  const node = process.execPath;
  const runScript = (script, args) => {
    const res = spawnSync(node, [path.join(SCRIPTS, script), ...args],
      { encoding: 'utf8', windowsHide: true });
    if (res.status !== 0) {
      throw new Error(`${script} failed:\n${res.stderr || res.stdout}`);
    }
    return res;
  };

  const dumpFile = path.join(registry, 'dump-P1_W1_2025-01-01.json');
  const windowFile = path.join(registry, 'window-P1_W1_2025-01-01.json');
  runScript('dumpWindow.js', [
    '--from', '2025-01-01', '--to', '2025-01-31',
    '--authors', SUBJECT, '--cache', cache,
    '--window-id', 'P1_W1_2025-01-01', '--phase', 'P1',
    '--out', dumpFile,
  ]);
  runScript('computeMetrics.js', ['--dump', dumpFile, '--out', windowFile]);
  runScript('reworkIndex.js', [
    '--registry', registry, '--authors', SUBJECT, '--cache', cache,
  ]);

  // --- Assertions ------------------------------------------------------------
  const win = JSON.parse(fs.readFileSync(windowFile, 'utf8'));
  const m8 = win.metrics.m8;
  const m9 = win.metrics.m9;
  const m16 = win.metrics.m16;

  let failures = 0;
  const assertEq = (label, actual, expected) => {
    const ok = actual === expected;
    console.log(`[${ok ? 'PASS' : 'FAIL'}] ${label}: expected ${expected}, got ${actual}`);
    if (!ok) failures++;
  };

  // added cs lines by subject: Foo A=10, B=4, C=2, Hub H1=5 -> 21.
  assertEq('m8.added_cs_lines', m8.added_cs_lines, 21);
  // Iteration (feat B rewriting 4 lines) is CHURN, not fix-rework.
  assertEq('m8.churn_lines (iteration != rework)', m8.churn_lines, 4);
  // Own fix C rewriting 2 own recent lines.
  assertEq('m8.self_fix_lines', m8.self_fix_lines, 2);
  // Other's fix D rewriting 3 subject lines; hub fix must NOT add more.
  assertEq('m9.fixed_lines_by_others (hub excluded)', m9.fixed_lines_by_others, 3);
  assertEq('m9.fix_commits_by_others', m9.fix_commits_by_others, 1);
  // Revert by another author, attributed to subject window.
  assertEq('m16.reverted_by_others', m16.reverted_by_others, 1);

  const reworkIndex = JSON.parse(fs.readFileSync(path.join(registry, 'rework-index.json'), 'utf8'));
  const hubPaths = (reworkIndex.hub_files_excluded || []).map(h => h.path);
  assertEq('hub detection (Assets/Hub.cs excluded)', hubPaths.includes('Assets/Hub.cs'), true);
  assertEq('hub detection (Assets/Foo.cs NOT a hub)', hubPaths.includes('Assets/Foo.cs'), false);

  if (!keep && failures === 0) {
    try { fs.rmSync(work, { recursive: true, force: true }); } catch (_) { /* win file locks */ }
  } else {
    console.log(`work dir kept: ${work}`);
  }

  console.log(failures === 0 ? 'FIXTURE TEST: ALL PASS' : `FIXTURE TEST: ${failures} FAILURE(S)`);
  process.exit(failures === 0 ? 0 : 1);
}

try { main(); } catch (e) {
  console.error(`runFixtureTest.js: ${e.stack || e.message}`);
  process.exit(1);
}
