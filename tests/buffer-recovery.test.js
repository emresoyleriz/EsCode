import {test} from 'node:test';
import assert from 'node:assert/strict';
import {BUFFER_KEY, readCheckpoint, saveCheckpoint, removeCheckpoint, restoreCheckpoint} from '../src/buffer-recovery.js';
const baseline = {'a.cs':'original', 'b.js':'second'};
const session = {buffers:{'a.cs':'edited', 'b.js':''}, tabs:['a.cs'], selected:'a.cs'};
function store() { const map = new Map(); return {getItem:k=>map.get(k),setItem:(k,v)=>map.set(k,v),removeItem:k=>map.delete(k)}; }
test('explicit checkpoint roundtrip preserves closed dirty buffers and empty content', () => {
  const s = store(); assert.equal(readCheckpoint(baseline,s).status,'empty');
  assert.equal(saveCheckpoint(session,baseline,s),true);
  const {checkpoint} = readCheckpoint(baseline,s);
  assert.deepEqual(restoreCheckpoint(checkpoint,baseline,baseline),session);
  assert.equal(removeCheckpoint(s),true); assert.equal(readCheckpoint(baseline,s).status,'empty');
});
test('restore is atomic and refuses conflicting live edits', () => {
  const s=store(); saveCheckpoint(session,baseline,s);
  const {checkpoint}=readCheckpoint(baseline,s);
  const live={...baseline,'b.js':'new unsaved work'};
  assert.throws(()=>restoreCheckpoint(checkpoint,live,baseline),/Conflicting/);
  assert.equal(live['b.js'],'new unsaved work');
  assert.deepEqual(restoreCheckpoint(checkpoint,session.buffers,baseline),session);
});
test('checkpoint from another baseline, workspace or unknown file is rejected', () => {
  const s=store(); saveCheckpoint(session,baseline,s);
  const original=JSON.parse(s.getItem(BUFFER_KEY));
  for (const change of [{version:2},{workspace:'other'},{tabs:['unknown']},{tabs:['a.cs','a.cs']},
    {selected:'b.js'},{buffers:{'a.cs':'edited'}},{baseline:{...baseline,'b.js':'changed'}}]) {
    s.setItem(BUFFER_KEY,JSON.stringify({...original,...change}));
    assert.equal(readCheckpoint(baseline,s).status,'invalid');
  }
});
test('malformed records and storage failure cannot lose the prior checkpoint', () => {
  const s=store(); saveCheckpoint(session,baseline,s); const previous=s.getItem(BUFFER_KEY);
  assert.equal(saveCheckpoint({...session,buffers:{...session.buffers,'a.cs':'x'.repeat(1_000_001)}},baseline,s),false);
  assert.equal(s.getItem(BUFFER_KEY),previous);
  const denied={getItem(){throw Error();},setItem(){throw Error();},removeItem(){throw Error();}};
  assert.equal(readCheckpoint(baseline,denied).status,'unavailable');
  assert.equal(saveCheckpoint(session,baseline,denied),false); assert.equal(removeCheckpoint(denied),false);
  s.setItem(BUFFER_KEY,'{'); assert.equal(readCheckpoint(baseline,s).status,'invalid');
});
