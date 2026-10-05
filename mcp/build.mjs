import {build} from 'esbuild';
import {readFileSync,writeFileSync,mkdirSync,existsSync,copyFileSync} from 'node:fs';
import path from 'node:path';
const runtime=path.resolve('../plugins/sidescreen/runtime');mkdirSync(runtime,{recursive:true});
const result=await build({entryPoints:['server.mjs'],outfile:path.join(runtime,'server.cjs'),bundle:true,platform:'node',format:'cjs',target:'node22',metafile:true,legalComments:'inline'});
const packages=new Set();
for(const file of Object.keys(result.metafile.inputs)) {
  const match=file.match(/^node_modules\/(?:@[^/]+\/)?[^/]+/);if(match)packages.add(match[0]);
}
let notices='Bundled dependencies for SideScreen local MCP. Build-only esbuild is not included.\n';
for(const dir of [...packages].sort()) {
  const pkg=JSON.parse(readFileSync(path.join(dir,'package.json'),'utf8'));
  const license=['LICENSE','LICENSE.md','LICENSE.txt','LICENSE-MIT'].find(f=>existsSync(path.join(dir,f)));
  if(!license)throw Error(`Review license for ${pkg.name} before distributing`);
  notices+=`\n--- ${pkg.name} ${pkg.version} (${pkg.license}) ---\n`+readFileSync(path.join(dir,license),'utf8')+'\n';
}
writeFileSync(path.join(runtime,'THIRD-PARTY-LICENSES.txt'),notices);
const native=path.resolve('../plugins/sidescreen/native');mkdirSync(native,{recursive:true});
for(const name of ['SideScreen.Cua.exe','SideScreen.Input.exe','SideScreen.Core.dll','SideScreen.Virtual32Host.exe','SideScreen.VirtualInput.dll','SideScreen.VirtualInput32.dll','start-cua.ps1','LICENSE','MinHook-LICENSE.txt']) {
  const source=path.resolve('../dist',name);if(!existsSync(source))throw Error(`Build native SideScreen first: missing ${name}`);copyFileSync(source,path.join(native,name));
}
console.log(`Built independent desktop MCP (${packages.size} bundled dependency licenses).`);
