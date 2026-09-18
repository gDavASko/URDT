#!/usr/bin/env node
// Read-only evidence collector for kbpro-mr-reviewer.
// Orchestration and all LLM review are performed by kbpro-subagent-team, not here.
import { execFile } from 'node:child_process';
import { promisify } from 'node:util';

const execFileAsync = promisify(execFile);

function parseArgs(args) {
    const result = { base: null, head: null, local: false };
    for (let index = 0; index < args.length; index += 1) {
        const value = args[index];
        if (value === '--base' && args[index + 1]) {
            result.base = args[++index];
        } else if (value === '--head' && args[index + 1]) {
            result.head = args[++index];
        } else if (value === '--local') {
            result.local = true;
        } else {
            throw new Error(`Unknown or incomplete argument: ${value}`);
        }
    }
    if ((result.base || result.head) && (!result.base || !result.head)) {
        throw new Error('Use --base and --head together.');
    }
    return result;
}

async function git(args) {
    try {
        const { stdout, stderr } = await execFileAsync('git', args, { encoding: 'utf8' });
        return { ok: true, stdout: stdout.trim(), stderr: stderr.trim() };
    } catch (error) {
        return { ok: false, stdout: error.stdout?.trim() ?? '', stderr: error.stderr?.trim() ?? error.message };
    }
}

function lines(value) {
    return value ? value.split(/\r?\n/).filter(Boolean) : [];
}

async function main() {
    const options = parseArgs(process.argv.slice(2));
    const root = await git(['rev-parse', '--show-toplevel']);
    if (!root.ok) {
        throw new Error(`Not a git repository: ${root.stderr}`);
    }

    let range = null;
    let changed = [];
    let whitespace = { ok: true, stdout: '', stderr: '' };
    if (options.base) {
        range = `${options.base}...${options.head}`;
        changed = lines((await git(['diff', '--name-only', '--no-ext-diff', range])).stdout);
        whitespace = await git(['diff', '--check', '--no-ext-diff', range]);
    } else if (options.local) {
        const unstaged = await git(['diff', '--name-only', '--no-ext-diff']);
        const staged = await git(['diff', '--cached', '--name-only', '--no-ext-diff']);
        changed = [...new Set([...lines(unstaged.stdout), ...lines(staged.stdout)])].sort();
        const unstagedCheck = await git(['diff', '--check', '--no-ext-diff']);
        const stagedCheck = await git(['diff', '--cached', '--check', '--no-ext-diff']);
        whitespace = {
            ok: unstagedCheck.ok && stagedCheck.ok,
            stdout: [unstagedCheck.stdout, stagedCheck.stdout].filter(Boolean).join('\n'),
            stderr: [unstagedCheck.stderr, stagedCheck.stderr].filter(Boolean).join('\n')
        };
    } else {
        throw new Error('Specify a review range with --base <sha> --head <sha> or use --local.');
    }

    process.stdout.write(`${JSON.stringify({
        repository_root: root.stdout,
        range,
        changed_files: changed,
        diff_check: { ok: whitespace.ok, output: [whitespace.stdout, whitespace.stderr].filter(Boolean).join('\n') },
        limitations: [
            'This command collects read-only evidence only.',
            'Use kbpro-subagent-team code-review orchestration for analysis and independent judgment.',
            'No comments, commits, network requests, or model CLI calls were made.'
        ]
    }, null, 2)}\n`);
}

main().catch((error) => {
    process.stderr.write(`Evidence collection failed: ${error.message}\n`);
    process.exitCode = 2;
});
