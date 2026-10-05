import {test} from 'node:test';
import assert from 'node:assert/strict';
import {createSideScreenEngine} from '../desktop-engine.mjs';

const args={window_handle:9,expected_display_id:'side',steps:[
  {selector:{label:'Counter',role:'Button'},tool:'click',arguments:{}},
  {selector:{label:'Counter',role:'Button'},tool:'click',arguments:{}}
]};
const identity={windowHandle:9,processId:42,processStartTicks:'1',displayId:'side',bounds:{X:-1200,Y:0,Width:700,Height:500}};
const observation=scope=>({ok:true,scope,backend:'native-control-messages',observationId:'fresh',state:{elements:[{label:'Counter',role:'Button',element_token:'token',sidescreen_route:'native-control-messages',actions:['invoke']}]}});
for(const [name,change] of [['reused window handle',{processStartTicks:'2'}],['moved window',{bounds:{...identity.bounds,X:-1199}}]]) {
  test(`batch stops before the next action on ${name}`,async()=>{
    let observations=0,actions=0;
    const engine=createSideScreenEngine({nativeFirst:true,run:async request=>{
      if(request.Action==='NativeObserve')return observation(observations++===0?identity:{...identity,...change});
      if(request.Action==='CuaAct'){actions++;return {ok:true};}
      throw Error('Unexpected backend');
    }});
    const result=JSON.parse((await engine({method:'call',tool:'sidescreen_steps',arguments:args})).content[0].text);
    assert.equal(result.ok,false);assert.equal(result.stop,true);assert.equal(actions,1);
    assert.match(result.error,/identity or geometry/);engine.close();
  });
}
test('backend fallback cannot dispatch to a replacement process',async()=>{
  let actions=0;
  const engine=createSideScreenEngine({nativeFirst:true,run:async request=>{
    if(request.Action==='NativeObserve')return {...observation(identity),state:{elements:[]}};
    if(request.Action==='CuaObserve')return observation({...identity,processId:43});
    if(request.Action==='CuaAct'){actions++;return {ok:true};}
    throw Error('Unexpected backend');
  }});
  const result=JSON.parse((await engine({method:'call',tool:'sidescreen_steps',arguments:args})).content[0].text);
  assert.equal(result.ok,false);assert.equal(actions,0);assert.match(result.error,/backend preparation/);engine.close();
});
test('leased batch cannot establish a new identity before its first action',async()=>{
  let actions=0;
  const engine=createSideScreenEngine({nativeFirst:true,run:async request=>{
    if(request.Action==='NativeObserve')return observation({...identity,processStartTicks:'2'});
    if(request.Action==='CuaAct'){actions++;return {ok:true};}
  }});
  const result=JSON.parse((await engine({method:'call',tool:'sidescreen_steps',expectedScope:identity,arguments:args})).content[0].text);
  assert.equal(result.ok,false);assert.equal(actions,0);engine.close();
});
test('scope reply metadata does not block an unchanged leased native batch',async()=>{
  let actions=0;
  const engine=createSideScreenEngine({nativeFirst:true,run:async request=>{
    if(request.Action==='NativeObserve')return observation(identity);
    if(request.Action==='CuaAct'){actions++;return {ok:true};}
  }});
  const result=JSON.parse((await engine({method:'call',tool:'sidescreen_steps',expectedScope:{ok:true,...identity},arguments:args})).content[0].text);
  assert.equal(result.ok,true);assert.equal(actions,2);engine.close();
});
