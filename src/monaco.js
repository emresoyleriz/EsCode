import * as monaco from "monaco-editor";
import loader from "@monaco-editor/loader";
import EditorWorker from "monaco-editor/esm/vs/editor/editor.worker.js?worker";
import JsonWorker from "monaco-editor/esm/vs/language/json/json.worker.js?worker";
// Use bundled workers and ESM Monaco. Never fetch Monaco source from a CDN.
globalThis.MonacoEnvironment={getWorker(_moduleId,label){return label==="json"?new JsonWorker():new EditorWorker();}};
loader.config({monaco});
