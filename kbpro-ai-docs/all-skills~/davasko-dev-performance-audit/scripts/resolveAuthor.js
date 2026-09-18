#!/usr/bin/env node
'use strict';
// resolveAuthor.js — author identity resolution (plan §5а.0c, §2). Local pass:
// `git shortlog -sne --all` over every repo dir in the cache, fuzzy-matched
// against known ids. Optional GitLab pass via /users?search=. Output is a set
// of candidates for the orchestrator/user to confirm — never auto-confirmed.
//
// Usage:
//   node resolveAuthor.js --author <id> [--ids extra,ids] [--cache <dir>] [--gitlab] [--out file]
//   node resolveAuthor.js --help

const fs = require('fs');
const path = require('path');
const common = require('./lib/common.js');

const HELP = `resolveAuthor.js — find author identity aliases across cached repos

Usage:
  node resolveAuthor.js --author <id> [options]

Options:
  --author <id>   Seed author identifier: email or nick (required).
  --ids a,b,c     Extra known identifiers, comma separated.
  --cache <dir>   Cache root directory to scan for repo dirs
                   (default: common.cacheRoot()).
  --gitlab        Also query GitLab /users?search= for each id (needs GITLAB_TOKEN).
  --out <file>    Write JSON result to this file (in addition to stdout).
  --help          Print this message and exit 0.

Output candidates are for the orchestrator/user to confirm; this script never
auto-confirms an alias. Iterating discovery<->resolveAuthor to a fixed point
is done by the skill orchestrator, not by this script.
`;

function printHelp() {
  console.log(HELP);
}

function levenshtein(a, b) {
  const m = a.length;
  const n = b.length;
  if (m === 0) return n;
  if (n === 0) return m;
  const dp = new Array(n + 1);
  for (let j = 0; j <= n; j++) dp[j] = j;
  for (let i = 1; i <= m; i++) {
    let prev = dp[0];
    dp[0] = i;
    for (let j = 1; j <= n; j++) {
      const tmp = dp[j];
      dp[j] = Math.min(
        dp[j] + 1,
        dp[j - 1] + 1,
        prev + (a[i - 1] === b[j - 1] ? 0 : 1)
      );
      prev = tmp;
    }
  }
  return dp[n];
}

function emailLocalPart(email) {
  const at = email.indexOf('@');
  return at === -1 ? email : email.slice(0, at);
}

function nameTokens(name) {
  return name
    .toLowerCase()
    .split(/[\s._-]+/)
    .map((t) => t.trim())
    .filter(Boolean);
}

// Returns a match reason string if (name,email) fuzzily matches id, else null.
function matchReason(name, email, id) {
  const nameLc = (name || '').toLowerCase();
  const emailLc = (email || '').toLowerCase();
  const idLc = id.toLowerCase();

  if (emailLc && emailLc === idLc) return 'exact-email';
  if (nameLc && nameLc === idLc) return 'exact-name';

  const idTokens = nameTokens(id);
  const candTokens = nameTokens(name || '');
  if (idTokens.length > 0 && candTokens.length > 0) {
    for (const t of idTokens) {
      if (t.length >= 3 && candTokens.some((ct) => ct.includes(t) || t.includes(ct))) {
        return 'name-token-overlap';
      }
    }
  }
  // Substring, e.g. "DavASko" in "gDavASko".
  if (nameLc && idLc.length >= 3 && (nameLc.includes(idLc) || idLc.includes(nameLc))) {
    return 'name-substring';
  }

  if (emailLc) {
    const emailLocal = emailLocalPart(emailLc);
    const idLocal = /@/.test(idLc) ? emailLocalPart(idLc) : idLc;
    if (idLocal.length >= 3 && emailLocal.length >= 3) {
      const dist = levenshtein(emailLocal, idLocal);
      if (dist <= 2) return `email-local-levenshtein-${dist}`;
    }
  }
  return null;
}

function listRepoDirs(cacheDir) {
  if (!fs.existsSync(cacheDir)) return [];
  return fs.readdirSync(cacheDir, { withFileTypes: true })
    .filter((d) => d.isDirectory())
    .map((d) => path.join(cacheDir, d.name))
    .filter((p) => {
      const res = common.git(p, ['rev-parse', '--git-dir']);
      return res.status === 0;
    });
}

function shortlogRepo(repoDir) {
  const res = common.git(repoDir, ['shortlog', '-sne', '--all']);
  if (res.status !== 0) return [];
  const out = [];
  // Format: "  <count>\t<name> <email>"
  const lineRe = /^\s*\d+\t(.*?)\s*<([^>]*)>\s*$/;
  for (const line of res.stdout.split(/\r?\n/)) {
    if (!line.trim()) continue;
    const m = line.match(lineRe);
    if (m) out.push({ name: m[1].trim(), email: m[2].trim() });
  }
  return out;
}

async function gitlabSearch(ids) {
  const found = [];
  for (const id of ids) {
    try {
      const res = await common.gitlabGet('/users', { params: { search: id } });
      if (res.status === 200 && Array.isArray(res.body)) {
        for (const u of res.body) {
          found.push({
            id,
            username: u.username,
            name: u.name,
            public_email: u.public_email || null,
          });
        }
      } else {
        console.log(`[WARN] gitlab:users-search "${id}" -> HTTP ${res.status}`);
      }
    } catch (e) {
      console.log(`[WARN] gitlab:users-search "${id}" failed: ${e.message}`);
    }
  }
  return found;
}

async function main() {
  const args = common.parseArgs(process.argv);
  if (args.help) {
    printHelp();
    process.exit(0);
  }

  if (typeof args.author !== 'string') {
    console.error('resolveAuthor.js: missing required arg --author <id>');
    printHelp();
    process.exit(1);
  }

  const seedIds = [args.author];
  if (typeof args.ids === 'string') {
    seedIds.push(...args.ids.split(',').map((s) => s.trim()).filter(Boolean));
  }

  const cacheDir = typeof args.cache === 'string' ? path.resolve(args.cache) : common.cacheRoot();
  const repoDirs = listRepoDirs(cacheDir);

  if (repoDirs.length === 0) {
    console.log(`[WARN] resolveAuthor: no git repos found under ${cacheDir} — run syncRepos.js first for local matching.`);
  }

  // repos affected per (name,email) pair, dedup by "name <email>" key.
  const candidateMap = new Map();

  for (const repoDir of repoDirs) {
    const repoLabel = path.basename(repoDir);
    const entries = shortlogRepo(repoDir);
    for (const { name, email } of entries) {
      let bestReason = null;
      for (const id of seedIds) {
        const reason = matchReason(name, email, id);
        if (reason) {
          bestReason = reason;
          break;
        }
      }
      if (!bestReason) continue;
      const key = `${name} <${email}>`;
      if (!candidateMap.has(key)) {
        candidateMap.set(key, { name, email, repos: [], match_reason: bestReason });
      }
      const entry = candidateMap.get(key);
      if (!entry.repos.includes(repoLabel)) entry.repos.push(repoLabel);
    }
  }

  const candidates = Array.from(candidateMap.values());

  let gitlabMatches = [];
  if (args.gitlab) {
    if (!common.gitlabToken()) {
      console.log('[WARN] resolveAuthor: --gitlab given but GITLAB_TOKEN is not set — skipping GitLab pass.');
    } else {
      gitlabMatches = await gitlabSearch(seedIds);
    }
  }

  const output = {
    schema_version: common.SCHEMA_VERSION,
    confirmed_seed: seedIds,
    candidates,
    gitlab_matches: gitlabMatches,
  };

  console.log('');
  console.log(`resolveAuthor.js: ${candidates.length} local candidate alias(es) found across ${repoDirs.length} repo(s).`);
  for (const c of candidates) {
    console.log(`  - "${c.name} <${c.email}>"  reason=${c.match_reason}  repos=${c.repos.join(',')}`);
  }
  if (args.gitlab) {
    console.log(`resolveAuthor.js: ${gitlabMatches.length} GitLab user match(es).`);
    for (const g of gitlabMatches) {
      console.log(`  - id="${g.id}" -> username=${g.username} name="${g.name}" public_email=${g.public_email || 'n/a'}`);
    }
  }
  console.log('');
  console.log('NOTE: these are candidates for confirmation by the orchestrator/user — none are auto-confirmed.');

  if (typeof args.out === 'string') {
    common.writeJsonPretty(args.out, output);
    console.log(`resolveAuthor.js: result written to ${args.out}`);
  } else {
    console.log(JSON.stringify(output));
  }
}

main().catch((e) => {
  console.error(`resolveAuthor.js fatal error: ${e.message}`);
  process.exit(1);
});
