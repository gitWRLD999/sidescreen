import {test} from 'node:test';import assert from 'node:assert/strict';
process.env.SIDESCREEN_MCP_TEST_IMPORT='1';
const {createDesktopRouter}=await import('../server.mjs');
const reply=data=>({content:[{type:'text',text:JSON.stringify(data)}]});
test('independent router exposes only desktop/session tools and releases its kernel lease',async()=>{
  const calls=[];const scope={ok:true,windowHandle:9,processId:42,processStartTicks:'1',displayId:'side',bounds:{x:-1200}};
  const side=async r=>{calls.push(r);return reply({ok:true});};
  Object.assign(side,{scope:async()=>scope,lease:async()=>{calls.push('lease');return {ok:true};},unlease:async()=>{calls.push('unlease');return {ok:true};},release:async()=>({ok:true}),close:()=>{calls.push('close');}});
  const router=createDesktopRouter({side});
  assert.equal(router.tools.length,15);assert(!router.tools.some(t=>/chrome_|browser_|muse_|ssh/.test(t.name)));
  const opened=JSON.parse((await router.call('sideuser_open',{window_handle:9,expected_display_id:'side',label:'Desktop'})).content[0].text);
  assert.equal(opened.ok,true);
  await router.call('sideuser_close',{session_id:opened.sessionId});
  assert.deepEqual(calls,['lease','unlease']);await router.close();assert.equal(calls.at(-1),'close');
  await assert.rejects(router.call('sidescreen_status',{}),/closed/);
});
test('cross-process lease refusal dispatches no action',async()=>{
  let input=0;const side=async()=>{input++;return reply({ok:true});};
  Object.assign(side,{scope:async()=>({ok:true}),lease:async()=>({ok:false,stop:true,error:'leased'}),close:()=>{}});
  const router=createDesktopRouter({side});
  const result=await router.call('sideuser_open',{window_handle:9,expected_display_id:'side',label:'Desktop'});
  assert(result.isError);assert.equal(input,0);await router.close();
});
