import assert from 'node:assert/strict';
import {fileURLToPath} from 'node:url';
import {createSideScreenEngine,translateSideScreen,sideScreenTools} from '../integrations/muse/sidescreen-engine.mjs';
let checks=0;
function check(value){assert(value);checks++;}
function reject(fn){assert.throws(fn);checks++;}
check(sideScreenTools.length===4);
reject(()=>translateSideScreen('bring_to_front'));
reject(()=>translateSideScreen('sidescreen_status',{engine:'desktop'}));
reject(()=>translateSideScreen('sidescreen_act',{window_handle:1,expected_display_id:'display'}));
reject(()=>translateSideScreen('sidescreen_observe',{window_handle:1,expected_display_id:'display',target:{kind:'desktop'}}));
check(translateSideScreen('sidescreen_observe',{window_handle:1,expected_display_id:'display',include_screenshot:false}).IncludeScreenshot===false);
let received;
const fake=createSideScreenEngine({run:async request=>{received=request;return {ok:false,stop:true,error:'refused'};}});
const result=await fake({method:'call',tool:'sidescreen_act',arguments:{window_handle:1,expected_display_id:'display',observation_id:'fresh',tool:'click',arguments:{element_token:'token'}}});
check(result.isError);
check(result.structuredContent.stop);
check(received.Action==='CuaAct' && received.Tool==='click' && received.WindowHandle===1);
check((await fake({method:'list'})).tools.length===4);
// Read-only end-to-end transport with the freshly compiled Windows helper.
const live=createSideScreenEngine({directory:fileURLToPath(new URL('../dist/',import.meta.url))});
const status=await live({method:'call',tool:'sidescreen_status',arguments:{}});
check(!status.isError && status.structuredContent.inputIsolation===false);
const windows=await live({method:'call',tool:'sidescreen_windows',arguments:{}});
check(status.structuredContent.available
  ? !windows.isError && Array.isArray(windows.structuredContent.windows)
  : windows.isError && windows.structuredContent.stop);
console.log(`PASS: ${checks} Muse adapter checks`);
