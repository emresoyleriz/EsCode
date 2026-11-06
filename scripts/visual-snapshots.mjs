import {chromium} from "@playwright/test";
import assert from "node:assert/strict";
import {mkdir} from "node:fs/promises";
import {NAV} from "../src/state.js";
import {t} from "../src/i18n.js";

const origin=process.env.ESCODE_PREVIEW_URL||"http://127.0.0.1:5173";
const output="artifacts/visual";
await mkdir(output,{recursive:true});
const browser=await chromium.launch({headless:true});
try{
 const page=await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1,reducedMotion:"reduce"});
 await page.goto(origin,{waitUntil:"networkidle"});
 assert.equal(await page.locator(".sidebar [data-page]").count(),13);
 for(const locale of ["TR","EN"]){
  await page.locator('.sidebar [data-page="settings"]').click();
  await page.locator("#locale").selectOption(locale);
  assert.equal((await page.locator("h1").innerText()).trim(),t(locale,"settings"));
  for(const theme of ["dark","light"]){
   await page.locator('.sidebar [data-page="settings"]').click();
   await page.locator("#theme").selectOption(theme);
   assert.ok((await page.locator(".shell").getAttribute("class")).includes("theme-"+theme));
   for(const screen of NAV){
    await page.locator('.sidebar [data-page="'+screen.id+'"]').click();
    assert.equal((await page.locator("h1").innerText()).trim(),t(locale,screen.id));
    await page.screenshot({path:output+"/"+locale.toLowerCase()+"-"+theme+"-"+screen.id+".png",animations:"disabled"});
   }
  }
 }
 await page.keyboard.press("Control+k");
 assert.equal(await page.locator('.palette[role="dialog"]').count(),1);
 await page.locator("#palette-search").fill(t("EN","models"));
 await page.keyboard.press("Enter");
 assert.equal((await page.locator("h1").innerText()).trim(),t("EN","models"));
 // Keyboard smoke: palette results must be reachable via arrows, Tab and Escape.
 await page.keyboard.press("Control+k");
 const paletteSearch=page.locator("#palette-search");
 await paletteSearch.fill(t("EN","terminal"));
 await paletteSearch.press("ArrowDown");
 assert.equal(await page.locator(".palette button:focus").count(),1);
 await page.keyboard.press("Shift+Tab");
 assert.equal(await paletteSearch.evaluate(el=>document.activeElement===el),true);
 await paletteSearch.press("Escape");
 assert.equal(await page.locator(".palette").count(),0);
 await page.locator('.sidebar [data-page="review"]').focus();
 await page.keyboard.press("Enter");
 assert.equal((await page.locator("h1").innerText()).trim(),t("EN","review"));
 await page.locator('.sidebar [data-page="settings"]').click();
 await page.locator("#agentMode").selectOption("floating");
 assert.equal(await page.locator(".agent-panel.agent-floating").count(),1);
 await page.reload({waitUntil:"networkidle"});
 await page.locator('.sidebar [data-page="settings"]').click();
 assert.equal(await page.locator("#locale").inputValue(),"EN");
 assert.equal(await page.locator("#theme").inputValue(),"light");
 assert.equal(await page.locator("#agentMode").inputValue(),"floating");
 // Four desktop viewport classes. This checks navigation and viewport overflow,
 // not mixed-DPI screen readers or a signed Windows desktop build.
 for(const size of [{width:1366,height:768},{width:1920,height:1080},
                    {width:2560,height:1440},{width:3440,height:1440}]){
  await page.setViewportSize(size);
  for(const screen of NAV){
    await page.locator('.sidebar [data-page="'+screen.id+'"]').click();
    assert.equal((await page.locator("h1").innerText()).trim(),t("EN",screen.id));
    const metrics=await page.evaluate(()=>({
      viewport:document.documentElement.clientWidth,
      document:document.documentElement.scrollWidth
    }));
    assert.ok(metrics.document<=metrics.viewport+1,
      "Unexpected horizontal overflow on "+screen.id+" at "+size.width);
  }
 }
 // Explicit recovery: no background code storage, closed dirty buffers survive.
 await page.getByRole("button",{name:t("EN","toggleAgent"),exact:true}).click();
 await page.locator('.sidebar [data-page="ide"]').click();
 await page.locator(".monaco-editor .view-lines").click();
 await page.keyboard.press("Control+End");
 await page.keyboard.press("Enter");
 await page.keyboard.type("// recovery-fixture");
 await page.getByText(t("EN","dirty"),{exact:true}).waitFor();
 await page.locator('.filetree button').filter({hasText:"AgentOrchestrator.cs"}).click();
 await page.getByRole("button",{name:"Close WorkspaceService.cs",exact:true}).click();
 // Same-file selection and closing an inactive tab must retain the editor handle.
 await page.locator('.filetree button').filter({hasText:"AgentOrchestrator.cs"}).click();
 await page.locator(".monaco-editor .view-lines").click();
 await page.keyboard.press("Control+Home");
 await page.getByRole("button",{name:t("EN","inlineEdit"),exact:true}).click();
 await page.locator("#inline-replacement").fill("// inline-ref-check\n");
 await page.getByRole("button",{name:t("EN","inlineAccept"),exact:true}).click();
 await page.waitForFunction(()=>document.querySelector(".view-lines")?.textContent.includes("inline-ref-check"));
 await page.locator(".monaco-editor .view-lines").click();
 await page.keyboard.press("Control+z");
 await page.waitForFunction(()=>!document.querySelector(".view-lines")?.textContent.includes("inline-ref-check"));

 await page.locator('.sidebar [data-page="recovery"]').click();
 assert.equal(await page.evaluate(()=>localStorage.getItem("escode.preview.buffers.v1")),null);
 await page.getByRole("button",{name:t("EN","checkpointSave"),exact:true}).click();
 const copy=await page.evaluate(()=>JSON.parse(localStorage.getItem("escode.preview.buffers.v1")));
 assert.ok(copy.buffers["WorkspaceService.cs"].includes("recovery-fixture"),JSON.stringify(copy.buffers["WorkspaceService.cs"]));
 assert.ok(!copy.tabs.includes("WorkspaceService.cs"));
 page.on("dialog",dialog=>dialog.accept());
 await page.reload({waitUntil:"networkidle"});
 await page.locator('.sidebar [data-page="recovery"]').click();
 await page.getByRole("button",{name:t("EN","checkpointRestore"),exact:true}).click();
 await page.locator('.filetree button').filter({hasText:"WorkspaceService.cs"}).click();
 await page.waitForFunction(()=>document.querySelector(".view-lines")?.textContent.includes("recovery-fixture"));
 await page.locator('.sidebar [data-page="recovery"]').click();
 await page.getByRole("button",{name:t("EN","checkpointRemove"),exact:true}).click();
 assert.equal(await page.evaluate(()=>localStorage.getItem("escode.preview.buffers.v1")),null);
 console.log("VISUAL_SMOKE_PASS: "+(NAV.length*4)+" screenshots; 13 screens, 2 themes, 2 languages, command palette, persisted prefs and floating panel.");
 await page.close();
} finally {await browser.close();}
