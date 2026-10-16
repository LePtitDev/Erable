import { expect, test } from "@playwright/test";
import { spawn, type ChildProcess } from "node:child_process";
import { createServer, type Server } from "node:http";
import { mkdtempSync } from "node:fs";
import { tmpdir } from "node:os";
import { join, resolve } from "node:path";
import { t } from "../support/i18n";
import { users } from "../support/users";
import { launchDesktop, remainingProcesses, type DesktopApp } from "./app";

test.skip(
  process.platform !== "win32",
  "CDP attach is Windows-only; macOS and Linux run --self-test",
);

const repo = resolve(import.meta.dirname, "..", "..", "..", "..");

/** A real Erable server (the same executable, in test mode) on a free port, with « Chroniques d'Aube » and the test users. */
async function startServer(): Promise<{ origin: string; stop: () => void }> {
  const probe = createServer();
  await new Promise<void>((r) => probe.listen(0, "127.0.0.1", r));
  const port = (probe.address() as { port: number }).port;
  await new Promise<void>((r) => {
    probe.close(() => {
      r();
    });
  });

  const data = mkdtempSync(join(tmpdir(), "erable-e2e-server-"));
  const child: ChildProcess = spawn(
    "dotnet",
    [
      "run",
      "--project",
      join(repo, "src", "Erable.Server"),
      "--configuration",
      "Debug",
      "--no-launch-profile",
      "--",
      "serve",
      data,
      "--port",
      String(port),
    ],
    {
      stdio: "ignore",
      env: {
        ...process.env,
        ERABLE_TEST_MODE: "1",
        ERABLE_TEST_FIXTURES: join(repo, "spec", "projects"),
        ASPNETCORE_ENVIRONMENT: "Development",
      },
    },
  );
  const origin = `http://127.0.0.1:${String(port)}`;
  const deadline = Date.now() + 120_000;
  for (;;) {
    try {
      if ((await fetch(`${origin}/api/info`)).ok) break;
    } catch {
      // not up yet
    }
    if (Date.now() > deadline || child.exitCode !== null)
      throw new Error("The server did not start");
    await new Promise((r) => setTimeout(r, 250));
  }
  const reset = await fetch(`${origin}/_test/reset?slot=desktop&fixture=chroniques&user=`, {
    method: "POST",
  });
  if (!reset.ok) throw new Error(`/_test/reset failed: ${String(reset.status)}`);
  return {
    origin,
    stop: () => {
      child.kill();
    },
  };
}

let app: DesktopApp | undefined;
let stopServer: (() => void) | undefined;
let fake: Server | undefined;

test.afterEach(async () => {
  await app?.close();
  app = undefined;
  stopServer?.();
  stopServer = undefined;
  fake?.close();
  fake = undefined;
  expect(remainingProcesses()).toBe(0);
});

test("E2E-01-06 · Connect to a server @server @desktop", async () => {
  test.setTimeout(240_000);
  const server = await startServer();
  stopServer = server.stop;
  app = await launchDesktop();
  const { page } = app;

  await page.getByRole("button", { name: t("home.connect.title") }).click();
  const dialog = page.getByRole("dialog", { name: t("servers.connect.title") });
  await dialog.getByLabel(t("servers.connect.url")).fill(server.origin);
  await dialog.getByRole("button", { name: t("servers.connect.next") }).click();
  await dialog.getByLabel(t("auth.login")).fill(users.camille.id);
  await dialog.getByLabel(t("auth.password")).fill(users.camille.password);
  await dialog.getByRole("button", { name: t("auth.submit") }).click();

  // The projects of the server, with the one camille belongs to…
  const servers = page.getByRole("region", { name: t("servers.title") });
  const host = new URL(server.origin).host;
  await expect(
    servers.getByRole("list", { name: t("servers.projectsOf", { name: host }) }),
  ).toBeVisible();
  await expect(servers.getByRole("link", { name: "Chroniques d'Aube" })).toBeVisible();
  // …and the server itself in the recent list, marked « Serveur ».
  const row = servers.getByRole("listitem").filter({ hasText: host }).first();
  await expect(row).toContainText(t("servers.mode"));

  // The project opens from there: its tree is the one of the server, read through the application.
  await servers.getByRole("link", { name: "Chroniques d'Aube" }).click();
  await expect(page.getByRole("tree", { name: t("explorer.title") })).toBeVisible();
  await expect(page.getByRole("status").filter({ hasText: host })).toBeVisible();
});

test("E2E-16-06 · Incompatible server @desktop @server", async () => {
  const requests: string[] = [];
  fake = createServer((request, response) => {
    requests.push(`${request.method ?? ""} ${request.url ?? ""}`);
    response.setHeader("Content-Type", "application/json");
    response.end(
      JSON.stringify({
        appVersion: "9.0.0",
        apiVersion: "v9",
        formatVersion: 42,
        mode: "cloud",
        folderProjects: false,
      }),
    );
  });
  await new Promise<void>((r) => fake?.listen(0, "127.0.0.1", r));
  const origin = `http://127.0.0.1:${String((fake.address() as { port: number }).port)}`;
  app = await launchDesktop();
  const { page } = app;

  await page.getByRole("button", { name: t("home.connect.title") }).click();
  const dialog = page.getByRole("dialog", { name: t("servers.connect.title") });
  await dialog.getByLabel(t("servers.connect.url")).fill(origin);
  await dialog.getByRole("button", { name: t("servers.connect.next") }).click();

  const message = dialog.getByRole("alert");
  await expect(message).toContainText("v9");
  await expect(message).toContainText("9.0.0");
  await expect(message).toContainText("v1");
  await expect(dialog.getByLabel(t("auth.password"))).toHaveCount(0);
  // Nothing but the handshake reached the server.
  expect(requests).toEqual(["GET /api/info"]);
});
