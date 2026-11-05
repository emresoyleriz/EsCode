// Pure Monaco language dispatch. No file I/O, executable association or LSP launch.
const extensionModes = Object.freeze({
  cs:'csharp',csx:'csharp',js:'javascript',jsx:'javascript',mjs:'javascript',
  cjs:'javascript',ts:'typescript',tsx:'typescript',json:'json',jsonc:'json',
  py:'python',sql:'sql',md:'markdown',markdown:'markdown',yaml:'yaml',
  yml:'yaml',xml:'xml',html:'html',htm:'html',css:'css',scss:'scss',
  less:'less',ps1:'powershell',sh:'shell',bat:'bat',ini:'ini'
});
export function languageForFile(file){
  if(typeof file!=='string'||file.length===0||file.length>1024)return 'plaintext';
  const name=file.replaceAll('\\','/').split('/').pop()?.toLowerCase()??'';
  if(name==='dockerfile')return 'dockerfile';
  const ext=name.includes('.')?name.split('.').pop():'';
  return Object.prototype.hasOwnProperty.call(extensionModes,ext)
    ?extensionModes[ext]:'plaintext';
}
