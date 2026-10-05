import {Server} from '@modelcontextprotocol/sdk/server/index.js';
import {StdioServerTransport} from '@modelcontextprotocol/sdk/server/stdio.js';
import {CallToolRequestSchema,ListToolsRequestSchema} from '@modelcontextprotocol/sdk/types.js';
import {randomUUID} from 'node:crypto';
import {createSideScreenEngine,sideScreenTools} from './desktop-engine.mjs';
import {createSideUsers,sideUserTools} from './sideusers.mjs';
import {validate,sameScope} from './validation.mjs';

export function createDesktopRouter({side=createSideScreenEngine(),owner=randomUUID()}={}) {
  const users=createSideUsers(side,{validate,sameScope}),sessions=new Map();
  let tail=Promise.resolve(),closed=false;
  const decode=r=>JSON.parse(r.content.find(c=>c.type==='text').text);
  async function handle(name,args={}) {
    if(closed)throw Error('SideScreen connection closed; no input was replayed.');
    const request={method:'call',tool:name,arguments:args,callerId:owner};
    let acquired=false,handle=args.window_handle||sessions.get(args.session_id);
    if(name==='sideuser_open') {
      const scope=await side.scope(args);if(!scope.ok)return {isError:true,content:[{type:'text',text:JSON.stringify(scope)}]};
      const lease=await side.lease(args);if(!lease.ok)return {isError:true,content:[{type:'text',text:JSON.stringify(lease)}]};acquired=true;
    }
    try {
      let result;
      if(sideUserTools.some(t=>t.name===name))result=await users.handle(request);
      else {await users.assertAccess(request);result=await side(request);}
      if(name==='sideuser_open') {
        const data=decode(result);if(data.ok)sessions.set(data.sessionId,handle);else await side.unlease(handle);
      }
      if(name==='sideuser_close'&&!result.isError)sessions.delete(args.session_id);
      return result;
    }catch(error){if(acquired)await side.unlease(handle);throw error;}
  }
  const expiry=setInterval(()=>{const next=tail.then(()=>users.assertAccess({callerId:owner,arguments:{}}));tail=next.catch(()=>{});},15000);expiry.unref();
  return {
    tools:[...sideScreenTools,...sideUserTools],
    call(name,args){const result=tail.then(()=>handle(name,args));tail=result.catch(()=>{});return result;},
    async close(){if(closed)return;closed=true;clearInterval(expiry);side.close();await tail.catch(()=>{});await users.close();}
  };
}
async function main() {
  let router=createDesktopRouter(),stopped=false;
  const stopTool={name:'sidescreen_stop',description:'Immediately stop this desktop connection and release its window leases. No input is replayed or focus restored. A fresh status call is required before new work.',inputSchema:{type:'object',properties:{hook_event_name:{type:'string'},session_id:{type:'string'},turn_id:{type:'string'}},additionalProperties:false}};
  const server=new Server({name:'sidescreen-desktop',version:'1.0.0'},{capabilities:{tools:{}},instructions:'Use only current SideScreen windows. Observe, act once and verify actual state. Scoped background input must not fall back to foreground or global input. Coordinate actions need fresh screenshots. Use session leases for private cursor/keyboard state. Stop on unknown outcomes. This is a local Windows desktop plugin, independent of Muse Link and browser automation.'});
  let stopPromise=Promise.resolve();
  function stop(active) {
    if(router===active)stopped=true;
    const closing=active.close();
    if(router===active)stopPromise=closing;
    return closing;
  }
  server.setRequestHandler(ListToolsRequestSchema,()=>({tools:[...router.tools,stopTool]}));
  server.setRequestHandler(CallToolRequestSchema,async(request,extra)=>{
    if(request.params.name==='sidescreen_stop'){validate(stopTool.inputSchema,request.params.arguments||{});await stop(router);return {content:[{type:'text',text:JSON.stringify({ok:true,stopped:true,requiresFreshStatus:true})}]};}
    if(stopped&&request.params.name==='sidescreen_status'){await stopPromise;if(stopped){router=createDesktopRouter();stopped=false;}}
    const active=router;
    const cancelled=()=>{stop(active).catch(()=>{});};extra.signal.addEventListener('abort',cancelled,{once:true});
    try{return await active.call(request.params.name,request.params.arguments||{});}
    catch(error){return {isError:true,content:[{type:'text',text:JSON.stringify({ok:false,stop:true,error:error.message})}]};}
    finally{extra.signal.removeEventListener('abort',cancelled);}
  });
  let closing=false;
  async function close(){if(closing)return;closing=true;await router.close();await server.close();}
  server.onclose=()=>{close().catch(()=>{});};
  for(const signal of ['SIGINT','SIGTERM'])process.on(signal,()=>close().finally(()=>process.exit(0)));
  process.stdin.on('end',()=>close().finally(()=>process.exit(0)));
  await server.connect(new StdioServerTransport());
}
// Imports by tests do not start an MCP connection.
if(process.env.SIDESCREEN_MCP_TEST_IMPORT!=='1')main().catch(error=>{console.error(error.message);process.exitCode=1;});
