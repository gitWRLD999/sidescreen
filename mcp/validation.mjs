export function validate(rule,value,name='arguments') {
  if(rule.enum&&!rule.enum.includes(value))throw Error(`Invalid ${name}`);
  const types=Array.isArray(rule.type)?rule.type:[rule.type];
  if(types.includes('null')&&value===null)return;
  const type=Array.isArray(value)?'array':Number.isInteger(value)?'integer':typeof value;
  if(!types.includes(type)&&!(type==='integer'&&types.includes('number')))throw Error(`Invalid ${name}`);
  if((type==='integer'||type==='number')&&(!Number.isFinite(value)||value<(rule.minimum??-Infinity)||value>(rule.maximum??Infinity)))throw Error(`Invalid ${name}`);
  if(type==='string'&&(value.length<(rule.minLength??0)||value.length>(rule.maxLength??32768)||value.includes('\0')))throw Error(`Invalid ${name}`);
  if(type==='object') {
    if(!value||Object.keys(value).some(k=>!Object.hasOwn(rule.properties,k)))throw Error(`Unsupported ${name}`);
    for(const k of rule.required||[])if(!Object.hasOwn(value,k))throw Error(`Missing ${k}`);
    for(const [k,v] of Object.entries(value))validate(rule.properties[k],v,k);
  }
  if(type==='array') {
    if(value.length<(rule.minItems??0)||value.length>(rule.maxItems??Infinity))throw Error(`Invalid ${name}`);
    for(const v of value)validate(rule.items,v,name);
  }
}
export const sameScope=(a,b)=>a?.ok&&b?.ok&&a.windowHandle===b.windowHandle&&a.processId===b.processId&&a.processStartTicks===b.processStartTicks&&a.displayId===b.displayId&&JSON.stringify(a.bounds)===JSON.stringify(b.bounds);