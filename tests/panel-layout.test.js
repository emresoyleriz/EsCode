import {test} from 'node:test';
import assert from 'node:assert/strict';
import {fitFloatingPanel} from '../src/panel-layout.js';
test('restore fits full panel width after monitor removal or resize',()=>{
 for(const width of [320,640])for(const vw of [1,220,980,1366]){
  const p=fitFloatingPanel({x:9000,y:9000,width},{width:vw,height:768});
  assert.ok(p.x>=0&&p.x+p.width<=vw);assert.ok(p.y<=588);
 }
});
test('negative coordinates and invalid geometry recover to visible bounds',()=>{
 for(const value of [-2000,NaN,Infinity,undefined]){
  const p=fitFloatingPanel({x:value,y:value,width:value},{width:980,height:620});
  assert.ok(Number.isFinite(p.x)&&p.x>=0);assert.ok(p.y>=60);assert.ok(p.width<=980);
 }
});
test('normal geometry is stable and tiny viewports retain a reachable header',()=>{
 assert.deepEqual(fitFloatingPanel({x:280,y:96,width:320},{width:1440,height:900}),{x:280,y:96,width:320,maxHeight:804});
 const p=fitFloatingPanel({x:280,y:96,width:320},{width:100,height:80});
 assert.equal(p.x,0);assert.equal(p.y,0);assert.equal(p.width,100);assert.equal(p.maxHeight,80);
});
