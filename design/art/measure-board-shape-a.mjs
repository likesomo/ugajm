// Ordinary, visible Chrome with remote debugging enabled; no Orca browser or headless renderer.
// node design/art/measure-board-shape-a.mjs <base-url> <before|after> <output-dir> [debug-port]
// Start chrome.exe --remote-debugging-port=9331 --user-data-dir=<ignored-dir> --window-size=1280,720
import {mkdir,writeFile} from 'node:fs/promises';
const [base,label,out,port='9331']=process.argv.slice(2);
if(!base||!label||!out)throw Error('Expected base URL, label, output directory');
await mkdir(out,{recursive:true});
const version=await (await fetch(`http://localhost:${port}/json/version`)).json();
const tab=await (await fetch(`http://localhost:${port}/json/new?about:blank`,{method:'PUT'})).json();
const ws=new WebSocket(tab.webSocketDebuggerUrl);await new Promise(r=>ws.addEventListener('open',r,{once:true}));
let next=0;const pending=new Map();const handlers=new Map();
ws.addEventListener('message',event=>{
 const message=JSON.parse(event.data);
 if(message.id){const p=pending.get(message.id);pending.delete(message.id);if(message.error)p.reject(Error(JSON.stringify(message.error)));else p.resolve(message.result);}
 else for(const handler of handlers.get(message.method)||[])handler(message.params);
});
function call(method,params={}){return new Promise((resolve,reject)=>{const id=++next;pending.set(id,{resolve,reject});ws.send(JSON.stringify({id,method,params}));});}
function on(method,fn){handlers.set(method,[...(handlers.get(method)||[]),fn]);}
await call('Page.enable');await call('Runtime.enable');
// The game canvas is fixed identically in both players; retain the host's real DPR.
await call('Page.addScriptToEvaluateOnNewDocument',{source:`
window.__bsaDraws=[];window.__bsaCalls=0;
for(const type of [window.WebGLRenderingContext,window.WebGL2RenderingContext])if(type){
 for(const name of ['drawArrays','drawElements','drawArraysInstanced','drawElementsInstanced']){
  const original=type.prototype[name];if(original)type.prototype[name]=function(...args){window.__bsaCalls++;return original.apply(this,args);};
 }
}
function bsaFrame(t){window.__bsaDraws.push({t,draws:window.__bsaCalls});window.__bsaCalls=0;if(window.__bsaDraws.length>10000)window.__bsaDraws.shift();requestAnimationFrame(bsaFrame);}requestAnimationFrame(bsaFrame);
addEventListener('DOMContentLoaded',()=>{const c=document.getElementById('unity-canvas');const container=document.getElementById('unity-container');if(container){container.style.width='1280px';container.style.height='720px';container.style.left='0';container.style.top='0';container.style.transform='none';}if(c){c.style.width='1280px';c.style.height='720px';}document.body.style.margin='0';});
`});
let messages=[],resolveSample;on('Runtime.consoleAPICalled',event=>{
 const line=event.args.map(a=>a.value??a.description??'').join(' ');messages.push(line);
 if(line.includes('[ArtPerf]')){const i=line.indexOf('{');if(i>=0)try{resolveSample?.(JSON.parse(line.slice(i)));}catch{}}
});
on('Runtime.exceptionThrown',e=>messages.push('EXCEPTION '+JSON.stringify(e.exceptionDetails)));
const results=[];
try{
 for(let repeat=1;repeat<=3;repeat++)for(const level of [1,29,50]){
  messages=[];let timeout;const sample=new Promise((resolve,reject)=>{resolveSample=resolve;timeout=setTimeout(()=>reject(Error('No ArtPerf sample within 120s')),120000);});
  const url=new URL(base);url.searchParams.set('level',level);url.searchParams.set('replay','R');url.searchParams.set('artperf','1');
  await call('Page.navigate',{url:url.href});await call('Page.bringToFront');
  const perf=await sample;clearTimeout(timeout);resolveSample=null;
  const metadata=await call('Runtime.evaluate',{expression:`(()=>{const c=document.getElementById('unity-canvas');const gl=c?.getContext('webgl2')||c?.getContext('webgl');const debug=gl?.getExtension('WEBGL_debug_renderer_info');const now=performance.now();const frames=window.__bsaDraws.filter(f=>f.t>now-10000&&f.draws>0);return {dpr:devicePixelRatio,userAgent:navigator.userAgent,renderer:debug?gl.getParameter(debug.UNMASKED_RENDERER_WEBGL):'',canvas:c?{width:c.width,height:c.height,cssWidth:c.clientWidth,cssHeight:c.clientHeight}:null,drawFrames:frames.length,meanDraws:frames.reduce((n,f)=>n+f.draws,0)/Math.max(1,frames.length),minDraws:Math.min(...frames.map(f=>f.draws)),maxDraws:Math.max(...frames.map(f=>f.draws))};})()`,returnByValue:true});
  const row={label,repeat,level,url:url.href,chrome:version.Browser,...perf,...metadata.result.value};results.push(row);
  await writeFile(`${out}/${label}-${level}-${repeat}.console.txt`,messages.join('\n'));
  const screenshot=await call('Page.captureScreenshot',{format:'png',clip:{x:0,y:0,width:1280,height:720,scale:1}});
  await writeFile(`${out}/${label}-${level}-${repeat}.png`,Buffer.from(screenshot.data,'base64'));
  await writeFile(`${out}/${label}.json`,JSON.stringify(results,null,2));
  console.log(JSON.stringify(row));
  await call('Page.navigate',{url:'about:blank'});
 }
}finally{await fetch(`http://localhost:${port}/json/close/${tab.id}`);ws.close();}
