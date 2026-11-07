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
function validateModelProbeInput(baseUrl,model){
  if(typeof baseUrl!=='string'||baseUrl.length>128||baseUrl.length===0||
    /[%\\|\r\n]/.test(baseUrl)||typeof model!=='string'||
    !/^[A-Za-z0-9][A-Za-z0-9_./:-]{0,127}$/.test(model))return false;
  try{
    const uri=new URL(baseUrl);
    return uri.protocol==='http:'&&['127.0.0.1','[::1]'].includes(uri.hostname)&&
      !uri.username&&!uri.password&&!uri.search&&!uri.hash&&
      (uri.pathname==='/v1'||uri.pathname==='/v1/')&&uri.port!=='';
  }catch{return false;}
}
function modelSign(secret,kind,requestId,baseUrl,model,found,status){
  const message=kind==='request'
    ? 'request|'+VERSION+'|'+requestId+'|model-probe|'+baseUrl+'|'+model
    : 'response|'+VERSION+'|'+requestId+'|model-probe|'+(found?'true':'false')+'|'+status;
  return createHmac('sha256',secret).update(message,'utf8').digest('hex');
}

function createBridge({name,secret,connect=net.createConnection,timeoutMs=1500,modelTimeoutMs=9000}){
  if(!Buffer.isBuffer(secret)||secret.length!==32)throw new Error('Invalid session secret');
  if(!Number.isSafeInteger(timeoutMs)||timeoutMs<1||timeoutMs>30000||
     !Number.isSafeInteger(modelTimeoutMs)||modelTimeoutMs<1||modelTimeoutMs>30000)
    throw new Error('Invalid request deadline');
  const endpoint=pipePath(name);
  let inFlight=0;
  async function bounded(action){
    if(inFlight>=4)throw new Error('Host request capacity exceeded');
    inFlight++;
    try{return await action();}finally{inFlight--;}
  }
  return Object.freeze({
    async capabilities(requestId=randomBytes(16).toString('hex')){
      if(typeof requestId!=='string'||!/^[a-f0-9]{32}$/.test(requestId))throw new Error('Invalid request ID');
      const request={protocolVersion:VERSION,requestId,type:RESPONSE_TYPE,
        auth:sign(secret,'request',requestId)};
      return await bounded(()=>new Promise((resolve,reject)=>{
        let socket,deadline,settled=false,raw=Buffer.alloc(0);
        const finish=(error,value)=>{
          if(settled)return;
          settled=true;
          clearTimeout(deadline);
          if(socket)socket.destroy();
          if(error)reject(error);else resolve(value);
        };
        try{socket=connect(endpoint);}
        catch{finish(new Error('Host connection unavailable'));return;}
        deadline=setTimeout(()=>finish(new Error('Host response timeout')),timeoutMs);
        socket.on('close',()=>finish(new Error('Host closed connection')));
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
      }));
    },
    async probeModel(baseUrl,model,requestId=randomBytes(16).toString('hex')){
      if(!validateModelProbeInput(baseUrl,model)||
         typeof requestId!=='string'||!/^[a-f0-9]{32}$/.test(requestId))
        throw new Error('Invalid local model probe');
      const req={protocolVersion:VERSION,requestId,type:'model-probe',baseUrl,model,
        auth:modelSign(secret,'request',requestId,baseUrl,model)};
      return await bounded(()=>new Promise((resolve,reject)=>{
        let socket,deadline,settled=false,raw=Buffer.alloc(0);
        const finish=(error,result)=>{
          if(settled)return;
          settled=true;
          clearTimeout(deadline);
          socket?.destroy();
          if(error)reject(error);else resolve(result);
        };
        try{socket=connect(endpoint);}
        catch{finish(new Error('Model Host unavailable'));return;}
        deadline=setTimeout(()=>finish(new Error('Model probe timeout')),modelTimeoutMs);
        socket.on('close',()=>finish(new Error('Model Host disconnected')));
        socket.on('error',()=>finish(new Error('Model Host unavailable')));
        socket.on('end',()=>finish(new Error('Model Host disconnected')));
        socket.on('connect',()=>socket.write(JSON.stringify(req)+'\n'));
        socket.on('data',data=>{
          if(settled)return;
          raw=Buffer.concat([raw,data]);
          if(raw.length>FRAME_LIMIT)return finish(new Error('Model Host frame too large'));
          const newline=raw.indexOf(10);
          if(newline<0)return;
          if(newline!==raw.length-1)return finish(new Error('Multiple Model Host frames'));
          try{
            const result=JSON.parse(raw.subarray(0,newline).toString('utf8'));
            const allowed=['model-found','model-not-listed','authorization-required','provider-unavailable'];
            if(!result||typeof result!=='object'||Array.isArray(result)||
               Object.keys(result).sort().join(',')!=='auth,found,protocolVersion,requestId,status,type'||
               result.protocolVersion!==VERSION||result.requestId!==requestId||
               result.type!=='model-probe'||typeof result.found!=='boolean'||
               !allowed.includes(result.status)||typeof result.auth!=='string'||
               !/^[a-f0-9]{64}$/.test(result.auth)||
               (result.found!==(result.status==='model-found')))
              throw new Error('Untrusted model response');
            const expected=Buffer.from(modelSign(secret,'response',requestId,null,null,
              result.found,result.status),'hex');
            const actual=Buffer.from(result.auth,'hex');
            if(actual.length!==expected.length||!timingSafeEqual(actual,expected))
              throw new Error('Invalid model response auth');
            finish(null,Object.freeze({found:result.found,status:result.status}));
          }catch{finish(new Error('Model response authentication failed'));}
        });
      }));
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
    async probeModel(baseUrl,model){
      if(stopped||!alive)return Object.freeze({found:false,status:'provider-unavailable'});
      try{return await bridge.probeModel(baseUrl,model);}
      catch{return Object.freeze({found:false,status:'provider-unavailable'});}
    },
    stop(){
      stopped=true;
      secret.fill(0);
      if(alive)child.kill();
    }
  });
}
module.exports={createBridge,startManagedHost,pipePath,sign,authenticatedResponse,modelSign,validateModelProbeInput};
