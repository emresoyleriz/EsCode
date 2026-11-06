import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {createRequire} from 'node:module';
import {randomBytes} from 'node:crypto';
import net from 'node:net';
import {rmSync} from 'node:fs';
const require=createRequire(import.meta.url);
const {createBridge,sign,pipePath}=require('../desktop/host-bridge.cjs');
function healthy(secret,requestId){return {
  protocolVersion:1,requestId,type:'capabilities',mode:'host-read-only',
  hostConnected:true,auth:sign(secret,'response',requestId)
};}
async function withPipeServer(handler,callback){
  const name='escode-'+randomBytes(16).toString('hex');
  const server=net.createServer(socket=>{
    let input='';
    socket.on('data',chunk=>{
      input+=chunk.toString('utf8');
      const cut=input.indexOf('\n');
      if(cut>=0)handler(socket,JSON.parse(input.slice(0,cut)));
    });
  });
  const endpoint=pipePath(name);
  await new Promise((resolve,reject)=>{server.once('error',reject);server.listen(endpoint,resolve);});
  try{await callback(name);}
  finally{
    await new Promise(resolve=>server.close(resolve));
    if(process.platform!=='win32')rmSync(endpoint,{force:true});
  }
}
test('valid authenticated read-only response and no secret sent on pipe',async()=>{
  const secret=randomBytes(32);
  await withPipeServer((socket,req)=>{
    assert.deepEqual(Object.keys(req).sort(),['auth','protocolVersion','requestId','type']);
    assert.equal(req.protocolVersion,1);
    assert.equal(req.type,'capabilities');
    assert.equal(req.auth,sign(secret,'request',req.requestId));
    assert.ok(!JSON.stringify(req).includes(secret.toString('hex')));
    socket.end(JSON.stringify(healthy(secret,req.requestId))+'\n');
  },async name=>{
    assert.deepEqual(await createBridge({name,secret}).capabilities(),
      {mode:'host-read-only',protocolVersion:1,safeRenderer:true,hostConnected:true});
  });
});
test('forged Host response fails closed',async()=>{
  const secret=randomBytes(32);
  await withPipeServer((socket,req)=>{
    socket.end(JSON.stringify({...healthy(secret,req.requestId),auth:'0'.repeat(64)})+'\n');
  },async name=>await assert.rejects(createBridge({name,secret}).capabilities(),/authentication failed/));
});
test('mismatched/replayed request nonce rejected by client',async()=>{
  const secret=randomBytes(32);
  await withPipeServer((socket,req)=>{
    socket.end(JSON.stringify(healthy(secret,'a'.repeat(32)))+'\n');
  },async name=>await assert.rejects(createBridge({name,secret}).capabilities('b'.repeat(32)),/authentication failed/));
});
test('unexpected response command fields rejected',async()=>{
  const secret=randomBytes(32);
  await withPipeServer((socket,req)=>{
    socket.end(JSON.stringify({...healthy(secret,req.requestId),executeTool:true})+'\n');
  },async name=>await assert.rejects(createBridge({name,secret}).capabilities(),/authentication failed/));
});
test('oversized frames fail before parsing',async()=>{
  const secret=randomBytes(32);
  await withPipeServer(socket=>socket.write('x'.repeat(4098)),async name=>{
    await assert.rejects(createBridge({name,secret}).capabilities(),/too large/);
  });
});
test('stalled Host request times out',async()=>{
  const secret=randomBytes(32);
  await withPipeServer(()=>{},async name=>{
    await assert.rejects(createBridge({name,secret,timeoutMs:80}).capabilities(),/timeout/);
  });
});
test('invalid request IDs, pipe names and secrets rejected',async()=>{
  assert.throws(()=>pipePath('my-pipe'),/pipe name/);
  assert.throws(()=>createBridge({name:'escode-'+'a'.repeat(32),secret:Buffer.alloc(4)}),/session secret/);
  await assert.rejects(createBridge({name:'escode-'+'b'.repeat(32),secret:randomBytes(32)}).capabilities('bad'),/request ID/);
});

test('host IPC rejects a valid MAC bound to a different response mode',async()=>{
 const secret=randomBytes(32);
 await withPipeServer((socket,req)=>{
   socket.end(JSON.stringify({...healthy(secret,req.requestId),mode:'host-executable'})+'\n');
 },async name=>await assert.rejects(createBridge({name,secret}).capabilities(),/authentication failed/));
});
test('host IPC rejects extra response frames even when first frame is authenticated',async()=>{
 const secret=randomBytes(32);
 await withPipeServer((socket,req)=>{
   socket.end(JSON.stringify(healthy(secret,req.requestId))+'\n'+JSON.stringify(healthy(secret,req.requestId))+'\n');
 },async name=>await assert.rejects(createBridge({name,secret}).capabilities(),/multiple frames/));
});
test('host IPC rejects malformed JSON and unknown fields without returning capabilities',async()=>{
 const secret=randomBytes(32);
 for(const response of ['not-json\n',JSON.stringify({protocolVersion:1})+'\n']){
   await withPipeServer(socket=>socket.end(response),async name=>
     await assert.rejects(createBridge({name,secret}).capabilities(),/authentication failed/));
 }
});
test('host IPC accepts a fragmented single authenticated frame',async()=>{
 const secret=randomBytes(32);
 await withPipeServer((socket,req)=>{
   const wire=JSON.stringify(healthy(secret,req.requestId))+'\n';
   const half=Math.floor(wire.length/2);
   socket.write(wire.slice(0,half));
   socket.end(wire.slice(half));
 },async name=>assert.equal((await createBridge({name,secret}).capabilities()).hostConnected,true));
});
