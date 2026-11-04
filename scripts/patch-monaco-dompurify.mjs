// Monaco 0.55.1 embeds a vulnerable DOMPurify inside its ESM distribution.
// Fail closed if the expected layout changes; remove this only after a verified
// upstream version bundles a patched sanitizer. https://github.com/microsoft/monaco-editor/issues/5517
import {readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
const pkg=JSON.parse(await readFile('node_modules/dompurify/package.json','utf8'));
if(pkg.version!=='3.4.17')throw new Error('Expected patched DOMPurify 3.4.17');
const bundled=resolve('node_modules/monaco-editor/esm/vs/base/browser/dompurify/dompurify.js');
const current=await readFile(bundled,'utf8');
if(!current.includes('DOMPurify')&&!current.includes('dompurify')&&!current.includes('EsCode patched sanitizer'))
  throw new Error('Unknown Monaco sanitizer layout; refusing to patch');
await writeFile(bundled,
  '// EsCode patched sanitizer: uses npm DOMPurify 3.4.17 instead of vulnerable embedded copy\n'+
  "export { default } from 'dompurify';\n",'utf8');
console.log('Monaco ESM sanitizer redirected to patched DOMPurify 3.4.17');
