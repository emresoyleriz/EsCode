import {test} from 'node:test';
import {strict as assert} from 'node:assert';
import {prepareInlineEdit,applyInlineEdit} from '../src/inline-edit.js';
test('exact replacement changes only the selected range',()=>{
 const p=prepareInlineEdit('A.cs','hello world',6,11);
 assert.equal(p.original,'world');
 assert.equal(applyInlineEdit(p,'hello world','EsCode'),'hello EsCode');
 assert.equal(p.base,'hello world');
});
test('insertion and deletion are both explicit in-memory edits',()=>{
 const base='a + b';
 assert.equal(applyInlineEdit(prepareInlineEdit('A.cs',base,1,1),base,'123'),'a123 + b');
 assert.equal(applyInlineEdit(prepareInlineEdit('A.cs',base,2,5),base,''),'a ');
});
test('external or concurrent modification rejects a stale proposal',()=>{
 const p=prepareInlineEdit('A.cs','int count=1;',4,9);
 assert.throws(()=>applyInlineEdit(p,'int count=2;','newCount'),/Stale buffer/);
 assert.throws(()=>applyInlineEdit(p,'int Count=1;','newCount'),/Stale buffer/);
});
test('malformed offsets and unsafe replacement are denied',()=>{
 for(const [start,end] of [[-1,1],[0,100],[2,1],[0,NaN]])
  assert.throws(()=>prepareInlineEdit('A.cs','abc',start,end),RangeError);
 const p=prepareInlineEdit('A.cs','abc',0,1);
 assert.throws(()=>applyInlineEdit(p,'abc','x'.repeat(16_385)),RangeError);
 assert.throws(()=>applyInlineEdit(p,'abc','\0'),RangeError);
});
