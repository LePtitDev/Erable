// Throwaway spike runner (M0-08): indicative run of the portable checks in Playwright's WebKit build.
// NOT WebKitGTK nor WKWebView: same engine family, different ports (media stack, clipboard, IME differ).
// Usage: node webkit-checks.mjs   (serves web/dist with `vite preview` on :4173)
import { spawn } from "node:child_process";
import { createRequire } from "node:module";
import { resolve } from "node:path";

const here = import.meta.dirname;
const require = createRequire(resolve(here, "web", "package.json"));
const { webkit } = require("playwright");
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));

const preview = spawn("corepack", ["pnpm", "exec", "vite", "preview", "--port", "4173", "--strictPort"], {
  cwd: resolve(here, "web"),
  shell: true,
  stdio: "ignore",
});
for (let i = 0; i < 100; i++) {
  if (await fetch("http://localhost:4173/").then((r) => r.ok).catch(() => false)) break;
  await sleep(100);
}

const browser = await webkit.launch();
const page = await browser.newPage();
await page.goto("http://localhost:4173/");
await page.waitForFunction(() => window.spike?.ready === true);
const report = { engine: `WebKit ${browser.version()} (Playwright)`, checks: {} };
const doc = () => page.evaluate(() => spike.editor.state.doc.toString());
const head = () => page.evaluate(() => spike.editor.state.selection.main.head);

// 1 · CodeMirror (IME cannot be driven in WebKit through Playwright)
await page.evaluate(() => { spike.editor.focus(); spike.editor.dispatch({ selection: { anchor: 9 } }); });
await page.keyboard.press("ArrowRight");
const jumps = (await head()) === 29;
await page.keyboard.press("Backspace");
const deletes = !(await doc()).includes("[[");
await page.keyboard.press("Control+z");
const undo = (await doc()).includes("[[3F2A9C14E07B5D21]]");
await page.evaluate(() => spike.editor.dispatch({ selection: { anchor: spike.editor.state.doc.length } }));
await page.keyboard.type(" Fin é");
report.checks["1-codemirror"] = { cursorJumpsOverPill: jumps, backspaceDeletesWholePill: deletes, undoRestoresPill: undo, typing: (await doc()).endsWith(" Fin é"), ime: "not verifiable here" };

// 2 · Drag and drop
await page.dragAndDrop('[data-id="a"]', '[data-id="c"]');
await page.dragAndDrop('[data-id="b"]', '[data-id="root-cat"]');
report.checks["2-dnd"] = {
  order: await page.evaluate(() => [...document.querySelectorAll("#tree li")].map((li) => li.dataset.id).join(",")),
  expected: "b,c,a,root-cat",
  log: await page.evaluate(() => spike.dnd),
};

// 3 · Video (codec support of this WebKit port is not representative of GStreamer/AVFoundation)
report.checks["3-video"] = await page.evaluate(async () => {
  const v = document.getElementById("video");
  const loaded = await Promise.race([
    new Promise((r) => (v.readyState >= 1 ? r(true) : v.addEventListener("loadedmetadata", () => r(true), { once: true }))),
    new Promise((r) => setTimeout(() => r(false), 5000)),
  ]);
  if (!loaded) return { loaded, error: v.error?.code ?? null, canPlayH264: v.canPlayType('video/mp4; codecs="avc1.42E01E"') };
  v.currentTime = 25;
  await new Promise((r) => v.addEventListener("seeked", r, { once: true }));
  return { loaded, seekedAt: v.currentTime, error: v.error?.code ?? null };
});

// 5 · Shortcuts
await page.locator("body").click({ position: { x: 5, y: 5 } });
for (const combo of ["Control+s", "Control+k", "Control+Space", "Meta+Shift+K"]) await page.keyboard.press(combo);
report.checks["5-shortcuts"] = { log: await page.evaluate(() => spike.keys) };

await browser.close();
preview.kill();
console.log(JSON.stringify(report, null, 2));
process.exit(0);
