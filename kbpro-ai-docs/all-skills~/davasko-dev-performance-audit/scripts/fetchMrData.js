#!/usr/bin/env node
'use strict';
// fetchMrData.js — GitLab MR layer (plan v2 phase 2). Fetches merge requests,
// approvals and MR commit shas for the subject author(s)
// over the analyzed range, and writes a compact mr-data.json.
//
// This unlocks the review metrics that were "m19: N/A" in v1:
//   a2 MR cycle time, a4 merged MRs/month,
//   c3 approval coverage, e4 share of commits landed without an MR.
//
// API budget: 1 list request chain per (project x username) + 2 detail calls
// per merged MR (approvals, commits). --max-mrs guards runaways.
// MR comments/notes are intentionally not fetched: mandatory AI reviews pollute
// this signal and must not affect performance metrics.
//
// GITLAB_TOKEN from env only; never persisted, never echoed.

const path = require('path');
const fs = require('fs');
const common = require('./lib/common.js');

const HELP = `fetchMrData.js — fetch merge-request data for perf-audit

Usage:
  node fetchMrData.js --projects <discovery.json> --usernames <u1,u2> \\
                      --from YYYY-MM-DD --to YYYY-MM-DD --out mr-data.json

Options:
  --projects <file>    discoverRepos.js output (project ids + paths).
  --usernames <list>   GitLab usernames of the subject author (from resolveAuthor).
  --from/--to <date>   Analysis range (MRs merged OR created in range are kept).
  --out <file>         Output file (required; put it in the run dir, not on C:).
  --max-mrs <n>        Safety cap on detail-fetched MRs (default 800).
  --help               Print this message and exit 0.`;

function die(msg, code = 1) { console.error(msg); process.exit(code); }

async function fetchProjectMrs(projectId, username, fromIso, toIso) {
  // created_after is widened backwards: an MR merged inside the range may have
  // been created before it. 90 days is plenty for this team's cycle times.
  const createdAfter = common.addDays(fromIso, -90);
  const items = await common.gitlabGetAll(
    `/projects/${projectId}/merge_requests`,
    {
      params: {
        author_username: username,
        state: 'all',
        created_after: `${createdAfter}T00:00:00Z`,
        updated_before: `${common.addDays(toIso, 1)}T00:00:00Z`,
        order_by: 'created_at',
        sort: 'asc',
      },
    },
  );
  const inRange = (iso) => {
    if (!iso) return false;
    const d = String(iso).slice(0, 10);
    return d >= fromIso && d <= toIso;
  };
  return items.filter(mr => inRange(mr.merged_at) || inRange(mr.created_at));
}

async function fetchDetails(projectId, mr) {
  const iid = mr.iid;
  const out = {
    approved_by: [],
    commit_shas: [],
  };
  try {
    const { status, body } = await common.gitlabGet(`/projects/${projectId}/merge_requests/${iid}/approvals`);
    if (status === 200 && body && Array.isArray(body.approved_by)) {
      out.approved_by = body.approved_by.map(a => a.user && a.user.username).filter(Boolean);
    }
  } catch (_) { /* approvals API may be disabled on the instance tier */ }
  try {
    const commits = await common.gitlabGetAll(`/projects/${projectId}/merge_requests/${iid}/commits`);
    out.commit_shas = commits.map(c => c.id).filter(Boolean);
  } catch (_) { /* commits are optional */ }
  return out;
}

async function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) { console.log(HELP); process.exit(0); }
  for (const req of ['projects', 'usernames', 'from', 'to', 'out']) {
    if (!args[req]) die(`missing --${req}\n\n${HELP}`);
  }
  if (!common.gitlabToken()) die('GITLAB_TOKEN is not set');
  const outFile = path.resolve(String(args.out));
  common.assertNotDriveC(outFile, 'mr-data output');

  const discovery = common.readJson(path.resolve(String(args.projects)));
  const projects = Array.isArray(discovery.projects) ? discovery.projects : [];
  if (!projects.length) die('no projects in discovery file');
  const usernames = String(args.usernames).split(',').map(s => s.trim()).filter(Boolean);
  const fromIso = String(args.from);
  const toIso = String(args.to);
  const maxMrs = Number(args['max-mrs']) || 800;

  const mrs = [];
  const flags = [];
  let detailBudget = maxMrs;

  for (const p of projects) {
    const projectId = p.id;
    const repo = p.path_with_namespace || String(projectId);
    for (const username of usernames) {
      let list;
      try {
        list = await fetchProjectMrs(projectId, username, fromIso, toIso);
      } catch (e) {
        console.warn(`WARN: MR list failed for ${repo}/${username}: ${e.message}`);
        flags.push(`MR_LIST_FAILED:${repo}`);
        continue;
      }
      for (const mr of list) {
        let details = { approved_by: [], commit_shas: [] };
        const isMerged = mr.state === 'merged';
        if (isMerged && detailBudget > 0) {
          detailBudget--;
          details = await fetchDetails(projectId, mr);
        }
        mrs.push({
          repo,
          iid: mr.iid,
          author_username: mr.author && mr.author.username,
          title: mr.title,
          state: mr.state,
          draft: Boolean(mr.draft || mr.work_in_progress),
          target_branch: mr.target_branch,
          created_at: mr.created_at,
          merged_at: mr.merged_at || null,
          closed_at: mr.closed_at || null,
          merge_commit_sha: mr.merge_commit_sha || null,
          squash_commit_sha: mr.squash_commit_sha || null,
          changes_count: mr.changes_count != null ? String(mr.changes_count) : null,
          labels: mr.labels || [],
          ...details,
        });
      }
    }
  }
  if (detailBudget <= 0) flags.push('MR_DETAIL_BUDGET_EXHAUSTED');

  common.writeJson(outFile, {
    schema_version: common.SCHEMA_VERSION,
    range: { from: fromIso, to: toIso },
    usernames,
    fetched_at: new Date().toISOString(),
    mrs,
    flags,
  });
  console.log(`wrote ${outFile} (mrs=${mrs.length}, flags=${flags.length})`);
}

main().catch(e => die(`fetchMrData.js: ${e.stack || e.message}`));
