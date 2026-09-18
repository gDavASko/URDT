// Create finish-start dependency arrows from the chain in updates.json.
// CRITICAL: the Gantt UI draws arrows ONLY from the legacy DEPENDS_ON field, so this
// script uses task.item.update (sequential, slow ~1-4s/call — run in background).
// NEVER use task.dependence.add (invisible "phantom" links) and never batch it.
// Usage: node set-links.cjs [--updates updates.json]
const fs = require('fs');

const arg = (n, d) => { const i = process.argv.indexOf('--' + n); return i > -1 ? process.argv[i + 1] : d; };
const base = (process.env.KBPRO_BITRIX24_WEBHOOK_BASE || '').replace(/\/$/, '');
if (!base) { console.error('KBPRO_BITRIX24_WEBHOOK_BASE not set'); process.exit(1); }
const updates = JSON.parse(fs.readFileSync(arg('updates', 'updates.json'), 'utf8').replace(/^﻿/, ''));

const links = updates.filter(u => u.pred); // every task except each person's first
console.log(`Links to set: ${links.length}`);

async function main() {
  let ok = 0; const errors = [];
  for (const [i, u] of links.entries()) {
    try {
      const res = await fetch(`${base}/task.item.update.json`, {
        method: 'POST', headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ TASKID: Number(u.id), FIELDS: { DEPENDS_ON: [Number(u.pred)] } }),
      });
      const data = await res.json();
      if (data.error) errors.push(`${u.pred}->${u.id}: ${data.error} ${data.error_description || ''}`);
      else ok++;
    } catch (e) { errors.push(`${u.pred}->${u.id}: ${e.message}`); }
    if ((i + 1) % 20 === 0) console.log(`...${i + 1}/${links.length}`);
    await new Promise(r => setTimeout(r, 250));
  }
  console.log(`\nDone: ok=${ok}, errors=${errors.length} of ${links.length}`);
  if (errors.length) { console.error(errors.join('\n')); process.exit(1); }
}
main().catch(e => { console.error(e); process.exit(1); });
