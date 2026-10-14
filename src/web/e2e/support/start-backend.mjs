// Starts the real backend for Playwright: builds the SPA, then `erable serve` in test mode on a fresh data
// folder. The backend serves the built SPA from wwwroot, exactly like in production.
// Usage: node e2e/support/start-backend.mjs [--port 5181] [--skip-build]
import { execFileSync, spawn } from "node:child_process";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";

const webDir = resolve(import.meta.dirname, "..", "..");
const serverProject = resolve(webDir, "..", "Erable.Server");
const portIndex = process.argv.indexOf("--port");
const port = portIndex > 0 ? process.argv[portIndex + 1] : "5181";

if (!process.argv.includes("--skip-build")) {
  execFileSync("corepack", ["pnpm", "exec", "vite", "build"], {
    cwd: webDir,
    stdio: "inherit",
    shell: true,
  });
}

const data = mkdtempSync(join(tmpdir(), "erable-e2e-"));
// Debug configuration: /_test endpoints are compiled out of Release builds.
const server = spawn(
  "dotnet",
  [
    "run",
    "--project",
    serverProject,
    "--configuration",
    "Debug",
    "--no-launch-profile",
    "--",
    "serve",
    data,
    "--port",
    port,
  ],
  {
    stdio: "inherit",
    env: { ...process.env, ERABLE_TEST_MODE: "1", ASPNETCORE_ENVIRONMENT: "Development" },
  },
);
for (const signal of ["SIGINT", "SIGTERM"]) process.on(signal, () => server.kill(signal));
server.on("exit", (code) => process.exit(code ?? 0));
