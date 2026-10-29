const {app,BrowserWindow,ipcMain,net,protocol,session}=require("electron");
const path=require("node:path");
const fs=require("node:fs");
const {pathToFileURL}=require("node:url");

const ROOT=path.resolve(__dirname,"../dist");
protocol.registerSchemesAsPrivileged([{scheme:"escode",privileges:{standard:true,secure:true,supportFetchAPI:true,stream:true}}]);
app.enableSandbox();
let mainWindow;

function allowedSender(event){
 const url=event.senderFrame?.url??"";
 return event.sender===mainWindow?.webContents &&
    (url.startsWith("escode://app/") || (process.env.ESCODE_DEV_SERVER==="1"&&url.startsWith("http://127.0.0.1:5173/")));
}
function createWindow(){
 mainWindow=new BrowserWindow({
   width:1440,height:900,minWidth:980,minHeight:620,backgroundColor:"#0c1120",
   show:false,autoHideMenuBar:true,
   webPreferences:{sandbox:true,contextIsolation:true,nodeIntegration:false,webSecurity:true,
     webviewTag:false,preload:path.join(__dirname,"preload.cjs")}
 });
 mainWindow.webContents.setWindowOpenHandler(()=>({action:"deny"}));
 mainWindow.webContents.on("will-navigate",(event,url)=>{
   const intended=process.env.ESCODE_DEV_SERVER==="1"?"http://127.0.0.1:5173/":"escode://app/";
   if(!url.startsWith(intended))event.preventDefault();
 });
 mainWindow.once("ready-to-show",()=>mainWindow?.show());
 if(process.env.ESCODE_DEV_SERVER==="1")mainWindow.loadURL("http://127.0.0.1:5173/");
 else mainWindow.loadURL("escode://app/index.html");
}
app.whenReady().then(()=>{
 session.defaultSession.setPermissionRequestHandler((_webContents,_permission,callback)=>callback(false));
 session.defaultSession.setPermissionCheckHandler(()=>false);
 protocol.handle("escode",request=>{
   const url=new URL(request.url);
   if(url.host!=="app")return new Response("Forbidden",{status:403});
   let file;
   try{file=path.resolve(ROOT,"."+decodeURIComponent(url.pathname));}catch{return new Response("Invalid URL",{status:400});}
   if(!(file===ROOT||file.startsWith(ROOT+path.sep))||!fs.existsSync(file)||!fs.statSync(file).isFile())
      return new Response("Not found",{status:404});
   return net.fetch(pathToFileURL(file).toString());
 });
 ipcMain.handle("escode:capabilities",event=>{
   if(!allowedSender(event))throw new Error("Unauthorized sender");
   return Object.freeze({mode:"electron-preview",protocolVersion:1,safeRenderer:true,hostConnected:false});
 });
 createWindow();
 app.on("activate",()=>{if(BrowserWindow.getAllWindows().length===0)createWindow()});
}).catch(error=>{console.error("Desktop bootstrap failed:",error);app.quit()});
app.on("window-all-closed",()=>{if(process.platform!=="darwin")app.quit()});
