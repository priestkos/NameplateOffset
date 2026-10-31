/**
 * Deterministic mock-up walkthrough: frame screenshots → H.264 MP4 + GIF.
 * Requires: npm install (playwright), ffmpeg on PATH, npx playwright install chromium
 */
import { chromium } from "playwright";
import { fileURLToPath } from "url";
import path from "path";
import fs from "fs";
import { spawnSync } from "child_process";

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const outDir = path.join(__dirname, "..");
const htmlPath = path.join(__dirname, "walkthrough.html");
const framesDir = path.join(__dirname, ".frames");

const FPS = 30;

function run(cmd, args, opts = {}) {
  const r = spawnSync(cmd, args, { stdio: "inherit", ...opts });
  if (r.status !== 0) {
    throw new Error(`${cmd} exited with ${r.status}`);
  }
}

function formatBytes(n) {
  if (n < 1024) return `${n} B`;
  if (n < 1024 * 1024) return `${(n / 1024).toFixed(1)} KiB`;
  return `${(n / 1024 / 1024).toFixed(2)} MiB`;
}

if (fs.existsSync(framesDir)) {
  fs.rmSync(framesDir, { recursive: true });
}
fs.mkdirSync(framesDir, { recursive: true });

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  deviceScaleFactor: 1,
});
const page = await context.newPage();
await page.goto(`file://${htmlPath}`, { waitUntil: "load" });

const totalFrames = await page.evaluate(() => window.WALKTHROUGH_TOTAL_FRAMES);
console.log(`Rendering ${totalFrames} frames at ${FPS} fps (${(totalFrames / FPS).toFixed(1)} s)`);

for (let frame = 0; frame < totalFrames; frame++) {
  await page.evaluate((f) => window.renderWalkthroughFrame(f), frame);
  const name = `frame_${String(frame).padStart(5, "0")}.png`;
  await page.screenshot({
    path: path.join(framesDir, name),
    type: "png",
  });
  if (frame % 150 === 0) {
    console.log(`  frame ${frame}/${totalFrames}`);
  }
}

await browser.close();

const mp4Path = path.join(outDir, "mockup-walkthrough.mp4");
const gifPath = path.join(outDir, "mockup-walkthrough.gif");
const palettePath = path.join(framesDir, "palette.png");

run("ffmpeg", [
  "-y",
  "-framerate",
  String(FPS),
  "-i",
  path.join(framesDir, "frame_%05d.png"),
  "-c:v",
  "libx264",
  "-preset",
  "medium",
  "-pix_fmt",
  "yuv420p",
  "-movflags",
  "+faststart",
  "-crf",
  "28",
  mp4Path,
]);

const mp4Stat = fs.statSync(mp4Path);
console.log(`MP4: ${formatBytes(mp4Stat.size)} → ${mp4Path}`);

run("ffmpeg", [
  "-y",
  "-framerate",
  String(FPS),
  "-i",
  path.join(framesDir, "frame_%05d.png"),
  "-vf",
  "fps=5,scale=640:-1:flags=lanczos,palettegen=stats_mode=diff:max_colors=128",
  "-frames:v",
  "1",
  palettePath,
]);

run("ffmpeg", [
  "-y",
  "-framerate",
  String(FPS),
  "-i",
  path.join(framesDir, "frame_%05d.png"),
  "-i",
  palettePath,
  "-lavfi",
  "fps=5,scale=640:-1:flags=lanczos[x];[x][1:v]paletteuse=dither=bayer:bayer_scale=5",
  "-loop",
  "0",
  gifPath,
]);

const gifStat = fs.statSync(gifPath);
console.log(`GIF: ${formatBytes(gifStat.size)} → ${gifPath}`);

fs.rmSync(framesDir, { recursive: true });
console.log("Done.");
