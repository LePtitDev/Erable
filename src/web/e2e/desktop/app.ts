import { chromium, type Browser, type Page } from "@playwright/test";
import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { resolve } from "node:path";

export const desktopExe =
  process.env["ERABLE_DESKTOP_EXE"] ??
  resolve(
    import.meta.dirname,
    "..",
    "..",
    "..",
    "..",
    "artifacts",
    "desktop",
    "win-x64",
    "Erable",
    "Erable.Desktop.exe",
  );

export interface DesktopApp {
  readonly process: ChildProcess;
  readonly browser: Browser;
  readonly page: Page;
  readonly baseUrl: URL;
  readonly launchedAt: number;
  close(): Promise<void>;
}

let nextCdpPort = 9600;

/** Launches the packaged application with WebView2 remote debugging and attaches Playwright to it. */
export async function launchDesktop(): Promise<DesktopApp> {
  const cdpPort = nextCdpPort++;
  const launchedAt = Date.now();
  const child = spawn(desktopExe, [], {
    env: {
      ...process.env,
      WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${String(cdpPort)}`,
    },
    stdio: "ignore",
  });
  const exited = new Promise<void>((r) => {
    child.once("exit", () => {
      r();
    });
  });

  let browser: Browser | undefined;
  while (!browser) {
    try {
      browser = await chromium.connectOverCDP(`http://127.0.0.1:${String(cdpPort)}`);
    } catch (error) {
      if (Date.now() - launchedAt > 20_000 || child.exitCode !== null) throw error;
      await new Promise((r) => setTimeout(r, 100));
    }
  }
  const context = browser.contexts()[0];
  const page = context?.pages()[0] ?? (await context?.waitForEvent("page"));
  if (!page) throw new Error("No WebView page found");
  await page.waitForURL(/^http:\/\/127\.0\.0\.1:\d+\//);

  return {
    process: child,
    browser,
    page,
    baseUrl: new URL(page.url()),
    launchedAt,
    async close() {
      await browser.close().catch(() => undefined);
      // Same path as the user closing the window (WM_CLOSE): exercises the clean shutdown (F16.R3).
      execFileSync("powershell.exe", [
        "-NoProfile",
        "-Command",
        `$p = Get-Process -Id ${String(child.pid)} -ErrorAction SilentlyContinue; if ($p) { $p.CloseMainWindow() | Out-Null }`,
      ]);
      await Promise.race([exited, new Promise((r) => setTimeout(r, 10_000))]);
      if (child.exitCode === null) {
        child.kill();
        throw new Error("The application did not exit after its window was closed");
      }
    },
  };
}

/** Erable processes still alive (any instance). */
export function remainingProcesses(): number {
  const out = execFileSync(
    "tasklist",
    ["/FI", "IMAGENAME eq Erable.Desktop.exe", "/FO", "CSV", "/NH"],
    { encoding: "utf8" },
  );
  return out.split(/\r?\n/).filter((l) => l.includes("Erable.Desktop.exe")).length;
}
