#!/usr/bin/env node
'use strict';
// discoverRepos.js — GitLab discovery of projects an author committed to in a
// date range (plan §5а.0a). Paginates /projects, then for each project pages
// through /repository/commits filtered by since/until, matching author_email
// or author_name against the supplied ids.
//
// Usage:
//   node discoverRepos.js --from YYYY-MM-DD --to YYYY-MM-DD --author <id> [--ids a,b,c] [--out file.json]
//   node discoverRepos.js --help

const common = require('./lib/common.js');

const HELP = `discoverRepos.js — find GitLab projects an author committed to in a period

Usage:
  node discoverRepos.js --from YYYY-MM-DD --to YYYY-MM-DD --author <id> [options]

Options:
  --from <date>   Range start, YYYY-MM-DD (required).
  --to <date>     Range end, YYYY-MM-DD (required).
  --author <id>   Author identifier: email (matched exact, case-insensitive)
                   or name/nick substring (matched case-insensitive) (required).
  --ids a,b,c     Extra author identifiers (same matching rules), comma separated.
  --out <file>    Write JSON result to this file (in addition to stdout table).
  --help          Print this message and exit 0.

Requires GITLAB_TOKEN in the environment (read_api scope). Discovery is capped
by what the token can see (plan §11.2b) — projects the token has no membership
of are invisible even if the author committed there.
`;

function printHelp() {
  console.log(HELP);
}

function isEmailLike(id) {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(id);
}

function matchesAuthor(commit, ids) {
  const email = (commit.author_email || '').toLowerCase();
  const name = (commit.author_name || '').toLowerCase();
  for (const idRaw of ids) {
    const id = idRaw.toLowerCase().trim();
    if (!id) continue;
    if (isEmailLike(id)) {
      if (email === id) return idRaw;
    } else {
      if (name.includes(id) || email.includes(id)) return idRaw;
    }
  }
  return null;
}

async function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

// GET with 429 retry (max 3), honoring Retry-After.
async function gitlabGetWithRetry(apiPath, opts) {
  let attempt = 0;
  for (;;) {
    const res = await common.gitlabGet(apiPath, opts);
    if (res.status !== 429) return res;
    attempt++;
    if (attempt > 3) return res;
    const retryAfter = Number(res.headers.get('retry-after')) || 2;
    console.error(`[discoverRepos] rate limited (429) on ${apiPath}, waiting ${retryAfter}s (attempt ${attempt}/3)`);
    await sleep(retryAfter * 1000);
  }
}

async function gitlabGetAllWithRetry(apiPath, opts) {
  const items = [];
  const perPage = (opts && opts.perPage) || 100;
  const maxPages = (opts && opts.maxPages) || 1000;
  const params = (opts && opts.params) || {};
  for (let page = 1; page <= maxPages; page++) {
    const res = await gitlabGetWithRetry(apiPath, { ...opts, params: { ...params, per_page: perPage, page } });
    if (res.status !== 200) {
      throw new Error(`GitLab GET ${apiPath} page ${page} -> HTTP ${res.status}: ${JSON.stringify(res.body).slice(0, 300)}`);
    }
    if (!Array.isArray(res.body)) throw new Error(`GitLab GET ${apiPath}: expected array`);
    items.push(...res.body);
    const next = res.headers.get('x-next-page');
    if (!next) break;
  }
  return items;
}

async function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) {
    printHelp();
    process.exit(0);
  }

  const missing = ['from', 'to', 'author'].filter((k) => typeof args[k] !== 'string');
  if (missing.length > 0) {
    console.error(`discoverRepos.js: missing required arg(s): ${missing.map((m) => `--${m}`).join(', ')}`);
    printHelp();
    process.exit(1);
  }

  const from = args.from;
  const to = args.to;
  const authorIds = [args.author];
  if (typeof args.ids === 'string') {
    authorIds.push(...args.ids.split(',').map((s) => s.trim()).filter(Boolean));
  }

  if (!common.gitlabToken()) {
    console.error('discoverRepos.js: GITLAB_TOKEN is not set — export a personal access token with read_api scope.');
    process.exit(1);
  }

  const warnings = [];
  warnings.push('Discovery is capped by GitLab token visibility: only projects the token holder is a member of (or has access to) are scanned. Projects the author committed to but the token cannot see will be missing from this list (plan §11.2b).');
  warnings.push('No last_activity_at pre-filter is applied — scanning every visible project since deciding skip-safety from last_activity_at/created_at alone is not reliable.');

  console.log(`discoverRepos.js: listing visible projects (membership) …`);
  let projects;
  try {
    projects = await gitlabGetAllWithRetry('/projects', {
      params: { simple: true, archived: false, membership: true },
      perPage: 100,
    });
  } catch (e) {
    console.error(`discoverRepos.js: failed to list projects: ${e.message}`);
    process.exit(1);
  }

  const tokenVisibleProjects = projects.length;
  console.log(`discoverRepos.js: ${tokenVisibleProjects} visible project(s), scanning commits for author match(es): ${authorIds.join(', ')}`);

  const sinceIso = `${from}T00:00:00Z`;
  const untilIso = `${to}T23:59:59Z`;
  const results = [];
  let scannedProjects = 0;

  for (const project of projects) {
    scannedProjects++;
    const id = project.id;
    let commits;
    try {
      commits = await gitlabGetAllWithRetry(`/projects/${id}/repository/commits`, {
        params: { since: sinceIso, until: untilIso, all: true },
        perPage: 100,
      });
    } catch (e) {
      if (/HTTP 404/.test(e.message)) {
        // Project has no repository (empty project) — skip silently per spec.
        continue;
      }
      warnings.push(`project ${project.path_with_namespace} (id ${id}): commit fetch failed: ${e.message}`);
      continue;
    }

    const matched = [];
    const sampleAuthors = new Set();
    for (const c of commits) {
      const hit = matchesAuthor(c, authorIds);
      if (hit) {
        matched.push(c);
        sampleAuthors.add(c.author_email || c.author_name || hit);
      }
    }

    if (matched.length > 0) {
      results.push({
        id,
        path_with_namespace: project.path_with_namespace,
        http_url_to_repo: project.http_url_to_repo,
        commit_count: matched.length,
        sample_authors: Array.from(sampleAuthors).slice(0, 10),
      });
    }
  }

  const output = {
    schema_version: common.SCHEMA_VERSION,
    projects: results,
    scanned_projects: scannedProjects,
    token_visible_projects: tokenVisibleProjects,
    warnings,
  };

  // Human table to stdout.
  console.log('');
  console.log(`Found ${results.length} project(s) with matching commits between ${from} and ${to}:`);
  console.log('');
  const rows = results
    .slice()
    .sort((a, b) => b.commit_count - a.commit_count)
    .map((r) => [r.path_with_namespace, String(r.commit_count), r.sample_authors.join('; ')]);
  const header = ['project', 'commits', 'sample authors'];
  const widths = header.map((h, i) => Math.max(h.length, ...rows.map((r) => (r[i] || '').length), 4));
  const fmtRow = (r) => r.map((cell, i) => (cell || '').padEnd(widths[i])).join('  ');
  console.log(fmtRow(header));
  console.log(widths.map((w) => '-'.repeat(w)).join('  '));
  for (const r of rows) console.log(fmtRow(r));

  console.log('');
  for (const w of warnings) console.log(`[WARN] ${w}`);

  if (typeof args.out === 'string') {
    common.writeJsonPretty(args.out, output);
    console.log('');
    console.log(`Full result written to ${args.out}`);
  } else {
    console.log('');
    console.log(JSON.stringify(output));
  }
}

main().catch((e) => {
  console.error(`discoverRepos.js fatal error: ${e.message}`);
  process.exit(1);
});
