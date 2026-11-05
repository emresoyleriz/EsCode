import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {languageForFile} from '../src/language-for-file.js';
test('Monaco uses actual extension-aware source languages',()=>{
 for(const [name,expected] of [
  ['Api.cs','csharp'],['Program.csx','csharp'],['src/component.tsx','typescript'],
  ['app/index.jsx','javascript'],['src/appsettings.JSON','json'],['main.py','python'],
  ['data/report.sql','sql'],['README.md','markdown'],['styles.scss','scss'],
  ['src/Dockerfile','dockerfile'],['src/example.yml','yaml']])
  assert.equal(languageForFile(name),expected,name);
});
test('unknown and untrusted file types cannot launch language servers',()=>{
 assert.equal(languageForFile('exe.exe'),'plaintext');
 assert.equal(languageForFile(''),'plaintext');
 assert.equal(languageForFile(null),'plaintext');
 assert.equal(languageForFile('x'.repeat(1025)),'plaintext');
 assert.equal(languageForFile('config.env'),'plaintext');
 assert.equal(languageForFile('folder/README'),'plaintext');
});
