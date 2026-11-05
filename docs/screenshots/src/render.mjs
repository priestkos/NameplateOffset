import { chromium } from "playwright";
import { fileURLToPath } from "url";
import path from "path";
import fs from "fs";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(__dirname, "..");

const pages = [
  { html: "01-main-window-default.html", png: "01-main-window-default.png" },
  { html: "02-height-and-scopes.html", png: "02-height-and-scopes.png" },
  { html: "03-reset-offset-zero.html", png: "03-reset-offset-zero.png" },
  { html: "04-nameplate-visual-effect.html", png: "04-nameplate-visual-effect.png" },
  { html: "05-slash-commands.html", png: "05-slash-commands.png" },
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
  await p.screenshot({ path: outPath, type: "png" });
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
