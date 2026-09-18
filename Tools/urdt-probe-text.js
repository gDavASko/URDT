const TOKEN = 'urdt-test-poligon';
const URL = 'ws://127.0.0.1:7777/';
const wait = ms => new Promise(r => setTimeout(r, ms));
class C {
  constructor(){this.s=0;this.p=new Map();}
  async connect(){this.ws=new WebSocket(URL);await new Promise((res,rej)=>{this.ws.onopen=res;this.ws.onerror=rej;this.ws.onmessage=e=>{const m=JSON.parse(e.data);const r=this.p.get(m.id);if(r){this.p.delete(m.id);r(m);}};});return this.call('handshake',{token:TOKEN});}
  call(a,pl){return new Promise((res,rej)=>{const id=String(++this.s);const to=setTimeout(()=>{this.p.delete(id);rej(new Error('timeout '+a));},5000);this.p.set(id,m=>{clearTimeout(to);res(m);});this.ws.send(JSON.stringify({api:1,id,a:0,action:a,payload:pl}));});}
  close(){this.ws.close();}
}
const sl = r => {
  if (!r || !r.data || !r.data.components) return null;
  for (const k of Object.keys(r.data.components)) {
    const c = r.data.components[k];
    if (c && (c.TargetId || c.ScreenRect)) return c;
  }
  return null;
};
async function main(){
  const c=new C(); await c.connect();
  await c.call('set_time_scale',{scale:1});
  await c.call('click',{testId:'btn_open_ui_suite'}); await wait(300);

  // focus the field
  const f=await c.call('click',{testId:'ui.text_input'});
  console.log('focus click     :', JSON.stringify({status:f.status,clicked:f.data&&f.data.clicked,err:f.error}));
  await wait(300);
  // read EventSystem.selected via hit_test at field center
  const fs=sl(await c.call('inspect',{testId:'ui.text_input'}));
  const ht=await c.call('hit_test',{x:fs.ScreenCenter.x,y:fs.ScreenCenter.y});
  console.log('ES.selected     :', ht.data && ht.data.eventSystem && ht.data.eventSystem.selectedPath);
  console.log('topHit          :', ht.data && ht.data.topHit && ht.data.topHit.path);

  // type
  const t=await c.call('type_text',{text:'привет'});
  console.log('type_text resp  :', JSON.stringify(t.data||t.error));
  await wait(500);
  const after=sl(await c.call('inspect',{testId:'ui.text_input'}));
  console.log('InputValue after:', JSON.stringify(after && after.InputValue), 'ic=', after&&after.InteractionCount, 'last=', after&&after.LastInputAction);

  // try a single key_press to compare (space is in Key enum)
  const k=await c.call('key_press',{key:'a'});
  console.log('key_press a     :', JSON.stringify(k.data||k.error));
  await wait(400);
  const after2=sl(await c.call('inspect',{testId:'ui.text_input'}));
  console.log('InputValue after key:', JSON.stringify(after2 && after2.InputValue));
  c.close();
}
main().catch(e=>{console.error(e.message||e);process.exit(1);});
