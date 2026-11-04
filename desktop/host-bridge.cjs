'use strict';
// Electron MAIN ONLY. No arbitrary IPC or renderer access to this module.
const {createHmac,randomBytes,timingSafeEqual}=require('node:crypto');
const {spawn}=require('node:child_process');
const fs=require('node:fs');
const net=require('node:net');
const os=require('node:os');
const path=require('node:path');
const VERSION=1,FRAME_LIMIT=4096,RESPONSE_TYPE='capabilities',MODE='host-read-only';
function pipePath(name){
  if(!/^escode-[a-f0-9]{32}$/.test(name))throw new Error('Invalid pipe name');
  return process.platform==='win32'?'\\\\.\\pipe\\'+name:path.join(os.tmpdir(),name+'.sock');
}
function sign(secret,kind,requestId){
  const message=kind==='request'
    ? 'request|'+VERSION+'|'+requestId+'|'+RESPONSE_TYPE
    : 'response|'+VERSION+'|'+requestId+'|'+MODE+'|true';
  return createHmac('sha256',secret).update(message,'utf8').digest('hex');
}
function authenticatedResponse(message,secret,requestId){
  if(!message||typeof message!=='object'||Array.isArray(message)||
      Object.keys(message).sort().join(',')!=='auth,hostConnected,mode,protocolVersion,requestId,type'||
      message.protocolVersion!==VERSION||message.type!==RESPONSE_TYPE||
      message.requestId!==requestId||message.hostConnected!==true||message.mode!==MODE||
      typeof message.auth!=='string'||!/^[a-f0-9]{64}$/.test(message.auth))return false;
  const expected=Buffer.from(sign(secret,'response',requestId),'hex');
  const actual=Buffer.from(message.auth,'hex');
  return actual.length===expected.length&&timingSafeEqual(expected,actual);
}
function createBridge({name,secret,connect=net.createConnection,timeoutMs=1500}){
  if(!Buffer.isBuffer(secret)||secret.length!==32)throw new Error('Invalid session secret');
  const endpoint=pipePath(name);
  return Object.freeze({
    async capabilities(requestId=randomBytes(16).toString('hex')){
      if(typeof requestId!=='string'||!/^[a-f0-9]{32}$/.test(requestId))throw new Error('Invalid request ID');
      const request={protocolVersion:VERSION,requestId,type:RESPONSE_TYPE,
        auth:sign(secret,'request',requestId)};
      return await new Promise((resolve,reject)=>{
        let socket,settled=false,raw=Buffer.alloc(0);
        const finish=(error,value)=>{
          if(settled)return;
          settled=true;
          if(socket)socket.destroy();
          if(error)reject(error);else resolve(value);
        };
        try{socket=connect(endpoint);}
        catch{finish(new Error('Host connection unavailable'));return;}
        socket.setTimeout(timeoutMs,()=>finish(new Error('Host response timeout')));
        socket.on('error',()=>finish(new Error('Host connection unavailable')));
        socket.on('end',()=>finish(new Error('Host closed connection')));
        socket.on('connect',()=>socket.write(JSON.stringify(request)+'\n'));
        socket.on('data',data=>{
          if(settled)return;
          raw=Buffer.concat([raw,data]);
          if(raw.length>FRAME_LIMIT)return finish(new Error('Host frame too large'));
          const cut=raw.indexOf(10);
          if(cut<0)return;
          if(cut!==raw.length-1)return finish(new Error('Host sent multiple frames'));
          try{
            const response=JSON.parse(raw.subarray(0,cut).toString('utf8'));
            if(!authenticatedResponse(response,secret,requestId))throw new Error('Host response authentication failed');
            finish(null,Object.freeze({mode:MODE,protocolVersion:VERSION,safeRenderer:true,hostConnected:true}));
          }catch{finish(new Error('Host response authentication failed'));}
        });
      });
    }
  });
}
function startManagedHost({projectRoot=path.resolve(__dirname,'..'),dotnet='dotnet',spawnProcess=spawn}={}){
  if(process.platform!=='win32')return null;
  const name='escode-'+randomBytes(16).toString('hex');
  const secret=randomBytes(32);
  const dll=path.join(projectRoot,'src','host','EsCode.Host','bin','Release','net10.0','EsCode.Host.dll');
  const csproj=path.join(projectRoot,'src','host','EsCode.Host','EsCode.Host.csproj');
  const argv=fs.existsSync(dll)?[dll,'--serve-ipc']
    :['run','--no-launch-profile','--project',csproj,'--configuration','Release','--','--serve-ipc'];
  // No shell. The secret is supplied via stdin, NOT argv/environment/logs.
  const child=spawnProcess(dotnet,argv,{cwd:projectRoot,shell:false,windowsHide:true,stdio:['pipe','ignore','ignore']});
  let alive=true,stopped=false;
  child.on('error',()=>{alive=false;});
  child.on('exit',()=>{alive=false;});
  child.stdin.on('error',()=>{alive=false;});
  child.stdin.end(JSON.stringify({protocolVersion:VERSION,pipeName:name,
    sessionSecret:secret.toString('hex'),parentPid:process.pid})+'\n');
  const bridge=createBridge({name,secret});
  return Object.freeze({
    async capabilities(){
      const deadline=Date.now()+12000;
      while(Date.now()<deadline){
        if(stopped||!alive)break;
        try{return await bridge.capabilities();}catch{/* Host may still be booting. */}
        await new Promise(resolve=>setTimeout(resolve,250));
      }
      return null;
    },
    stop(){
      stopped=true;
      secret.fill(0);
      if(alive)child.kill();
    }
  });
}
module.exports={createBridge,startManagedHost,pipePath,sign,authenticatedResponse};
