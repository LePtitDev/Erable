import { defineConfig, devices } from "@playwright/test";

const port = Number(process.env["ERABLE_E2E_PORT"] ?? 5181);
const ci = process.env["CI"] !== undefined;

/**
 * End-to-end tests against the real backend in test mode (docs/testing.md §9).
 * Chromium and WebKit on every PR; Firefox is reserved for the nightly pipeline.
 */
export default defineConfig({
  testDir: "./e2e/specs",
  fullyParallel: true,
  forbidOnly: ci,
  // Flaky tests are quarantined, never retried silently (docs/testing.md §1).
  retries: 0,
  // Reference screenshots of the visual project, versioned next to the specs (e2e/screenshots/README.md).
  snapshotPathTemplate: "e2e/screenshots/{arg}{ext}",
  expect: { toHaveScreenshot: { maxDiffPixelRatio: 0.002 } },
  reporter: ci
    ? [["list"], ["html", { open: "never" }], ["junit", { outputFile: "test-results/junit.xml" }]]
    : "list",
  use: {
    baseURL: `http://127.0.0.1:${port}`,
    locale: "fr-FR",
    timezoneId: "Europe/Paris",
    trace: "retain-on-failure",
    video: "retain-on-failure",
    screenshot: "only-on-failure",
  },
  // Timing measurements (@perf) are meaningless under a full parallel run: they run alone, `pnpm e2e:perf`.
  projects: [
    // Screenshots are compared in a fixed container only (`pnpm e2e:visual`): their own project, left out of the others.
    { name: "chromium", use: { ...devices["Desktop Chrome"] }, grepInvert: /@perf|@visual/ },
    { name: "webkit", use: { ...devices["Desktop Safari"] }, grepInvert: /@perf|@visual/ },
    { name: "perf", use: { ...devices["Desktop Chrome"] }, grep: /@perf/ },
    {
      name: "visual",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1280, height: 800 },
        deviceScaleFactor: 1,
      },
      grep: /@visual/,
    },
  ],
  webServer: {
    command: `node e2e/support/start-backend.mjs --port ${port}`,
    url: `http://127.0.0.1:${port}/api/info`,
    reuseExistingServer: !ci,
    timeout: 240_000,
    stdout: "ignore",
    stderr: "pipe",
  },
});
