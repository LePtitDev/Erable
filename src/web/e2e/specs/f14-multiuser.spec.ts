import { request as playwrightRequest } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { LoginPage } from "../pages/LoginPage";
import { RecentPanel } from "../pages/RecentPanel";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";
import { users } from "../support/users";

const sessionCookie = "erable_auth";

test.describe("F14 · Signing in", () => {
  // Anonymous: the login screen, not a signed-in test.
  test.use({ user: "" });

  test("E2E-14-01 · Sign in and out @server", async ({ page, reference, baseURL }) => {
    const login = new LoginPage(page);

    await page.goto(reference.url(chroniques.ilsa));
    await expect(login.form).toBeVisible();
    await login.signIn(users.camille.id, users.camille.password);

    await expect(login.avatar(users.camille.name)).toBeVisible();
    await expect(page.getByRole("heading", { level: 1, name: "Ilsa Varenne" })).toBeVisible();
    const cookies = await page.context().cookies();
    const old = cookies.find((c) => c.name === sessionCookie);
    expect(old?.httpOnly).toBe(true);

    await login.signOut(users.camille.name);
    await expect(login.form).toBeVisible();

    // The cookie of the ended session no longer opens anything.
    const stranger = await playwrightRequest.newContext({
      ...(baseURL === undefined ? {} : { baseURL }),
      extraHTTPHeaders: { Cookie: `${sessionCookie}=${old?.value ?? ""}` },
    });
    expect((await stranger.get("/api/v1/me")).status()).toBe(401);
    await stranger.dispose();
  });

  test("E2E-14-02 · Invalid credentials @server", async ({ page, reference }) => {
    const login = new LoginPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    await login.signIn(users.camille.id, "pas-le-bon-mot-de-passe");
    await expect(login.error).toHaveText(t("errors.auth.invalidCredentials"));

    // An unknown login gets the very same message: the screen does not say which one was wrong.
    await login.signIn("personne", "pas-le-bon-mot-de-passe");
    await expect(login.error).toHaveText(t("errors.auth.invalidCredentials"));
    await expect(login.form).toBeVisible();
  });
});

test.describe("F14 · Roles and isolation", () => {
  test.describe("a reader", () => {
    test.use({ user: "lea" });

    test("E2E-14-03 · Read-only reader @server", async ({ page, reference }) => {
      await page.goto(reference.url(chroniques.ilsa));
      await expect(page.getByRole("heading", { level: 1, name: "Ilsa Varenne" })).toBeVisible();

      await expect(page.getByRole("button", { name: t("page.edit") })).toHaveCount(0);
      await expect(page.getByRole("button", { name: t("links.link") })).toHaveCount(0);
      await expect(page.getByRole("button", { name: t("attachments.addLabel") })).toHaveCount(0);
      await expect(page.getByRole("button", { name: t("page.newSubPage") })).toHaveCount(0);
      await expect(page.getByRole("button", { name: t("explorer.newPage") })).toHaveCount(0);
      await expect(
        page.getByRole("status").filter({ hasText: t("shell.status.readOnly") }),
      ).toBeVisible();

      // The tree offers nothing to change: no context menu.
      const tree = new TreePanel(page);
      await tree.item("Personnages").click({ button: "right" });
      await expect(page.getByRole("menuitem")).toHaveCount(0);

      // A forged write is refused by the server whatever the interface shows.
      const forged = await page.request.put(
        `/api/v1/projects/${reference.id}/pages/${chroniques.ilsa}/content`,
        { headers: { "If-Match": '"1"' }, data: { markdown: "Texte forgé" } },
      );
      expect(forged.status()).toBe(403);
      const content = await page.request.get(
        `/api/v1/projects/${reference.id}/pages/${chroniques.ilsa}/content`,
      );
      expect(await content.text()).not.toContain("Texte forgé");
    });

    test("E2E-14-03 · A reader following an edit link reads @server", async ({
      page,
      reference,
    }) => {
      await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);

      await expect(page.getByRole("heading", { level: 1, name: "Ilsa Varenne" })).toBeVisible();
      await expect(page.getByRole("textbox", { name: t("editor.content") })).toHaveCount(0);
    });
  });

  test.describe("a stranger", () => {
    test.use({ user: "externe", otherProject: true });

    test("E2E-14-04 · Projects stay apart @server", async ({ page, reference }) => {
      await page.goto("/");
      await expect(page.getByRole("heading", { name: t("home.server.title") })).toBeVisible();
      await expect(page.getByText("Autre projet")).toBeVisible();
      await expect(page.getByText(reference.name)).toHaveCount(0);

      await page.goto(reference.url(chroniques.ilsa));

      await expect(
        page.getByRole("heading", { level: 1, name: t("accessDenied.title") }),
      ).toBeVisible();
      await expect(page.getByText("Ilsa Varenne")).toHaveCount(0);
      await expect(page.getByText(reference.name)).toHaveCount(0);
    });
  });
});

test.describe("F11 · Views belong to each user", () => {
  test.use({ user: "camille" });

  test("E2E-11-03 · Each user has their own views @multi @server", async ({
    page,
    reference,
    openAs,
  }) => {
    const malo = await openAs("malo");
    const camilleRecent = new RecentPanel(page);

    await page.goto(reference.url(chroniques.hautbois));
    await expect(page.getByRole("heading", { level: 1, name: "Hautbois" })).toBeVisible();
    await camilleRecent.dwell();
    await expect(camilleRecent.entries.first()).toContainText("Hautbois");

    await malo.goto(reference.url(chroniques.pnj));
    await expect(malo.getByRole("heading", { level: 1, name: "PNJ" })).toBeVisible();
    const maloRecent = new RecentPanel(malo);
    await expect(maloRecent.section).toBeVisible();
    await expect(maloRecent.entries.filter({ hasText: "Hautbois" })).toHaveCount(0);

    // A change of camille is a change for everybody: it appears among the edited elements of malo.
    const current = await page.request.get(
      `/api/v1/projects/${reference.id}/pages/${chroniques.hautbois}/content`,
    );
    const { revision } = (await current.json()) as { revision: number };
    const saved = await page.request.put(
      `/api/v1/projects/${reference.id}/pages/${chroniques.hautbois}/content`,
      { headers: { "If-Match": `"${String(revision)}"` }, data: { markdown: "Un nouveau puits." } },
    );
    expect(saved.ok()).toBe(true);
    await malo.goto(`/p/${reference.id}/search`);
    await malo.getByRole("tab", { name: t("recent.edited") }).click();
    await expect(malo.getByRole("complementary", { name: t("search.activity") })).toContainText(
      "Hautbois",
    );
  });
});
