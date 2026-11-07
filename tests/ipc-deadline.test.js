import {test} from 'node:test';
import assert from 'node:assert/strict';
import {EventEmitter} from 'node:events';
import {createRequire} from 'node:module';
const {createBridge,sign,modelSign}=createRequire(import.meta.url)('../desktop/host-bridge.cjs');
const name='escode-'+'a'.repeat(32),secret=Buffer.alloc(32,1);
function socket(handler){
 const s=new EventEmitter();s.destroyed=false;
 s.destroy=()=>{s.destroyed=true;};
 s.write=wire=>handler(s,JSON.parse(wire));
 // Traffic intentionally never triggers an inactivity timeout.
 s.setTimeout=()=>{};
 queueMicrotask(()=>s.emit('connect'));
 return s;
}
for(const model of [false,true]){
 const request=b=>model?b.probeModel('http://127.0.0.1:8000/v1','coder'):b.capabilities();
 test(`${model?'model':'capability'} deadline rejects continuous partial traffic`,async()=>{
  let interval,s;
  const bridge=createBridge({name,secret,timeoutMs:40,modelTimeoutMs:40,connect:()=>s=socket(sock=>{
   interval=setInterval(()=>sock.emit('data',Buffer.from(' ')),2);
  })});
  try{await assert.rejects(request(bridge),/timeout/);assert.equal(s.destroyed,true);}
  finally{clearInterval(interval);}
 });
 test(`${model?'model':'capability'} abrupt close rejects immediately`,async()=>{
  const bridge=createBridge({name,secret,connect:()=>socket(s=>s.emit('close'))});
  await assert.rejects(request(bridge),/closed|disconnected/);
 });
 test(`${model?'model':'capability'} authenticated response survives transport deadline cleanup`,async()=>{
  const bridge=createBridge({name,secret,timeoutMs:40,modelTimeoutMs:40,connect:()=>socket((s,r)=>{
   const reply=model?{protocolVersion:1,requestId:r.requestId,type:'model-probe',found:true,status:'model-found',auth:modelSign(secret,'response',r.requestId,null,null,true,'model-found')}
    :{protocolVersion:1,requestId:r.requestId,type:'capabilities',mode:'host-read-only',hostConnected:true,auth:sign(secret,'response',r.requestId)};
   s.emit('data',Buffer.from(JSON.stringify(reply)+'\n'));
   s.emit('close');
  })});
  const result=await request(bridge);assert.equal(model?result.found:result.hostConnected,true);
 });
}
test('invalid deadlines cannot disable request bounds',()=>{
 for(const value of [0,-1,NaN,Infinity,30001,1.5])
  assert.throws(()=>createBridge({name,secret,timeoutMs:value}),/deadline/);
});
test('shared request capacity bounds model and capability traffic and releases on close',async()=>{
 const sockets=[];
 const bridge=createBridge({name,secret,connect:()=>{const s=socket(()=>{});sockets.push(s);return s;}});
 const pending=Array.from({length:4},()=>assert.rejects(bridge.capabilities(),/closed/));
 await assert.rejects(bridge.capabilities(),/capacity/);
 await assert.rejects(bridge.probeModel('http://127.0.0.1:8000/v1','coder'),/capacity/);
 assert.equal(sockets.length,4);
 sockets.forEach(s=>s.emit('close'));await Promise.all(pending);
 const next=assert.rejects(bridge.capabilities(),/closed/);
 sockets.at(-1).emit('close');await next;assert.equal(sockets.length,5);
});
