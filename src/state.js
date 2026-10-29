export const NAV=[
{id:"welcome",name:"Welcome Hub",icon:"◈",group:"workspace"},
{id:"ide",name:"Workspace",icon:"⌘",group:"workspace"},
{id:"agents",name:"Agent Studio",icon:"✧",group:"ai"},
{id:"tasks",name:"Task Center",icon:"▤",group:"ai"},
{id:"review",name:"Git & Review",icon:"⑂",group:"development"},
{id:"intelligence",name:"Code Intelligence",icon:"◇",group:"development"},
{id:"approvals",name:"Approval Center",icon:"⬡",group:"ai"},
{id:"models",name:"Model Studio",icon:"◉",group:"ai"},
{id:"mcp",name:"MCP Manager",icon:"⬢",group:"tools"},
{id:"terminal",name:"Terminal & Debugger",icon:">_",group:"tools"},
{id:"browser",name:"Browser Automation",icon:"◎",group:"tools"},
{id:"settings",name:"Settings & Themes",icon:"⚙",group:"workspace"},
{id:"recovery",name:"Recovery & Insights",icon:"▥",group:"tools"}
];
export const SCREENS=Object.fromEntries(NAV.map(x=>[x.id,x]));
export const PROFILES=["Comfortable","Compact","Focus"];
export function createState(){return {page:"ide",theme:"dark",density:"Comfortable",agentPanel:true,terminalPanel:true,agentPinned:true,locale:"TR",searchOpen:false,selectedFile:"WorkspaceService.cs",dirty:false,role:"Supervised",reducedMotion:false,notice:"",command:"",layoutVersion:1}}
export function reducer(s,a){switch(a.type){
case "NAVIGATE": return SCREENS[a.page]?{...s,page:a.page,searchOpen:false,notice:""}:s;
case "THEME": return ["dark","light"].includes(a.value)?{...s,theme:a.value}:s;
case "DENSITY": return PROFILES.includes(a.value)?{...s,density:a.value}:s;
case "PANEL": return a.panel==="agent"?{...s,agentPanel:!s.agentPanel}:{...s,terminalPanel:!s.terminalPanel};
case "PIN": return {...s,agentPinned:!s.agentPinned};
case "PALETTE": return {...s,searchOpen:!s.searchOpen};
case "FILE": return {...s,selectedFile:a.value};
case "DIRTY":return {...s,dirty:!!a.value};
case "MOTION":return {...s,reducedMotion:!!a.value};
case "LOCALE":return {...s,locale:a.value==="EN"?"EN":"TR"};
case "NOTICE":return {...s,notice:a.text};
case "ROLE":return {...s,role:["Read-only","Ask","Supervised"].includes(a.value)?a.value:s.role};
default:return s;}}
export const DESIGN={dark:{bg:"#0c1120",surface:"#161d2e",raised:"#202b42",accent:"#796cf4",text:"#f2f5fc",muted:"#a6b2cb"},light:{bg:"#f6f8fc",surface:"#ffffff",raised:"#e9edf7",accent:"#6256d9",text:"#17213b",muted:"#61708a"}};
export function validateLayout(input){if(!input||input.version!==1||!PROFILES.includes(input.density)||typeof input.agentPanel!=="boolean"||typeof input.terminalPanel!=="boolean")return null;return {density:input.density,agentPanel:input.agentPanel,terminalPanel:input.terminalPanel};}
export function layoutSnapshot(s){return {version:1,density:s.density,agentPanel:s.agentPanel,terminalPanel:s.terminalPanel};}