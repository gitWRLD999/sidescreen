import {spawn} from 'node:child_process';
import {readFileSync,writeFileSync,mkdirSync,existsSync} from 'node:fs';
import {fileURLToPath} from 'node:url';
import path from 'node:path';
import assert from 'node:assert/strict';
import {createSideScreenEngine} from '../mcp/desktop-engine.mjs';

// Paired, counterbalanced A/B trials. Actual app event handlers write the
// oracle; a successful transport receipt alone never counts as success.
const dist=fileURLToPath(new URL('../dist/',import.meta.url));
const artifacts=path.resolve(process.argv[2]||'work/benchmark');
const rounds=Number(process.argv[3]||6);
assert(Number.isInteger(rounds)&&rounds>=2&&rounds<=20);
mkdirSync(artifacts,{recursive:true});
const decode=r=>JSON.parse(r.content.find(c=>c.type==='text').text);
const rows=[],children=[],engines=[];
const read=file=>JSON.parse(readFileSync(file,'utf8'));
const sleep=ms=>new Promise(r=>setTimeout(r,ms));
async function call(engine,tool,args){return decode(await engine({method:'call',tool,arguments:args}));}
try {
  const fixtures=[];
  for(const kind of ['native','wpf']) {
    const file=path.join(artifacts,`${kind}-${crypto.randomUUID()}.json`);
    const child=spawn(path.join(dist,kind==='native'?'SideScreen.BackgroundProbe.exe':'SideScreen.CuaWpfProbe.exe'),[file],{windowsHide:true,stdio:'ignore'});children.push(child);
    const deadline=Date.now()+10000;while(!existsSync(file)&&Date.now()<deadline)await sleep(100);
    assert(existsSync(file),'Fixture did not render');fixtures.push({kind,file,handle:read(file).handle});
  }
  for(const nativeFirst of [false,true])engines.push(createSideScreenEngine({directory:dist,nativeFirst}));
  const status=await call(engines[0],'sidescreen_status',{});assert(status.available&&status.ready,'Live SideScreen and CUA required');
  const windows=(await call(engines[0],'sidescreen_windows',{})).windows;
  for(const f of fixtures)assert(windows.some(w=>w.Handle===f.handle),'Fixture missing from current SideScreen enumeration');
  for(let round=0;round<rounds;round++)for(const f of (round%2?[...fixtures].reverse():fixtures))for(const arm of (round%2?[1,0]:[0,1])) {
    const before=read(f.file),value=`A/B ${arm} round ${round} café Ω`;
    const steps=[
      {selector:f.kind==='native'?{label:'Agent text',role:'Edit'}:{role:'Edit'},tool:'set_value',arguments:{value}},
      {selector:{label:f.kind==='native'?'Increment counter':'Increment WPF counter',role:'Button'},tool:'click',arguments:{}},
      {selector:{label:f.kind==='native'?'Agent checkbox':'WPF checkbox',role:'CheckBox'},tool:'click',arguments:{}}
    ];
    const start=performance.now();const result=await call(engines[arm],'sidescreen_steps',{window_handle:f.handle,expected_display_id:status.agentScreen.id,steps});
    const elapsedMs=Math.round(performance.now()-start),after=read(f.file);
    const effectVerified=after.text===value&&after.clicks===before.clicks+1&&after.check===!before.check;
    const focus=(result.receipts||[]).map(r=>({preserved:r.focus?.Preserved===true,cursorPreserved:r.focus?.CursorPreserved,foregroundChanges:r.focus?.ForegroundChanges,targetActivated:r.focus?.TargetActivated}));
    const row={round,fixture:f.kind,arm:arm?'B-native-first':'A-CUA-first',elapsedMs,ok:result.ok,effectVerified,completed:result.completed,focus,observationTiming:result.observation?.timing};rows.push(row);
    writeFileSync(path.join(artifacts,'ab.json'),JSON.stringify({date:'2026-10-05',rounds,rows},null,2)+'\n');
    console.log(JSON.stringify(row));
    assert(result.ok&&effectVerified&&focus.length===3&&focus.every(x=>x.preserved&&!x.targetActivated),'Effect or isolation failure; no automatic replay');
  }
  const percentile=(v,p)=>[...v].sort((a,b)=>a-b)[Math.ceil(v.length*p)-1];
  const median=v=>{const sorted=[...v].sort((a,b)=>a-b),middle=Math.floor(sorted.length/2);return sorted.length%2?sorted[middle]:(sorted[middle-1]+sorted[middle])/2;};
  const summary=[];
  for(const fixture of ['native','wpf'])for(const arm of ['A-CUA-first','B-native-first']) {
    const matching=rows.filter(r=>r.fixture===fixture&&r.arm===arm),latencies=matching.map(r=>r.elapsedMs);
    summary.push({fixture,arm,n:latencies.length,p50Ms:median(latencies),p95Ms:percentile(latencies,.95),success:matching.filter(r=>r.ok&&r.effectVerified).length,foregroundChanges:matching.reduce((n,r)=>n+r.focus.reduce((a,f)=>a+(f.foregroundChanges||0),0),0)});
  }
  writeFileSync(path.join(artifacts,'summary.json'),JSON.stringify(summary,null,2)+'\n');console.log(JSON.stringify({summary}));
}finally{for(const engine of engines)engine.close();for(const child of children)child.kill();}
