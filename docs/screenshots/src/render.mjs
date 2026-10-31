import { chromium } from "playwright";
import { fileURLToPath } from "url";
import path from "path";
import fs from "fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(__dirname, "..");

const pages = [
  { html: "01-main-window-default.html", png: "01-main-window-default.png", fullPage: true },
  { html: "02-height-and-scopes.html", png: "02-height-and-scopes.png", fullPage: true },
  { html: "03-reset-offset-zero.html", png: "03-reset-offset-zero.png", fullPage: true },
  { html: "04-nameplate-visual-effect.html", png: "04-nameplate-visual-effect.png", selector: "#capture" },
  { html: "05-slash-commands.html", png: "05-slash-commands.png", selector: "#capture" },
];

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  deviceScaleFactor: 1,
});

for (const page of pages) {
  const filePath = path.join(__dirname, page.html);
  const p = await context.newPage();
  await p.goto(`file://${filePath}`, { waitUntil: "load" });
  await p.waitForTimeout(100);
  const outPath = path.join(outDir, page.png);
  if (page.selector) {
    const el = p.locator(page.selector);
    await el.screenshot({ path: outPath, type: "png" });
  } else {
    const el = p.locator("#capture");
    await el.screenshot({ path: outPath, type: "png" });
  }
  await p.close();
  const stat = fs.statSync(outPath);
  console.log(`${page.png}: ${(stat.size / 1024).toFixed(1)} KiB`);
}

await browser.close();

let total = 0;
for (const page of pages) {
  total += fs.statSync(path.join(outDir, page.png)).size;
}
console.log(`Total PNG size: ${(total / 1024 / 1024).toFixed(2)} MiB`);
