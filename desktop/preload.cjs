const {contextBridge,ipcRenderer}=require("electron");
// Deliberately exposes only a fixed read-only capability query.
// Do NOT add generic invoke, shell or file APIs without a security ADR.
contextBridge.exposeInMainWorld("escode",Object.freeze({
  version:1,
  capabilities:()=>ipcRenderer.invoke("escode:capabilities")
}));