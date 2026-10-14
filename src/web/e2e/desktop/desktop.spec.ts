import { expect, test } from "@playwright/test";
import { request as httpRequest } from "node:http";
import { connect } from "node:net";
import { networkInterfaces } from "node:os";
import { t } from "../support/i18n";
import { launchDesktop, remainingProcesses, type DesktopApp } from "./app";

test.skip(
  process.platform !== "win32",
  "CDP attach is Windows-only; macOS and Linux run --self-test",
);

/** Raw HTTP so that the Host header can be forged (fetch forbids it). */
function get(url: URL, path: string, headers: Record<string, string> = {}): Promise<number> {
  return new Promise((resolve, reject) => {
    const req = httpRequest({ host: url.hostname, port: url.port, path, headers }, (res) => {
      res.resume();
      resolve(res.statusCode ?? 0);
    });
    req.on("error", reject);
    req.end();
  });
}

function canConnect(host: string, port: number): Promise<boolean> {
  return new Promise((resolve) => {
    const socket = connect({ host, port, timeout: 500 });
    socket.once("connect", () => {
      socket.destroy();
      resolve(true);
    });
    socket.once("timeout", () => {
      socket.destroy();
      resolve(false);
    });
    socket.once("error", () => {
      resolve(false);
    });
  });
}

let app: DesktopApp | undefined;

test.afterEach(async () => {
  await app?.close();
  app = undefined;
  expect(remainingProcesses()).toBe(0);
});

// M0 version of the scenario: start and home screen. The full journey grows with each milestone.
test("E2E-16-01 · Start and smoke journey @desktop", async () => {
  app = await launchDesktop();

  await expect(app.page.getByRole("heading", { level: 1 })).toHaveText(t("app.name"));
  await expect(app.page.getByText(t("app.mode.desktop"))).toBeVisible();
  const startupMs = Date.now() - app.launchedAt;

  test.info().annotations.push({ type: "startup", description: `${String(startupMs)} ms` });
  expect(startupMs).toBeLessThan(3_000);
  expect(app.page.url()).not.toContain("session=");
});

test("E2E-16-02 · Local server is protected @desktop", async () => {
  app = await launchDesktop();
  const { baseUrl } = app;
  const port = Number(baseUrl.port);

  expect(baseUrl.hostname).toBe("127.0.0.1");
  expect(await get(baseUrl, "/api/info")).toBe(401);
  expect(await get(baseUrl, "/", { Host: `evil.example:${String(port)}` })).toBe(400);

  const external = Object.values(networkInterfaces())
    .flat()
    .filter((a) => a !== undefined && a.family === "IPv4" && !a.internal)
    .map((a) => a?.address ?? "");
  for (const address of external) {
    expect(await canConnect(address, port), `reachable through ${address}`).toBe(false);
  }

  // The SPA itself is authenticated through its HttpOnly cookie.
  expect(await app.page.evaluate(() => fetch("/api/info").then((r) => r.status))).toBe(200);
  expect(await app.page.evaluate(() => document.cookie)).toBe("");
});
