'use strict';
// Renderer URLs must never resolve outside the packaged dist directory, even
// through symlinks/reparse points. No filesystem path is accepted over IPC.
const fs=require('node:fs');
const path=require('node:path');
function within(root,file){
  const relative=path.relative(root,file);
  return relative!==''&&!relative.startsWith('..'+path.sep)&&relative!=='..'&&!path.isAbsolute(relative);
}
function resolveBundledAsset(root,pathname){
  if(typeof root!=='string'||typeof pathname!=='string'||pathname.length>2048)return null;
  try{
    const decoded=decodeURIComponent(pathname);
    if(!decoded.startsWith('/')||decoded.includes('\\')||decoded.includes('\0')||
       decoded.split('/').some(part=>part==='.'||part==='..'))return null;
    const realRoot=fs.realpathSync(root);
    const candidate=path.resolve(realRoot,'.'+decoded);
    if(!within(realRoot,candidate))return null;
    const realFile=fs.realpathSync(candidate);
    if(!within(realRoot,realFile)||!fs.statSync(realFile).isFile())return null;
    return realFile;
  }catch{return null;}
}
module.exports={resolveBundledAsset};
