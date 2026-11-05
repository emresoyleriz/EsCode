import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {createRequire} from 'node:module';
import {randomBytes} from 'node:crypto';
import net from 'node:net';
import {rmSync} from 'node:fs';
const require=createRequire(import.meta.url);
const {createBridge,modelSign,validateModelProbeInput,pipePath}=require('../desktop/host-bridge.cjs');

test('model catalog requests are limited to explicit loopback /v1 addresses',()=>{
 assert.equal(validateModelProbeInput('http://127.0.0.1:8000/v1','qwen2.5-coder-7b'),true);
 for(const url of ['https://example.com/v1','http://localhost:8000/v1',
 'http://127.0.0.1:8000/admin','http://127.0.0.1:8000/v1?secret=1',
 'http://127.0.0.1:8000/v1%2f','http://192.168.1.1:8000/v1'])
  assert.equal(validateModelProbeInput(url,'qwen2.5-coder-7b'),false,url);
 assert.equal(validateModelProbeInput('http://127.0.0.1:8000/v1','model|tamper'),false);
});
async function withServer(handler,run){
 const name='escode-'+randomBytes(16).toString('hex');
 const endpoint=pipePath(name);
 const server=net.createServer(socket=>{
  let raw='';
  socket.on('data',chunk=>{
   raw+=chunk.toString('utf8');
   const newline=raw.indexOf('\n');
   if(newline>=0)handler(socket,JSON.parse(raw.slice(0,newline)));
  });
 });
 await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(endpoint,resolve);});
 try{await run(name);}
 finally{
  await new Promise(resolve=>server.close(resolve));
  if(process.platform!=='win32')rmSync(endpoint,{force:true});
 }
}
test('signed model catalog replies are accepted but forged replies rejected',async()=>{
 const secret=randomBytes(32),url='http://127.0.0.1:8000/v1',model='qwen2.5-coder-7b';
 await withServer((socket,req)=>{
  assert.equal(req.type,'model-probe');
  assert.equal(req.baseUrl,url);
  assert.equal(req.model,model);
  assert.equal(req.auth,modelSign(secret,'request',req.requestId,url,model));
  const status='model-found',found=true;
  socket.end(JSON.stringify({protocolVersion:1,requestId:req.requestId,type:'model-probe',
   found,status,auth:modelSign(secret,'response',req.requestId,null,null,found,status)})+'\n');
 },async name=>{
  const result=await createBridge({name,secret}).probeModel(url,model);
  assert.deepEqual(result,{found:true,status:'model-found'});
 });
 await withServer((socket,req)=>{
  socket.end(JSON.stringify({protocolVersion:1,requestId:req.requestId,type:'model-probe',
    found:true,status:'model-found',auth:'0'.repeat(64)})+'\n');
 },async name=>await assert.rejects(
  createBridge({name,secret}).probeModel(url,model),/authentication failed/));
});
