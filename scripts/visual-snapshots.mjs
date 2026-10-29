import { chromium } from "@playwright/test";
import assert from "node:assert/strict";
import { mkdir } from "node:fs/promises";
import { NAV } from "../src/state.js";
import { t } from "../src/i18n.js";

const origin = process.env.ESCODE_PREVIEW_URL || "http://127.0.0.1:5173";
const output = "artifacts/visual";
await mkdir(output, {recursive:true});
const browser = await chromium.launch({headless:true});
try {
  const page = await browser.newPage({viewport:{width:1440,height:900},deviceScaleFactor:1,reducedMotion:"reduce"});
  await page.goto(origin,{waitUntil:"networkidle"});
  assert.equal(await page.locator(".sidebar [data-page]").count(),13);
  for(const theme of ["dark","light"]) {
    await page.locator('.sidebar [data-page="settings"]').click();
    await page.locator("#theme").selectOption(theme);
    assert.equal(await page.locator(".shell").getAttribute("class").then(x=>x.includes("theme-"+theme)),true);
    for(const target of NAV) {
      await page.locator('.sidebar [data-page="'+target.id+'"]').click();
      assert.equal((await page.locator("h1").innerText()).trim(),t("TR",target.id));
      await page.screenshot({path:output+"/"+theme+"-"+target.id+".png",animations:"disabled"});
    }
  }
  await page.keyboard.press("Control+k");
  assert.equal(await page.locator('.palette[role="dialog"]').count(),1);
  await page.locator("#palette-search").fill(t("TR","models"));
  await page.keyboard.press("Enter");
  assert.equal((await page.locator("h1").innerText()).trim(),t("TR","models"));
  console.log("VISUAL_SMOKE_PASS: "+(NAV.length*2)+" screenshots, 13 screens, 2 themes, command palette.");
  await page.close();
} finally {
  await browser.close();
}
