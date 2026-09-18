// Apply DEPENDS_ON changes from a change set (reflow.cjs --changes-links output).
// Unlike GantCreator's set-links.cjs this also CLEARS predecessors (pred=null -> []),
// which happens when a task becomes the new head of a chain after a move.
// Sequential legacy task.item.update (slow, run in background). NEVER task.dependence.add.
// Usage: node apply-links.cjs [--links changes-links.json]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const links = JSON.parse(fs.readFileSync(arg('links', 'changes-links.json'), 'utf8').replace(/^﻿/, ''));
console.log(`Link updates: ${links.length}`);

async function main() {
  let ok = 0; const errors = [];
  for (const [i, u] of links.entries()) {
    try {
      const res = await fetch(`${base}/task.item.update.json`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ TASKID: Number(u.id), FIELDS: { DEPENDS_ON: u.pred ? [Number(u.pred)] : [] } }),
      });
      const data = await res.json();
      if (data.error) errors.push(`${u.pred || '(none)'}->${u.id}: ${data.error} ${data.error_description || ''}`);
      else ok++;
    } catch (e) { errors.push(`${u.pred || '(none)'}->${u.id}: ${e.message}`); }
    if ((i + 1) % 10 === 0) console.log(`...${i + 1}/${links.length}`);
    await new Promise(r => setTimeout(r, 250));
  }
  console.log(`\nDone: ok=${ok}, errors=${errors.length} of ${links.length}`);
  if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
}
main().catch(e => { console.error(e); process.exit(1); });
