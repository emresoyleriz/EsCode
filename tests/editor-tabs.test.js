import {test} from "node:test";
import {strict as assert} from "node:assert";
import {openEditorTab,closeEditorTab} from "../src/editor-tabs.js";
test("tabs open once and retain stable ordering",()=>{
 assert.deepEqual(openEditorTab(["A.cs"],"B.cs"),["A.cs","B.cs"]);
 assert.deepEqual(openEditorTab(["A.cs","B.cs"],"A.cs"),["A.cs","B.cs"]);
});
test("closing active tab selects adjacent without deleting buffer state",()=>{
 assert.deepEqual(closeEditorTab(["A.cs","B.cs","C.cs"],"B.cs","B.cs"),
  {tabs:["A.cs","C.cs"],selected:"C.cs"});
});
test("closing an inactive or final tab preserves selection",()=>{
 assert.deepEqual(closeEditorTab(["A.cs","B.cs"],"A.cs","B.cs"),
  {tabs:["A.cs"],selected:"A.cs"});
 assert.deepEqual(closeEditorTab(["A.cs"],"A.cs","A.cs"),{tabs:["A.cs"],selected:"A.cs"});
});
test("invalid tab names cannot be inserted",()=>assert.deepEqual(openEditorTab(["A.cs"],""),["A.cs"]));
