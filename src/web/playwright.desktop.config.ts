import { defineConfig } from "@playwright/test";

/**
 * Desktop smoke tests on the packaged binary (docs/testing.md §10), Windows only: Playwright attaches to
 * WebView2 through CDP. macOS and Linux use `Erable.Desktop --self-test` instead.
 * Binary: ERABLE_DESKTOP_EXE, default artifacts/desktop/win-x64/Erable/Erable.Desktop.exe
 * (node build/publish-desktop.mjs win-x64).
 */
export default defineConfig({
  testDir: "./e2e/desktop",
  workers: 1,
  retries: 0,
  reporter: process.env["CI"]
    ? [["list"], ["html", { open: "never", outputFolder: "playwright-report-desktop" }]]
    : "list",
  use: { trace: "retain-on-failure" },
});
