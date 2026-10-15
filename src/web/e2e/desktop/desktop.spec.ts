import { expect, test } from "@playwright/test";
import { request as httpRequest } from "node:http";
import { connect } from "node:net";
import { networkInterfaces } from "node:os";
import { pathToFileURL } from "node:url";
import { chroniques } from "../fixtures/chroniques";
import { EditorPage, InsertPalette } from "../pages/EditorPage";
import { PageViewPage } from "../pages/PageViewPage";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";
import { png } from "../support/samples";
import {
  copyReferenceProject,
  foregroundProcessId,
  launchDesktop,
  remainingProcesses,
  spawnDesktop,
  tempFolder,
  type DesktopApp,
} from "./app";

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

/** URL of a page of the project the application opened at startup (`/p/<projectId>`). */
async function pageUrl(desktop: DesktopApp, pageId: string): Promise<string> {
  await desktop.page.waitForURL(/\/p\/[0-9A-F]{16}/);
  const projectId = /\/p\/([0-9A-F]{16})/.exec(desktop.page.url())?.[1] ?? "";
  return new URL(`/p/${projectId}/e/${pageId}`, desktop.page.url()).href;
}

test.afterEach(async () => {
  await app?.close();
  app = undefined;
  expect(remainingProcesses()).toBe(0);
});

// M1 version of the scenario: start, then a project, a page, a reference and an image (the search joins in M3).
test("E2E-16-01 · Start and smoke journey @desktop", async () => {
  app = await launchDesktop();
  const { page } = app;

  await expect(page.getByText(t("app.mode.desktop"))).toBeVisible();
  const startupMs = Date.now() - app.launchedAt;
  test.info().annotations.push({ type: "startup", description: `${String(startupMs)} ms` });
  expect(startupMs).toBeLessThan(3_000);
  expect(page.url()).not.toContain("session=");

  // A project.
  await page.getByRole("button", { name: t("home.new.title") }).click();
  const dialog = page.getByRole("dialog", { name: t("home.new.dialogTitle") });
  await dialog.getByLabel(t("home.new.name")).fill("Fumée");
  await dialog.getByLabel(t("home.new.location")).fill(tempFolder("smoke"));
  await dialog.getByRole("button", { name: t("home.new.create") }).click();
  await expect(page).toHaveTitle("Fumée — Erable");

  // Two pages, the second referencing the first, with an image.
  const tree = new TreePanel(page);
  const editor = new EditorPage(page);
  await tree.newPage("Destination");
  await expect(editor.content).toBeVisible();
  await tree.newPage("Départ");
  await expect(editor.title).toHaveValue("Départ");
  await editor.content.click();
  await editor.type("Vers [[desti");
  await expect(new InsertPalette(page).options.first()).toContainText("Destination");
  await page.keyboard.press("Enter");
  await editor.type(" ");
  await editor.pasteFiles(png("croquis.png"));
  await expect(editor.pill("croquis.png")).toBeVisible();
  await editor.content.focus();
  await editor.save();

  const view = new PageViewPage(page);
  await expect(view.link("Destination")).toBeVisible();
  const image = view.article.getByRole("img", { name: "croquis.png" });
  await expect
    .poll(() => image.evaluate((img: HTMLImageElement) => img.naturalWidth))
    .toBeGreaterThan(0);
});

test("E2E-16-03 · Open a folder with the native picker @desktop", async () => {
  const folder = copyReferenceProject();
  app = await launchDesktop({ env: { ERABLE_TEST_PICK_FOLDER: folder } });
  const { page } = app;
  await expect(page.getByRole("button", { name: t("home.open.title") })).toBeVisible();

  await page.keyboard.press("Control+o");

  await expect(page.getByRole("tree", { name: t("explorer.title") })).toBeVisible();
  await expect(page).toHaveTitle("Chroniques d'Aube — Erable");
  await expect(page).toHaveURL(/\/p\/[0-9A-F]{16}(\/e\/[0-9A-F]{16})?$/);
});

test("E2E-16-04 · Second instance @desktop", async () => {
  const folder = copyReferenceProject();
  app = await launchDesktop({ args: [folder] });
  await expect(app.page).toHaveTitle("Chroniques d'Aube — Erable");
  const firstPid = app.process.pid;

  const second = spawnDesktop({ args: [folder], profile: app.profile });
  const code = await Promise.race([second.exited, new Promise((r) => setTimeout(r, 15_000))]);

  expect(code, "the second instance stops by itself").toBe(0);
  await expect.poll(foregroundProcessId).toBe(firstPid);
  expect(remainingProcesses()).toBe(1);
});

test("E2E-16-05 · Clean shutdown @desktop", async () => {
  const folder = copyReferenceProject();
  app = await launchDesktop({ args: [folder] });
  const { page, profile } = app;
  const editor = new EditorPage(page);
  await page.goto(await pageUrl(app, chroniques.hautbois));
  await editor.open();
  await editor.toEnd();
  await editor.type(" Ajout avant fermeture.");

  app.requestClose();

  const confirm = page.getByRole("alertdialog", { name: t("editor.closeTitle") });
  await expect(confirm).toBeVisible();
  for (const label of [t("editor.save"), t("editor.discard"), t("common.cancel")]) {
    await expect(confirm.getByRole("button", { name: label })).toBeVisible();
  }
  await confirm.getByRole("button", { name: t("editor.save") }).click();
  expect(await Promise.race([app.exited, new Promise((r) => setTimeout(r, 10_000))])).toBe(0);
  await app.browser.close().catch(() => undefined);
  expect(remainingProcesses()).toBe(0);

  // Reopened: the change is there.
  app = await launchDesktop({ args: [folder], profile });
  await app.page.goto(await pageUrl(app, chroniques.hautbois));
  await expect(new PageViewPage(app.page).content).toContainText("Ajout avant fermeture.");
});

test("E2E-16-07 · Drop a folder on the home screen @desktop", async () => {
  const folder = copyReferenceProject();
  app = await launchDesktop();
  const { page } = app;
  await expect(page.getByRole("button", { name: t("home.open.title") })).toBeVisible();

  // Dropping a folder makes the WebView navigate to its file:/// URL, which the shell cancels and turns into an
  // « open this folder » event. A browser-initiated navigation takes that same path; CDP drag events do not run the
  // drop's default action, and a real OS drag would need the mouse and the foreground of the person's desktop.
  const cdp = await page.context().newCDPSession(page);
  const navigation = await cdp.send("Page.navigate", { url: pathToFileURL(folder).href });

  expect(navigation.errorText, "the shell cancels the navigation").toBe("net::ERR_ABORTED");
  await expect(page.getByRole("tree", { name: t("explorer.title") })).toBeVisible();
  await expect(page).toHaveTitle("Chroniques d'Aube — Erable");
  await expect(page).toHaveURL(/\/p\/[0-9A-F]{16}(\/e\/[0-9A-F]{16})?$/);
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
