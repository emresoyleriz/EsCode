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
 await page.locator('.sidebar [data-page="settings"]').click();
 await page.locator("#agentMode").selectOption("floating");
 assert.equal(await page.locator(".agent-panel.agent-floating").count(),1);
 await page.reload({waitUntil:"networkidle"});
 await page.locator('.sidebar [data-page="settings"]').click();
 assert.equal(await page.locator("#locale").inputValue(),"EN");
 assert.equal(await page.locator("#theme").inputValue(),"light");
 assert.equal(await page.locator("#agentMode").inputValue(),"floating");
 console.log("VISUAL_SMOKE_PASS: "+(NAV.length*4)+" screenshots; 13 screens, 2 themes, 2 languages, command palette, persisted prefs and floating panel.");
 await page.close();
} finally {await browser.close();}
