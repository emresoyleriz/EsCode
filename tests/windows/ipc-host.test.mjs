import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {createRequire} from 'node:module';
import {fileURLToPath} from 'node:url';
import {dirname,resolve,join} from 'node:path';
import {randomBytes} from 'node:crypto';
import {spawn} from 'node:child_process';
import net from 'node:net';
const require=createRequire(import.meta.url);
const {startManagedHost,pipePath,sign,createBridge}=require('../../desktop/host-bridge.cjs');
const root=resolve(dirname(fileURLToPath(import.meta.url)),'../..');
async function rawRequest(name,request){
  return await new Promise((resolve,reject)=>{
    const socket=net.createConnection(pipePath(name)),parts=[];
    socket.setTimeout(4000,()=>socket.destroy(new Error('Host timeout')));
    socket.on('connect',()=>socket.write(request));
    socket.on('data',chunk=>parts.push(chunk));
    socket.on('error',reject);
    socket.on('end',()=>resolve(Buffer.concat(parts).toString('utf8')));
  });
}
async function untilReady(bridge){
  const stopAt=Date.now()+12000;
  while(Date.now()<stopAt){
    try{return await bridge.capabilities();}
    catch{await new Promise(r=>setTimeout(r,150));}
  }
  throw new Error('Real .NET Host failed to start');
}
test('real Windows .NET Host answers authenticated read-only capability query',
  {skip:process.platform!=='win32',timeout:30000},async()=>{
    const host=startManagedHost({projectRoot:root});
    assert.ok(host);
    try{
      assert.deepEqual(await host.capabilities(),{
        mode:'host-read-only',protocolVersion:1,safeRenderer:true,hostConnected:true
      });
    }finally{host.stop();}
  });
test('real Windows Host rejects wrong auth, replay, tool command and oversize',
  {skip:process.platform!=='win32',timeout:30000},async()=>{
    const name='escode-'+randomBytes(16).toString('hex'),secret=randomBytes(32);
    const dll=join(root,'src','host','EsCode.Host','bin','Release','net10.0','EsCode.Host.dll');
    const child=spawn('dotnet',[dll,'--serve-ipc'],{
      cwd:root,windowsHide:true,shell:false,stdio:['pipe','ignore','ignore']
    });
    child.stdin.on('error',()=>{});
    child.stdin.end(JSON.stringify({protocolVersion:1,pipeName:name,
      sessionSecret:secret.toString('hex'),parentPid:process.pid})+'\n');
    try{
      assert.equal((await untilReady(createBridge({name,secret}))).hostConnected,true);
      const id=randomBytes(16).toString('hex');
      const valid=JSON.stringify({protocolVersion:1,requestId:id,type:'capabilities',
        auth:sign(secret,'request',id)})+'\n';
      const first=JSON.parse((await rawRequest(name,valid)).trim());
      assert.equal(first.auth,sign(secret,'response',id));
      assert.equal(first.hostConnected,true);
      assert.equal(first.executeTool,undefined);
      assert.equal((await rawRequest(name,valid)).trim(),'DENIED');
      assert.equal((await rawRequest(name,JSON.stringify({protocolVersion:1,
        requestId:randomBytes(16).toString('hex'),type:'execute-tool',auth:'0'.repeat(64)})+'\n')).trim(),'DENIED');
      assert.equal((await rawRequest(name,JSON.stringify({protocolVersion:1,
        requestId:randomBytes(16).toString('hex'),type:'capabilities',auth:'0'.repeat(64)})+'\n')).trim(),'DENIED');
      // An oversized request is denied by an empty response or an early pipe reset.
      let oversizedReply='';
      try { oversizedReply=await rawRequest(name,'x'.repeat(4097)+'\n'); }
      catch(error) { if(!['EOF','EPIPE','ECONNRESET'].includes(error?.code))throw error; }
      assert.equal(oversizedReply,'');
      assert.equal((await createBridge({name,secret}).capabilities()).hostConnected,true);
    }finally{child.kill();}
  });
