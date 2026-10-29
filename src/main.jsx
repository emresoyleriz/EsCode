import React,{useState,useEffect,useRef,Suspense} from "react";
import {createRoot} from "react-dom/client";
import {NAV} from "./state.js";
import {t} from "./i18n.js";
import {DEFAULT_SETTINGS,readSettings,saveSettings,resetSettings} from "./settings.js";
import "./styles.css";
import "./monaco.js";
import "./desktop.css";
import "./generated-tokens.css";
const Monaco=React.lazy(()=>import("@monaco-editor/react"));

const seed={
"WorkspaceService.cs":`using System.Threading;
namespace EsCode.Application;

public sealed class WorkspaceService
{
    public async Task<string> OpenAsync(string name, CancellationToken ct)
    {
        await Task.Yield();
        return $"Workspace: {name}";
    }
}`,
"AgentOrchestrator.cs":`namespace EsCode.Agents;

public sealed class AgentOrchestrator
{
    // Demo buffer: execution is not connected.
    public Task PlanAsync(CancellationToken ct) => Task.CompletedTask;
}`,
"ToolBroker.cs":`namespace EsCode.Security;

public sealed class ToolBroker
{
    // Actual OS authorization requires a verified .NET broker.
    public bool RequiresApproval => true;
}`,
"appsettings.json":`{ "EsCode": { "Stage": "Prototype", "Autonomous": false } }`
};
const TASKS=["Analyze","Plan","Approve","Implement","Test","Review"];
function App(){
 const [prefs,setPrefs]=useState(readSettings);
 const [page,setPage]=useState("ide");
 const [query,setQuery]=useState("");
 const [palette,setPalette]=useState(false);
 const [selectedFile,setSelectedFile]=useState("WorkspaceService.cs");
 const [buffers,setBuffers]=useState(seed);
 const [notice,setNotice]=useState("");
 const [role,setRole]=useState("Supervised");
 const [prompt,setPrompt]=useState("");
 const [ipcStatus,setIpcStatus]=useState("browser-preview");
 const lastFocus=useRef(null);
 const [floatPosition,setFloatPosition]=useState(null);
 const dragging=useRef(null);
 const panelResize=useRef(null);
 const dictionary=(key)=>t(prefs.locale,key);
 const change=(field,value)=>setPrefs(prev=>({...prev,[field]:value}));
 const navigate=(id)=>{if(NAV.some(n=>n.id===id)){setPage(id);setPalette(false);setQuery("");}};
 const openPalette=()=>{lastFocus.current=document.activeElement;setQuery("");setPalette(true);};
 const closePalette=()=>{setPalette(false);requestAnimationFrame(()=>lastFocus.current?.focus?.());};
 useEffect(()=>{saveSettings(prefs)},[prefs]);
 useEffect(()=>{document.documentElement.lang=prefs.locale==="TR"?"tr":"en";document.title="EsCode — "+dictionary(page)},[prefs.locale,page]);
 useEffect(()=>{const handle=e=>{
   if((e.ctrlKey||e.metaKey)&&e.key.toLowerCase()==="k"){e.preventDefault();if(palette)closePalette();else openPalette();}
   if(e.key==="Escape"&&palette){e.preventDefault();closePalette();}
 };document.addEventListener("keydown",handle);return()=>document.removeEventListener("keydown",handle);},[palette]);
 useEffect(()=>{let active=true;window.escode?.capabilities?.().then(s=>{if(active&&s?.mode)setIpcStatus(s.mode)}).catch(()=>{});return()=>{active=false}},[]);
 const onSelectFile=(name)=>{setSelectedFile(name);setPage("ide");};
 const dirty=buffers[selectedFile]!==seed[selectedFile];
 const agentWidth=prefs.agentWidth;
 const startResize=e=>{if(e.button!==0)return;panelResize.current={x:e.clientX,width:agentWidth};e.currentTarget.setPointerCapture(e.pointerId);};
 const resize=e=>{if(!panelResize.current)return;const delta=panelResize.current.x-e.clientX;change("agentWidth",Math.max(240,Math.min(640,panelResize.current.width+delta)));};
 const endResize=()=>{panelResize.current=null};
 const startDrag=e=>{if(prefs.agentPinned||prefs.agentMode!=="floating"||e.button!==0)return;dragging.current={x:e.clientX,y:e.clientY,initialX:floatPosition?.x??prefs.agentX,initialY:floatPosition?.y??prefs.agentY};e.currentTarget.setPointerCapture(e.pointerId);};
 const moveDrag=e=>{if(!dragging.current)return;const {x,y,initialX,initialY}=dragging.current;const navRight=document.querySelector(".sidebar")?.getBoundingClientRect().right??0;const minX=Math.min(window.innerWidth-240,navRight+8);setFloatPosition({x:Math.max(minX,Math.min(window.innerWidth-240,initialX+e.clientX-x)),y:Math.max(60,Math.min(window.innerHeight-180,initialY+e.clientY-y))});};
 const endDrag=()=>{if(dragging.current&&floatPosition){setPrefs(p=>({...p,agentX:floatPosition.x,agentY:floatPosition.y}));}dragging.current=null;};
 const dot=<span className="round-led" aria-label={dictionary("offline")}/>;
 const preview=()=>setNotice(dictionary("mock"));
 const tile=(title,subtitle,children)=><section className="tile"><div className="tile-heading"><div><h3>{title}</h3><p>{subtitle}</p></div><span aria-hidden="true" className="more">···</span></div>{children}</section>;
 const tag=(label,kind="")=><span className={`pill ${kind}`}>{label}</span>;
 const empty=(icon,title,details)=><div className="empty-state"><div className="empty-symbol" aria-hidden="true">{icon}</div><h2>{title}</h2><p>{details}</p></div>;
 const miniNotice=(heading,description)=><div className="warning-box"><b>{heading}</b><p>{description}</p></div>;
 function content(){
  if(page==="ide")return <div className="ide-work">
    <aside className="filetree" aria-label={dictionary("fileTree")}><div className="section-label">{dictionary("fileTree")}</div><div className="tree-row open">⌄ &nbsp; ESCODE</div><div className="tree-row indent">⌄ &nbsp; src</div>{Object.keys(seed).map(name=><button key={name} className={`tree-row indent2 ${selectedFile===name?"selected":""}`} aria-current={selectedFile===name?"page":undefined} onClick={()=>onSelectFile(name)}>⌑ &nbsp; {name}{buffers[name]!==seed[name]?" •":""}</button>)}</aside>
    <div className="editor">
      <div className="tabs"><div className="active-tab"><span className="dot-code">●</span>{selectedFile}{dirty?" •":""}</div></div>
      <div className="editor-toolbar"><span>src / {selectedFile} {dirty&&tag(dictionary("dirty"),"warn")}</span><span>{selectedFile.endsWith(".json")?"JSON":"C#"} · UTF-8</span></div>
      <div className="monaco-container" aria-label={dictionary("codeTitle")}><Suspense fallback={<div className="editor-loading">Monaco Editor…</div>}>
        <Monaco key={selectedFile} path={selectedFile} language={selectedFile.endsWith(".json")?"json":"csharp"} theme={prefs.theme==="dark"?"vs-dark":"light"} value={buffers[selectedFile]} options={{fontSize:13,minimap:{enabled:false},automaticLayout:true,scrollBeyondLastLine:false,wordWrap:"off",fontLigatures:true,readOnly:false,accessibilitySupport:"auto"}} onChange={v=>setBuffers(p=>({...p,[selectedFile]:v??""}))}/>
      </Suspense></div>
      {prefs.terminalOpen&&<div className="terminal"><div className="terminal-head"><span>{dictionary("terminalTitle")}</span><button onClick={()=>change("terminalOpen",false)} aria-label={dictionary("close")}>×</button></div><div className="terminal-body"><span className="term-dim">PS C:\EsCode&gt;</span> dotnet test<br/><span className="green">✓</span> {dictionary("mock")}<br/><span className="term-dim">PS C:\EsCode&gt; _</span></div></div>}
    </div></div>;
  if(page==="welcome")return <div className="cards-grid">{tile(dictionary("welcomeTitle"),dictionary("prototype"),<><div className="hero-icon">⌘</div><button className="primary" onClick={()=>navigate("ide")}>{dictionary("openWorkspace")} →</button></>)}{tile(dictionary("recent"),"EsCode",<div className="list-row">◇ &nbsp; EsCode <span>{dictionary("prototype")}</span></div>)}{tile(dictionary("restore"),dictionary("noConnection"),<><p>{dictionary("restoreHelp")}</p><button className="secondary" onClick={()=>navigate("recovery")}>{dictionary("recovery")} →</button></>)}</div>;
  if(page==="agents")return <div className="cards-grid two">{tile(dictionary("agents"),dictionary("noConnection"),["Architect","Developer","Reviewer"].map(x=><div className="agent-row" key={x}>✧ {x} {tag(dictionary("offline"))}</div>))}{tile(dictionary("taskFlow"),dictionary("taskHelp"),<div className="timeline">{TASKS.map(step=><div key={step}>○ &nbsp; {step}</div>)}</div>)}{tile(dictionary("newTask"),dictionary("prototype"),<><textarea className="prompt" value={prompt} onChange={e=>setPrompt(e.target.value)} aria-label={dictionary("newTask")} placeholder={dictionary("agentAsk")}/><button className="primary" onClick={preview}>{dictionary("newTask")} →</button></>)}</div>;
  if(page==="tasks")return <div className="cards-grid two">{tile(dictionary("taskFlow"),dictionary("taskHelp"),<div className="pipeline">{TASKS.map(x=><span key={x} className="step">○ {x}</span>)}</div>)}{tile(dictionary("tasks"),dictionary("noConnection"),<div className="metric">0 <small>{dictionary("noData")}</small></div>)}{tile(dictionary("unknown"),dictionary("noConnection"),miniNotice(dictionary("unknown"),dictionary("retryWarning")))}</div>;
  if(page==="settings")return <div className="cards-grid two">{tile(dictionary("settingsInfo"),"Midnight Glass / Pearl Light",<>
   <div className="field-row"><label htmlFor="theme">{dictionary("theme")}</label><select id="theme" value={prefs.theme} onChange={e=>change("theme",e.target.value)}><option value="dark">{dictionary("dark")}</option><option value="light">{dictionary("light")}</option></select></div>
   <div className="field-row"><label htmlFor="density">{dictionary("density")}</label><select id="density" value={prefs.density} onChange={e=>change("density",e.target.value)}>{["Comfortable","Compact","Focus"].map((v,i)=><option key={v} value={v}>{dictionary(["comfortable","compact","focus"][i])}</option>)}</select></div>
   <div className="field-row"><label htmlFor="locale">{dictionary("language")}</label><select id="locale" value={prefs.locale} onChange={e=>change("locale",e.target.value)}><option value="TR">Türkçe</option><option value="EN">English</option></select></div>
   <div className="field-row"><label htmlFor="agentMode">{dictionary("panelMode")}</label><select id="agentMode" value={prefs.agentMode} onChange={e=>change("agentMode",e.target.value)}><option value="docked">{dictionary("docked")}</option><option value="floating">{dictionary("floating")}</option><option value="hidden">{dictionary("hidden")}</option></select></div>
   <label className="toggle"><input type="checkbox" checked={prefs.motion} onChange={e=>change("motion",e.target.checked)}/>{dictionary("motion")}</label>
  </>)}{tile(dictionary("settings"),dictionary("layoutPersisted"),<><p>{dictionary("layoutPersisted")}</p><button className="secondary" onClick={()=>{setPrefs(resetSettings());setFloatPosition(null)}}>{dictionary("layoutReset")}</button></>)}</div>;
  if(page==="models")return <div className="cards-grid two">{tile(dictionary("modelTitle"),dictionary("noConnection"),<div className="agent-row">{dictionary("localModel")}{tag(dictionary("notConfigured"))}</div>)}{tile(dictionary("modelTitle"),dictionary("notVerified"),[dictionary("toolCalling"),dictionary("structured"),dictionary("context")].map(x=><div className="agent-row" key={x}>{x}{tag(dictionary("notVerified"),"warn")}</div>))}</div>;
  if(page==="approvals")return <div className="cards-grid two">{tile(dictionary("approvals"),dictionary("scope"),empty("⬡",dictionary("approvalTitle"),dictionary("approvalHelp")))}{tile(dictionary("role"),dictionary("prototype"),["Read-only","Ask","Supervised"].map(x=><div className="agent-row" key={x}>{x}{tag("MVP")}</div>))}</div>;
  if(page==="review")return <div className="cards-grid two">{tile(dictionary("review"),dictionary("noConnection"),empty("⑂",dictionary("noData"),dictionary("gitHelp")))}{tile(dictionary("review"),dictionary("prototype"),miniNotice(dictionary("role"),dictionary("gitHelp")))}</div>;
  if(page==="recovery")return <div className="cards-grid two">{tile(dictionary("recovery"),dictionary("noConnection"),empty("▥",dictionary("recoveryTitle"),dictionary("recoveryHelp")))}{tile(dictionary("unknown"),dictionary("noConnection"),miniNotice(dictionary("unknown"),dictionary("retryWarning")))}</div>;
  if(page==="intelligence")return <div className="cards-grid two">{tile(dictionary("intelligence"),dictionary("noConnection"),<><input className="search-input" aria-label={dictionary("intelligence")} placeholder={dictionary("command")}/><p>{dictionary("notConnected")}</p></>)}{tile(dictionary("context"),dictionary("notVerified"),<div className="agent-row">{dictionary("notVerified")}</div>)}</div>;
  if(page==="mcp")return <div className="cards-grid two">{tile(dictionary("mcp"),dictionary("noConnection"),empty("⬢",dictionary("noData"),dictionary("notConnected")))}{tile(dictionary("scope"),dictionary("notVerified"),miniNotice(dictionary("notVerified"),dictionary("notConnected")))}</div>;
  if(page==="terminal")return <div className="cards-grid two">{tile(dictionary("terminalTitle"),dictionary("prototype"),<pre className="mock-term">PS C:\EsCode&gt; _{"\n"}{dictionary("mock")}</pre>)}{tile("Agent Terminal",dictionary("offline"),<pre className="mock-term">{dictionary("notConnected")}</pre>)}</div>;
  return <div className="cards-grid two">{tile(dictionary("browser"),dictionary("noConnection"),empty("◎",dictionary("notConnected"),dictionary("mock")))}{tile(dictionary("scope"),dictionary("notVerified"),miniNotice(dictionary("notVerified"),dictionary("notConnected")))}</div>;
 }
 const menu= NAV.filter(n=>dictionary(n.id).toLocaleLowerCase(prefs.locale==="TR"?"tr-TR":"en-US").includes(query.toLocaleLowerCase(prefs.locale==="TR"?"tr-TR":"en-US")));
 const float=prefs.agentMode==="floating";
 const agent=<aside className={`agent-panel ${float?"agent-floating":""}`} style={{width:agentWidth,...(float?{left:floatPosition?.x??prefs.agentX,top:floatPosition?.y??prefs.agentY}:{})}} aria-label={dictionary("agentTitle")}>
  <div className={`panel-title ${float&&!prefs.agentPinned?"draggable":""}`} onPointerDown={startDrag} onPointerMove={moveDrag} onPointerUp={endDrag}>
    <span className="star">✧</span> {dictionary("agentTitle")}
    <span className="right"><button title={prefs.agentPinned?dictionary("unpin"):dictionary("pin")} aria-pressed={prefs.agentPinned} onClick={()=>change("agentPinned",!prefs.agentPinned)}>{prefs.agentPinned?"◉":"○"}</button><button onClick={()=>change("agentMode","hidden")} aria-label={dictionary("close")}>×</button></span>
  </div>
  <div className="agent-content"><div className="assistant-symbol">✧</div><h2>{dictionary("agentWelcome")}</h2><p>{dictionary("agentDescription")}</p>
  <div className="welcome-chips">{[dictionary("plan"),dictionary("verify"),dictionary("test")].map(x=><button onClick={preview} key={x}>{x}</button>)}</div>
  <div className="message"><span className="muted">{dictionary("noConnection")}</span><p>{dictionary("agentOffline")}</p></div></div>
  <div className="composer"><textarea value={prompt} onChange={e=>setPrompt(e.target.value)} aria-label={dictionary("agentAsk")} placeholder={dictionary("agentAsk")}/><div className="composer-actions"><span>Ctrl K · {role}</span><button className="send" aria-label={dictionary("send")} onClick={preview}>↑</button></div></div>
 </aside>;
 return <div className={`shell theme-${prefs.theme} density-${prefs.density.toLowerCase()} ${prefs.motion?"reduce-motion":""}`}>
  <aside className="sidebar"><div className="brand"><div className="logo">✧</div><div><strong>EsCode</strong><small>DEVELOPMENT STUDIO</small></div></div>
  <div className="side-nav">{NAV.map(n=><button key={n.id} className={`nav-item ${page===n.id?"active":""}`} data-page={n.id} title={dictionary(n.id)} aria-label={dictionary(n.id)} onClick={()=>navigate(n.id)}><span className="nav-icon">{n.icon}</span><span className="nav-label">{dictionary(n.id)}</span></button>)}</div>
  <div className="sidebar-bottom"><span className="avatar">ES</span><div><b>EsCode</b><small>{dictionary("prototype")}</small></div></div></aside>
  <div className="main"><header className="topbar"><div className="breadcrumbs"><span>EsCode</span><span className="separator">/</span><strong>{dictionary(page)}</strong></div>
   <div className="top-actions"><button className="cmd-trigger" aria-label={dictionary("openPalette")} onClick={openPalette}>{dictionary("command")} <kbd>Ctrl K</kbd></button>{dot}<button className="icon-btn" aria-label={dictionary("toggleAgent")} onClick={()=>change("agentMode",prefs.agentMode==="hidden"?"docked":"hidden")}>✧</button><button className="icon-btn" onClick={()=>navigate("settings")} aria-label={dictionary("settings")}>⚙</button></div></header>
   <div className="titlebar"><div><div className="eyebrow">STUDIO / {dictionary(page).toUpperCase()}</div><h1>{dictionary(page)}</h1><div className="subtitle">{dictionary("tagline")}</div></div><div className="title-actions">{tag(role)}<button className="secondary" onClick={preview}>＋ {dictionary("newTask")}</button></div></div>
   <div className="workspace-wrap">
    <main id="main-content" className={`workspace ${page==="ide"?"editor-workspace":""}`}>{content()}</main>
    {prefs.agentMode!=="hidden"&&<>{!float&&<div className="agent-resize" role="separator" tabIndex={0} aria-label="Resize agent panel" aria-orientation="vertical" onPointerDown={startResize} onPointerMove={resize} onPointerUp={endResize} onKeyDown={e=>{if(e.key==="ArrowLeft")change("agentWidth",Math.min(640,agentWidth+20));if(e.key==="ArrowRight")change("agentWidth",Math.max(240,agentWidth-20))}}/>}{agent}</>}
   </div>
   <footer className="statusbar"><span className="green">●</span> {dictionary("prototype")} <span>Windows 11 x64 / {ipcStatus}</span><span className="right">{dictionary("offline")} · {prefs.locale}</span></footer>
  </div>
  {palette&&<div className="overlay" onMouseDown={e=>{if(e.target===e.currentTarget)closePalette()}}>
   <div className="palette" role="dialog" aria-modal="true" aria-label={dictionary("search")}>
   <input id="palette-search" autoFocus placeholder={dictionary("command")} value={query} onChange={e=>setQuery(e.target.value)} onKeyDown={e=>{if(e.key==="Enter"&&menu.length)navigate(menu[0].id);if(e.key==="Tab"){e.preventDefault();e.currentTarget.focus();}}}/>
   <div id="palette-options">{menu.map(n=><button key={n.id} data-page={n.id} onClick={()=>{navigate(n.id);requestAnimationFrame(()=>lastFocus.current?.focus?.())}}>{n.icon} &nbsp; {dictionary(n.id)}</button>)}</div>
   <small>{dictionary("kbdHelp")}</small></div></div>}
  {notice&&<div className="toast" role="status">{notice}<button onClick={()=>setNotice("")} aria-label={dictionary("close")}>×</button></div>}
 </div>;
}
createRoot(document.getElementById("app")).render(<App/>);
