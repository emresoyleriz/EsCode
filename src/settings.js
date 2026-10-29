export const STORAGE_KEY="escode.ui.settings.v2";
export const DEFAULT_SETTINGS=Object.freeze({version:2,theme:"dark",locale:"TR",density:"Comfortable",motion:false,agentMode:"docked",agentPinned:true,terminalOpen:true,agentWidth:320,agentX:280,agentY:96});
const between=(x,min,max)=>Number.isFinite(x)&&x>=min&&x<=max;
export function validateSettings(value){
 if(!value||typeof value!=="object"||Array.isArray(value)||value.version!==2)return null;
 const r={...DEFAULT_SETTINGS};
 if(["dark","light"].includes(value.theme))r.theme=value.theme;
 if(["TR","EN"].includes(value.locale))r.locale=value.locale;
 if(["Comfortable","Compact","Focus"].includes(value.density))r.density=value.density;
 if(typeof value.motion==="boolean")r.motion=value.motion;
 if(["docked","floating","hidden"].includes(value.agentMode))r.agentMode=value.agentMode;
 if(typeof value.agentPinned==="boolean")r.agentPinned=value.agentPinned;
 if(typeof value.terminalOpen==="boolean")r.terminalOpen=value.terminalOpen;
 if(between(value.agentWidth,240,640))r.agentWidth=value.agentWidth;
 if(between(value.agentX,240,10000))r.agentX=value.agentX;
 if(between(value.agentY,-2000,10000))r.agentY=value.agentY;
 return r;
}
export function readSettings(store=typeof localStorage!=="undefined"?localStorage:null){try{const raw=store?.getItem(STORAGE_KEY);if(!raw||raw.length>12000)return {...DEFAULT_SETTINGS};return validateSettings(JSON.parse(raw))??{...DEFAULT_SETTINGS};}catch{return {...DEFAULT_SETTINGS};}}
export function saveSettings(s,store=typeof localStorage!=="undefined"?localStorage:null){try{const v=validateSettings({...s,version:2});if(!v||!store)return false;store.setItem(STORAGE_KEY,JSON.stringify(v));return true;}catch{return false;}}
export function resetSettings(store=typeof localStorage!=="undefined"?localStorage:null){try{store?.removeItem(STORAGE_KEY);}catch{}return {...DEFAULT_SETTINGS};}
