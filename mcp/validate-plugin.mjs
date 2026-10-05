import Ajv2020 from 'ajv/dist/2020.js';
import {readFileSync,existsSync} from 'node:fs';import path from 'node:path';import assert from 'node:assert/strict';
const plugin=path.resolve('../plugins/sidescreen'),ajv=new Ajv2020({strict:false,allErrors:true});
for(const [file,schema] of [['plugin.json','plugin'],['mcp.json','mcp']]) {
  const response=await fetch(`https://agent-plugins.org/schemas/1.0.0/${schema}.schema.json`);assert(response.ok,'Official schema unavailable');
  const rule=await response.json(),value=JSON.parse(readFileSync(path.join(plugin,file),'utf8'));
  const validate=ajv.compile(rule);assert(validate(value),JSON.stringify(validate.errors));
}
const manifest=JSON.parse(readFileSync(path.join(plugin,'plugin.json'),'utf8')),ui=manifest.extensions['com.openai'].interface;
assert(ui.displayName.length<=30&&ui.shortDescription.length<=30);
for(const name of [ui.logo,ui.composerIcon,'./runtime/server.cjs','./runtime/THIRD-PARTY-LICENSES.txt','./native/SideScreen.Cua.exe','./native/MinHook-LICENSE.txt']) {
  const full=path.resolve(plugin,name);assert(full.startsWith(plugin+path.sep)&&existsSync(full),'Missing/outside package asset');
}
const config=JSON.parse(readFileSync(path.join(plugin,'mcp.json'),'utf8'));
assert(Object.keys(config.mcpServers).length===1&&config.mcpServers.sidescreen.type==='stdio');
assert(!JSON.stringify(config).includes('MuseLink'),'Unexpected connector dependency');
console.log('PASS: portable manifest/MCP schemas, listing bounds and runtime assets');
