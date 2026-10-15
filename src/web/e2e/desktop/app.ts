import { chromium, type Browser, type Page } from "@playwright/test";
import { execFileSync, spawn, type ChildProcess } from "node:child_process";
import { cpSync, mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

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
  readonly profile: string;
  /** Resolves with the exit code once the process has ended. */
  readonly exited: Promise<number | null>;
  /** Closes the window like a user (WM_CLOSE), without waiting for the process to end. */
  requestClose(): void;
  close(): Promise<void>;
}

export interface LaunchOptions {
  /** Command line, e.g. a project folder (`Erable.Desktop <folder>`). */
  readonly args?: readonly string[];
  /** Application profile; a fresh temporary one by default (never the profile of the person running the tests). */
  readonly profile?: string;
  readonly env?: Readonly<Record<string, string>>;
}

let nextCdpPort = 9600;

/** A fresh, empty folder for a test (application profile, projects). */
export function tempFolder(prefix: string): string {
  return mkdtempSync(join(tmpdir(), `erable-desktop-${prefix}-`));
}

/** A private copy of « Chroniques d'Aube », for tests that open a project from its folder. */
export function copyReferenceProject(): string {
  const folder = join(tempFolder("project"), "Chroniques d'Aube");
  cpSync(
    resolve(import.meta.dirname, "..", "..", "..", "..", "spec", "projects", "chroniques"),
    folder,
    {
      recursive: true,
    },
  );
  return folder;
}

/** Starts the packaged application without attaching to it (the second instance of E2E-16-04). */
export function spawnDesktop(options: LaunchOptions & { cdpPort?: number }): {
  child: ChildProcess;
  exited: Promise<number | null>;
} {
  const child = spawn(desktopExe, [...(options.args ?? [])], {
    env: {
      ...process.env,
      // Test mode: the folder picker returns ERABLE_TEST_PICK_FOLDER instead of showing (E2E-16-03).
      ERABLE_TEST_MODE: "1",
      ERABLE_PROFILE_DIR: options.profile ?? tempFolder("profile"),
      ...(options.cdpPort === undefined
        ? {}
        : {
            WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS: `--remote-debugging-port=${String(options.cdpPort)}`,
          }),
      ...options.env,
    },
    stdio: "ignore",
  });
  const exited = new Promise<number | null>((r) => {
    child.once("exit", (code) => {
      r(code);
    });
  });
  return { child, exited };
}

/** Launches the packaged application with WebView2 remote debugging and attaches Playwright to it. */
export async function launchDesktop(options: LaunchOptions = {}): Promise<DesktopApp> {
  const cdpPort = nextCdpPort++;
  const launchedAt = Date.now();
  const profile = options.profile ?? tempFolder("profile");
  const { child, exited } = spawnDesktop({ ...options, profile, cdpPort });

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

  const requestClose = () => {
    execFileSync("powershell.exe", [
      "-NoProfile",
      "-Command",
      `$p = Get-Process -Id ${String(child.pid)} -ErrorAction SilentlyContinue; if ($p) { $p.CloseMainWindow() | Out-Null }`,
    ]);
  };

  return {
    process: child,
    browser,
    page,
    baseUrl: new URL(page.url()),
    launchedAt,
    profile,
    exited,
    requestClose,
    async close() {
      if (child.exitCode !== null) return;
      await browser.close().catch(() => undefined);
      // Same path as the user closing the window (WM_CLOSE): exercises the clean shutdown (F16.R3).
      requestClose();
      const ended = await Promise.race([
        exited.then(() => true),
        new Promise<false>((r) =>
          setTimeout(() => {
            r(false);
          }, 10_000),
        ),
      ]);
      if (!ended) {
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

/** Process that owns the foreground window (Windows). */
export function foregroundProcessId(): number {
  const script = [
    'Add-Type -Namespace W -Name U -MemberDefinition \'[DllImport("user32.dll")] public static extern System.IntPtr GetForegroundWindow(); [DllImport("user32.dll")] public static extern uint GetWindowThreadProcessId(System.IntPtr h, out uint p);\'',
    "$p = 0; [W.U]::GetWindowThreadProcessId([W.U]::GetForegroundWindow(), [ref]$p) | Out-Null; $p",
  ].join("; ");
  return Number(
    execFileSync("powershell.exe", ["-NoProfile", "-Command", script], { encoding: "utf8" }).trim(),
  );
}
