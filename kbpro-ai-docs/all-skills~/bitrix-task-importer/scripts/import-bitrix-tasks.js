#!/usr/bin/env node
/**
 * import-bitrix-tasks.js
 *
 * Импортирует JSON-бэклог задач в Bitrix24 по двухшаговому контракту.
 *
 * Читает JSON-массив объектов { "fields": { ... } } и для каждой задачи:
 *   1. Создаёт задачу через tasks.task.add
 *   2. При наличии TAGS — проставляет теги через task.item.update (legacy API)
 *
 * Базовый URL вебхука берётся ТОЛЬКО из переменной окружения
 * KBPRO_BITRIX24_WEBHOOK_BASE. Хардкодить URL запрещено.
 *
 * Использование:
 *   node import-bitrix-tasks.js --json ../examples/sample-backlog.json
 *   node import-bitrix-tasks.js --json ../examples/sample-backlog.json --dry-run
 *
 * Флаги:
 *   --json <path>   Путь к JSON-файлу бэклога (UTF-8 с BOM или без)
 *   --dry-run       Не отправлять запросы, только показать что будет создано
 */

import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// ─── ANSI Colors ─────────────────────────────────────────────────────
const C = {
  reset:  '\x1b[0m',
  green:  '\x1b[32m',
  yellow: '\x1b[33m',
  cyan:   '\x1b[36m',
  red:    '\x1b[31m',
  gray:   '\x1b[2m',
};

// ─── CLI Arguments ────────────────────────────────────────────────────
const args = process.argv.slice(2);
const jsonIdx = args.indexOf('--json');
const isDryRun = args.includes('--dry-run') || args.includes('--whatif');

if (jsonIdx === -1 || !args[jsonIdx + 1]) {
  console.error(`${C.red}[ERROR]${C.reset} Usage: node import-bitrix-tasks.js --json <path> [--dry-run]`);
  process.exit(1);
}

const jsonPath = path.resolve(args[jsonIdx + 1]);

// ─── Webhook Setup ────────────────────────────────────────────────────
const webhookBase = process.env.KBPRO_BITRIX24_WEBHOOK_BASE;
if (!webhookBase || webhookBase.trim() === '') {
  console.error(`${C.red}[ERROR]${C.reset} Set KBPRO_BITRIX24_WEBHOOK_BASE to your Bitrix24 incoming webhook base URL.`);
  process.exit(1);
}

const base = webhookBase.replace(/\/$/, '');
const addUrl    = `${base}/tasks.task.add.json`;
const updateUrl = `${base}/task.item.update.json`;
const redactedAddUrl = '<redacted>/tasks.task.add.json';

// ─── Helpers ──────────────────────────────────────────────────────────

/** Читает файл UTF-8 и снимает BOM если есть */
function readText(filePath) {
  let content = fs.readFileSync(filePath, 'utf8');
  if (content.charCodeAt(0) === 0xFEFF) content = content.slice(1);
  return content;
}

/** POST JSON к Bitrix24 */
async function bitrixPost(url, body) {
  const response = await fetch(url, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json; charset=utf-8' },
    body: JSON.stringify(body),
  });
  if (!response.ok) {
    throw new Error(`HTTP ${response.status} ${response.statusText}`);
  }
  return response.json();
}

// ─── Main ─────────────────────────────────────────────────────────────

if (!fs.existsSync(jsonPath)) {
  console.error(`${C.red}[ERROR]${C.reset} JSON file not found at ${jsonPath}`);
  process.exit(1);
}

let tasks;
try {
  tasks = JSON.parse(readText(jsonPath));
} catch (err) {
  console.error(`${C.red}[ERROR]${C.reset} Failed to parse JSON: ${err.message}`);
  process.exit(1);
}

if (!Array.isArray(tasks)) {
  console.error(`${C.red}[ERROR]${C.reset} Expected JSON array of task objects, got: ${typeof tasks}`);
  process.exit(1);
}

console.log(`${C.yellow}[*]${C.reset} Found ${tasks.length} tasks in JSON.`);
if (isDryRun) {
  console.log(`${C.yellow}[DRY RUN]${C.reset} No requests will be sent.\n`);
}

let created = 0;
let failed  = 0;

for (const task of tasks) {
  const title = task.fields?.TITLE;
  const desc  = task.fields?.DESCRIPTION ?? '';
  const tags  = Array.isArray(task.fields?.TAGS) ? task.fields.TAGS : [];
  const capacityRaw = task.fields?.UF_TASK_CAPACITY ?? task.fields?.UF_TASKS_TASK_1783529349965;

  if (!title || title.trim() === '') {
    console.error(`${C.red}[SKIP]${C.reset} Task with empty TITLE skipped.`);
    failed++;
    continue;
  }

  if (capacityRaw === undefined || capacityRaw === null || !/^\d+$/.test(String(capacityRaw))) {
    console.error(`${C.red}[SKIP]${C.reset} Task '${title}' has invalid UF_TASK_CAPACITY. Use an integer person-hour number, e.g. 1, 4, 8, 20.`);
    failed++;
    continue;
  }

  const capacity = Number(capacityRaw);
  const taskData = {
    fields: {
      TITLE:               title,
      DESCRIPTION:         desc,
      RESPONSIBLE_ID:      task.fields?.RESPONSIBLE_ID,
      PRIORITY:            Number(task.fields?.PRIORITY ?? 1),
      ALLOW_TIME_TRACKING: task.fields?.ALLOW_TIME_TRACKING ?? '1',
      GROUP_ID:            task.fields?.GROUP_ID,
      UF_TASKS_TASK_1783529349965: capacity,
      DURATION_TYPE:       'hours',
      DURATION_PLAN:       capacity,
    },
  };

  console.log(`${C.cyan}[SEND]${C.reset} ${title}`);

  if (isDryRun) {
    console.log(`  ${C.gray}[DRY RUN] Would POST to: ${redactedAddUrl}${C.reset}`);
    console.log(`  ${C.gray}Payload: ${JSON.stringify(taskData)}${C.reset}`);
    if (tags.length > 0) {
      console.log(`  ${C.gray}Tags: ${tags.join(', ')}${C.reset}`);
    }
    console.log('---');
    continue;
  }

  try {
    // Step 1: Create task
    const addResp = await bitrixPost(addUrl, taskData);

    if (addResp.result?.task) {
      const taskId = Number(addResp.result.task.id);
      console.log(`  ${C.green}[OK]${C.reset} Task created. ID: ${taskId}`);
      created++;

      // Step 2: Apply tags (legacy API)
      if (tags.length > 0) {
        console.log(`  ${C.gray}Applying tags: ${tags.join(', ')}...${C.reset}`);
        await bitrixPost(updateUrl, [taskId, { TAGS: tags }]);
        console.log(`  ${C.green}[OK]${C.reset} Tags updated.`);
      }
    } else if (addResp.error) {
      console.error(`  ${C.red}[BITRIX ERROR]${C.reset} ${addResp.error}: ${addResp.error_description ?? ''}`);
      failed++;
    } else {
      console.error(`  ${C.red}[ERROR]${C.reset} Unexpected response (no task id in result).`);
      failed++;
    }
  } catch (err) {
    console.error(`  ${C.red}[ERROR]${C.reset} Failed to process task '${title}': ${err.message}`);
    failed++;
  }

  console.log('---');
}

console.log(`\n${C.yellow}[DONE]${C.reset} Created: ${C.green}${created}${C.reset}, Failed: ${C.red}${failed}${C.reset}, Total: ${tasks.length}.`);
if (failed > 0) process.exit(1);
