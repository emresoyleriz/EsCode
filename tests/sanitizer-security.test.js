import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {readFileSync} from 'node:fs';
test('DOMPurify is pinned to patched 3.4.17 in package and lock',()=>{
 const pkg=JSON.parse(readFileSync('package.json','utf8'));
 const lock=JSON.parse(readFileSync('package-lock.json','utf8'));
 assert.equal(pkg.overrides.dompurify,'3.4.17');
 const dep=lock.packages['node_modules/dompurify'];
 assert.equal(dep.version,'3.4.17');
 assert.match(dep.integrity,/^sha512-/);
 assert.equal(pkg.scripts.postinstall,'node scripts/patch-monaco-dompurify.mjs');
});
test('Monaco bundled sanitizer has an explicit fail-closed patch',()=>{
 const code=readFileSync('scripts/patch-monaco-dompurify.mjs','utf8');
 assert.match(code,/throw new Error/);
 assert.match(code,/export \{ default \} from 'dompurify'/);
});
