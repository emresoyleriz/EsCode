import {test} from "node:test";
import {strict as assert} from "node:assert";
import {readFileSync} from "node:fs";
test("Electron renderer is sandboxed and context isolated",()=>{
 const code=readFileSync("desktop/main.cjs","utf8");
 for(const rule of ["sandbox:true","contextIsolation:true","nodeIntegration:false","webSecurity:true","webviewTag:false"])assert.ok(code.includes(rule),rule);
 assert.ok(code.includes("setWindowOpenHandler"));
 assert.ok(code.includes("setPermissionRequestHandler"));
 assert.ok(code.includes('allowedSender(event)'));
});
test("Preload exposes no generic IPC or OS capability",()=>{
 const code=readFileSync("desktop/preload.cjs","utf8");
 assert.ok(code.includes('contextBridge.exposeInMainWorld("escode"'));
 assert.ok(!code.includes("openExternal("));
 assert.ok(!code.includes("exec("));
 assert.ok(!code.includes("send:"));
});
test("Monaco is initialized locally without CDN",()=>{
 const code=readFileSync("src/monaco.js","utf8");
 assert.ok(code.includes("loader.config({monaco})"));
 assert.ok(code.includes("MonacoEnvironment"));
 assert.ok(!code.includes("https://"));
});
