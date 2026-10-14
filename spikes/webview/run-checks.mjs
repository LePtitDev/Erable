// Throwaway spike runner (M0-08), Windows only: launches a spike host, drives WebView2 over CDP and prints a
// JSON report. Usage: node run-checks.mjs <host exe> [--real-keys]
//   --real-keys  also sends OS-level keystrokes (SendKeys) to the spike window, after checking it is focused.
import { execFileSync, spawn } from "node:child_process";
import { readFileSync } from "node:fs";
import { join, resolve } from "node:path";

const [hostExe, ...flags] = process.argv.slice(2);
const realKeys = flags.includes("--real-keys");
const here = import.meta.dirname;
const dist = resolve(here, "web", "dist");
const dropFile = resolve(here, "web", "public", "drop.png");
const port = 9444;
const sleep = (ms) => new Promise((r) => setTimeout(r, ms));
const report = { host: hostExe, checks: {} };

const t0 = Date.now();
const child = spawn(hostExe, [dist], {
  env: { ...process.env, WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${port}` },
  stdio: ["ignore", "pipe", "inherit"],
});
child.stdout.on("data", (d) => (report.hostOutput = String(d).trim()));

// ---- CDP plumbing
async function connect() {
  for (;;) {
    try {
      const targets = await (await fetch(`http://127.0.0.1:${port}/json`)).json();
      const page = targets.find((t) => t.type === "page" && t.url.startsWith("http://127.0.0.1"));
      if (page) return page;
    } catch {}
    if (Date.now() - t0 > 30000) throw new Error("CDP not reachable");
    await sleep(100);
  }
}
const page = await connect();
const ws = new WebSocket(page.webSocketDebuggerUrl);
await new Promise((r) => ws.addEventListener("open", r));
let nextId = 0;
const pending = new Map();
const events = [];
ws.addEventListener("message", (e) => {
  const m = JSON.parse(e.data);
  if (m.id && pending.has(m.id)) {
    pending.get(m.id)(m);
    pending.delete(m.id);
  } else if (m.method) events.push(m);
});
const send = (method, params = {}) =>
  new Promise((resolve, reject) => {
    const id = ++nextId;
    pending.set(id, (m) => (m.error ? reject(new Error(`${method}: ${m.error.message}`)) : resolve(m.result)));
    ws.send(JSON.stringify({ id, method, params }));
  });
const evaluate = async (expression) => {
  const r = await send("Runtime.evaluate", { expression, awaitPromise: true, returnByValue: true });
  if (r.exceptionDetails) throw new Error(r.exceptionDetails.exception?.description ?? r.exceptionDetails.text);
  return r.result.value;
};
async function key(keyName, { code, vk, modifiers = 0, text, commands } = {}) {
  const base = { key: keyName, code, windowsVirtualKeyCode: vk, modifiers };
  await send("Input.dispatchKeyEvent", { type: text ? "keyDown" : "rawKeyDown", ...base, text, commands });
  await send("Input.dispatchKeyEvent", { type: "keyUp", ...base });
}
async function check(name, fn) {
  try {
    report.checks[name] = await fn();
  } catch (error) {
    report.checks[name] = { ok: false, error: String(error) };
  }
}

await send("Runtime.enable");
await send("Network.enable");
while (!(await evaluate("window.spike?.ready === true").catch(() => false))) await sleep(50);
report.startupToReadyMs = Date.now() - t0;
report.userAgent = await evaluate("navigator.userAgent");

// ---- 1 · CodeMirror atomic pill, deletion, undo, typing, IME
await check("1-codemirror", async () => {
  const doc = () => evaluate("spike.editor.state.doc.toString()");
  const head = () => evaluate("spike.editor.state.selection.main.head");
  const result = {};
  result.pillRendered = await evaluate("document.querySelector('.pill')?.textContent");
  await evaluate("spike.editor.focus(); spike.editor.dispatch({ selection: { anchor: 9 } })");
  await key("ArrowRight", { code: "ArrowRight", vk: 39 });
  result.cursorJumpsOverPill = (await head()) === 29;
  await key("ArrowLeft", { code: "ArrowLeft", vk: 37 });
  result.cursorJumpsBack = (await head()) === 9;
  await evaluate("spike.editor.dispatch({ selection: { anchor: 29 } })");
  await key("Backspace", { code: "Backspace", vk: 8 });
  result.backspaceDeletesWholePill = !(await doc()).includes("[[") && (await head()) === 9;
  await key("z", { code: "KeyZ", vk: 90, modifiers: 2 });
  result.undoRestoresPill = (await doc()).includes("[[3F2A9C14E07B5D21]]");
  await evaluate("spike.editor.dispatch({ selection: { anchor: spike.editor.state.doc.length } })");
  await send("Input.insertText", { text: " Fin." });
  result.typing = (await doc()).endsWith(" Fin.");
  // IME: dead key (^ + e) then a CJK composition.
  await send("Input.imeSetComposition", { text: "^", selectionStart: 1, selectionEnd: 1 });
  await send("Input.insertText", { text: "ê" });
  await send("Input.imeSetComposition", { text: "に", selectionStart: 1, selectionEnd: 1 });
  await send("Input.imeSetComposition", { text: "日本", selectionStart: 2, selectionEnd: 2 });
  await send("Input.insertText", { text: "日本" });
  const final = await doc();
  result.imeComposition = final.endsWith(" Fin.ê日本") && !final.includes("^") && !final.includes("に");
  result.finalDoc = final;
  result.ok = Object.entries(result).every(([k, v]) => k === "finalDoc" || k === "pillRendered" || v === true);
  return result;
});

// ---- 2 · HTML5 drag and drop (drag intercepted by CDP, then replayed onto the targets)
await check("2-dnd", async () => {
  const center = (id) =>
    evaluate(`(() => { const r = document.querySelector('[data-id="${id}"]').getBoundingClientRect();
      return { x: r.x + r.width / 2, y: r.y + r.height / 2 }; })()`);
  const order = () => evaluate("[...document.querySelectorAll('#tree li')].map((li) => li.dataset.id).join(',')");
  await send("Input.setInterceptDrags", { enabled: true });
  async function drag(from, to) {
    const a = await center(from);
    const b = await center(to);
    const intercepted = new Promise((resolve) => {
      const start = events.length;
      const poll = setInterval(() => {
        const ev = events.slice(start).find((e) => e.method === "Input.dragIntercepted");
        if (ev) {
          clearInterval(poll);
          resolve(ev.params.data);
        }
      }, 10);
    });
    await send("Input.dispatchMouseEvent", { type: "mousePressed", x: a.x, y: a.y, button: "left", clickCount: 1 });
    for (let i = 1; i <= 5; i++)
      await send("Input.dispatchMouseEvent", { type: "mouseMoved", x: a.x, y: a.y + i * 4, button: "left", buttons: 1 });
    const data = await Promise.race([intercepted, sleep(2000).then(() => null)]);
    if (!data) throw new Error("drag not intercepted");
    await send("Input.dispatchDragEvent", { type: "dragEnter", x: b.x, y: b.y, data });
    await send("Input.dispatchDragEvent", { type: "dragOver", x: b.x, y: b.y, data });
    await send("Input.dispatchDragEvent", { type: "drop", x: b.x, y: b.y, data });
    await send("Input.dispatchMouseEvent", { type: "mouseReleased", x: b.x, y: b.y, button: "left", clickCount: 1 });
  }
  const before = await order();
  await drag("a", "c");
  const afterAllowed = await order();
  await drag("b", "root-cat");
  const afterForbidden = await order();
  await send("Input.setInterceptDrags", { enabled: false });
  return {
    ok: afterAllowed === "b,c,a,root-cat" && afterForbidden === afterAllowed,
    before,
    afterAllowed,
    afterForbidden,
    log: await evaluate("spike.dnd"),
    note: "Forbidden-cursor rendering is visual: confirm by hand.",
  };
});

// ---- 3 · Video: seek → Range request answered 206, playback advances
await check("3-video", async () => {
  const start = events.length;
  const r = await evaluate(`(async () => {
    const v = document.getElementById('video');
    if (v.readyState < 1) await new Promise((r) => v.addEventListener('loadedmetadata', r, { once: true }));
    v.currentTime = 25;
    await new Promise((r) => v.addEventListener('seeked', r, { once: true }));
    const seekedAt = v.currentTime;
    v.muted = true;
    await v.play();
    await new Promise((r) => setTimeout(r, 700));
    v.pause();
    return { duration: v.duration, seekedAt, playedTo: v.currentTime, error: v.error?.code ?? null };
  })()`);
  const mediaRequests = events
    .slice(0)
    .filter((e) => e.method === "Network.requestWillBeSent" && e.params.request.url.endsWith("/sample.mp4"))
    .map((e) => ({ id: e.params.requestId, range: e.params.request.headers.Range ?? e.params.request.headers.range }));
  const statuses = events
    .filter((e) => e.method === "Network.responseReceived" && e.params.response.url.endsWith("/sample.mp4"))
    .map((e) => e.params.response.status);
  return {
    ok: r.error === null && Math.abs(r.seekedAt - 25) < 0.5 && r.playedTo > r.seekedAt && statuses.includes(206),
    ...r,
    rangeRequests: mediaRequests.filter((m) => m.range).length,
    statuses: [...new Set(statuses)],
    newEventsSinceCheck: events.length - start,
  };
});

// ---- 4 · Paste an image from the OS clipboard, drop a file from the OS
await check("4-paste-drop", async () => {
  const ps = (script) =>
    execFileSync("powershell.exe", ["-NoProfile", "-STA", "-Command", script], { encoding: "utf8" }).trim();
  const previousText = ps("Add-Type -AssemblyName System.Windows.Forms; [Windows.Forms.Clipboard]::GetText()");
  ps(`Add-Type -AssemblyName System.Windows.Forms, System.Drawing;
      [Windows.Forms.Clipboard]::SetImage([Drawing.Image]::FromFile('${dropFile}'))`);
  try {
    await evaluate("document.getElementById('paste').focus()");
    await key("v", { code: "KeyV", vk: 86, modifiers: 2, commands: ["paste"] });
    await sleep(300);
  } finally {
    ps(`Add-Type -AssemblyName System.Windows.Forms;
        ${previousText ? `[Windows.Forms.Clipboard]::SetText(@'\n${previousText}\n'@.Trim())` : "[Windows.Forms.Clipboard]::Clear()"}`);
  }
  const target = await evaluate(
    "(() => { const r = document.getElementById('paste').getBoundingClientRect(); return { x: r.x + 20, y: r.y + 20 }; })()",
  );
  const data = { items: [], files: [dropFile], dragOperationsMask: 1 };
  await send("Input.dispatchDragEvent", { type: "dragEnter", ...target, data });
  await send("Input.dispatchDragEvent", { type: "dragOver", ...target, data });
  await send("Input.dispatchDragEvent", { type: "drop", ...target, data });
  await sleep(200);
  const log = await evaluate("spike.files");
  const size = readFileSync(dropFile).length;
  return {
    ok: log.some((l) => l.startsWith("paste ") && l.includes("image/png")) && log.includes(`drop drop.png image/png ${size}`),
    log,
    note: "Clipboard image set with System.Windows.Forms; previous clipboard text restored.",
  };
});

// ---- 5 · Shortcuts reach the page
await check("5-shortcuts", async () => {
  await evaluate("document.body.focus()");
  await key("s", { code: "KeyS", vk: 83, modifiers: 2, text: "s" });
  await key("k", { code: "KeyK", vk: 75, modifiers: 2, text: "k" });
  await key(" ", { code: "Space", vk: 32, modifiers: 2, text: " " });
  await key("K", { code: "KeyK", vk: 75, modifiers: 4 | 8, text: "K" });
  let realKeyLog;
  if (realKeys) {
    const out = execFileSync(
      "powershell.exe",
      [
        "-NoProfile",
        "-STA",
        "-Command",
        `Add-Type -AssemblyName System.Windows.Forms, Microsoft.VisualBasic;
         $ok = [Microsoft.VisualBasic.Interaction]::AppActivate(${child.pid}); Start-Sleep -Milliseconds 400;
         Add-Type @'
using System; using System.Runtime.InteropServices; using System.Text;
public static class Fg { [DllImport("user32.dll")] public static extern IntPtr GetForegroundWindow();
  [DllImport("user32.dll")] public static extern int GetWindowText(IntPtr h, StringBuilder s, int n); }
'@;
         $sb = New-Object System.Text.StringBuilder 256; [void][Fg]::GetWindowText([Fg]::GetForegroundWindow(), $sb, 256);
         if ($sb.ToString() -like 'Erable spike*') { [Windows.Forms.SendKeys]::SendWait('^s'); [Windows.Forms.SendKeys]::SendWait('^k'); [Windows.Forms.SendKeys]::SendWait('^ '); 'sent' } else { 'skipped: foreground=' + $sb.ToString() }`,
      ],
      { encoding: "utf8" },
    ).trim();
    await sleep(300);
    realKeyLog = out;
  }
  const log = await evaluate("spike.keys");
  return {
    ok: ["handled Ctrl+s", "handled Ctrl+k", "handled Ctrl+ ", "handled Meta+Shift+K"].every((k) => log.includes(k)),
    log,
    realKeys: realKeyLog ?? "not run (pass --real-keys)",
  };
});

// ---- 6 · Metrics: memory of the host and its WebView2 processes
await check("6-metrics", async () => {
  const json = execFileSync(
    "powershell.exe",
    [
      "-NoProfile",
      "-Command",
      `$all = Get-CimInstance Win32_Process | Select-Object ProcessId, ParentProcessId, Name, WorkingSetSize;
       $ids = @(${child.pid}); do { $n = $ids.Count; $ids = @($ids + ($all | Where-Object { $ids -contains $_.ParentProcessId } | ForEach-Object ProcessId)) | Sort-Object -Unique } while ($ids.Count -ne $n);
       $procs = $all | Where-Object { $ids -contains $_.ProcessId };
       @{ processes = $procs.Count; host = [int](($procs | Where-Object ProcessId -eq ${child.pid}).WorkingSetSize / 1MB);
          totalWorkingSetMB = [int](($procs | Measure-Object WorkingSetSize -Sum).Sum / 1MB) } | ConvertTo-Json -Compress`,
    ],
    { encoding: "utf8" },
  );
  return { ok: true, startupToReadyMs: report.startupToReadyMs, page: await evaluate("spike.metrics"), ...JSON.parse(json) };
});

// ---- Screenshot for the report (optional: ERABLE_SPIKE_SCREENSHOT=<png path>)
if (process.env.ERABLE_SPIKE_SCREENSHOT) {
  const { writeFileSync } = await import("node:fs");
  const shot = await send("Page.captureScreenshot", { format: "png" });
  writeFileSync(process.env.ERABLE_SPIKE_SCREENSHOT, Buffer.from(shot.data, "base64"));
}

// ---- Close the window and check the process exits
ws.close();
execFileSync("powershell.exe", ["-NoProfile", "-Command", `(Get-Process -Id ${child.pid}).CloseMainWindow() | Out-Null`]);
const exited = await Promise.race([new Promise((r) => child.on("exit", () => r(true))), sleep(10000).then(() => false)]);
report.cleanExit = exited;
if (!exited) child.kill();
console.log(JSON.stringify(report, null, 2));
