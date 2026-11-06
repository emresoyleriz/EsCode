import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const {resolveBundledAsset}=require('../desktop/asset-guard.cjs');
test('packaged asset resolver accepts only real files inside dist',t=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'escode-assets-'));
 t.after(()=>fs.rmSync(root,{recursive:true,force:true}));
 const dist=path.join(root,'dist');fs.mkdirSync(dist);
 const inside=path.join(dist,'index.html');fs.writeFileSync(inside,'ok');
 const outside=path.join(root,'private.txt');fs.writeFileSync(outside,'secret');
 assert.equal(resolveBundledAsset(dist,'/index.html'),fs.realpathSync(inside));
 for(const attempt of ['/../private.txt','/%2e%2e/private.txt','/%2e/index.html',
   '/%5cprivate.txt','/missing.txt','/%00index.html','//../private.txt']){
   assert.equal(resolveBundledAsset(dist,attempt),null,attempt);
 }
 assert.equal(resolveBundledAsset(dist,'/'),null);
 const link=path.join(dist,'leak.txt');
 try{fs.symlinkSync(outside,link,'file');}
 catch(e){if(process.platform==='win32'&&['EPERM','EACCES'].includes(e.code))return;throw e;}
 assert.equal(resolveBundledAsset(dist,'/leak.txt'),null,'symlink escape');
});
