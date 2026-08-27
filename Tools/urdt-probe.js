/*
 * Ad-hoc diagnostic probe against a LIVE URDT server (ws://127.0.0.1:7777).
 * Replicates the runner's modal path and pinpoints why closing the modal fails.
 */
const TOKEN = 'urdt-test-poligon';
const URL = 'ws://127.0.0.1:7777/';
const wait = ms => new Promise(r => setTimeout(r, ms));

class Client {
  constructor() { this.seq = 0; this.pending = new Map(); }
  async connect() {
    this.socket = new WebSocket(URL);
    await new Promise((res, rej) => {
      this.socket.onopen = res; this.socket.onerror = rej;
      this.socket.onmessage = e => { const m = JSON.parse(e.data); const r = this.pending.get(m.id); if (r) { this.pending.delete(m.id); r(m); } };
    });
    return this.call('handshake', { token: TOKEN });
  }
  call(action, payload) {
    return new Promise((res, rej) => {
      const id = String(++this.seq);
      const to = setTimeout(() => { this.pending.delete(id); rej(new Error('timeout waiting for ' + action)); }, 5000);
      this.pending.set(id, m => { clearTimeout(to); res(m); });
      this.socket.send(JSON.stringify({ api: 1, id, action, payload }));
    });
  }
  close() { this.socket.close(); }
}

const slice = r => (r && r.data && r.data.components) ? r.data.components.UrdtTestPoligonDebugTarget : null;
const brief = s => s ? JSON.stringify({ vis: s.IsVisible, inter: s.IsInteractable, win: s.ActiveWindow, ic: s.InteractionCount, rect: s.ScreenRect, center: s.ScreenCenter }) : 'null';

async function main() {
  const c = new Client();
  const hs = await c.connect();
  console.log('handshake.data =', JSON.stringify(hs.data));

  // Nudge to realtime in case the session is frame-stepped (deterministic).
  const ts = await c.call('set_time_scale', { scale: 1 });
  console.log('set_time_scale =', JSON.stringify({ status: ts.status, data: ts.data, err: ts.error }));
  await wait(300);

  // open UI suite
  let r = await c.call('click', { testId: 'btn_open_ui_suite' });
  console.log('\nclick btn_open_ui_suite  :', JSON.stringify({ status: r.status, clicked: r.data && r.data.clicked, err: r.error }));
  await wait(400);
  let win = slice(await c.call('inspect', { testId: 'btn_open_ui_suite' }));
  console.log('  ActiveWindow now       :', win && win.ActiveWindow);

  const closeVis = async label => {
    const s = slice(await c.call('inspect', { testId: 'ui.modal_close_button' }));
    console.log('  ' + label.padEnd(24), 'IsVisible=' + (s && s.IsVisible), 'ic=' + (s && s.InteractionCount));
    return s;
  };

  console.log('\n--- modal reaction test (drive by testId only) ---');
  await closeVis('close BEFORE open modal');

  r = await c.call('click', { testId: 'ui.modal_button' });
  console.log('click ui.modal_button    :', JSON.stringify({ status: r.status, clicked: r.data && r.data.clicked }));
  await wait(500);
  await closeVis('close AFTER open modal');   // IsVisible should flip true if modal opened

  r = await c.call('click', { testId: 'ui.modal_close_button' });
  console.log('click ui.modal_close_btn :', JSON.stringify({ status: r.status, clicked: r.data && r.data.clicked }));
  await wait(500);
  await closeVis('close AFTER close modal');  // IsVisible should flip false if modal closed

  c.close();
}
main().catch(e => { console.error(e.stack || e); process.exit(1); });
