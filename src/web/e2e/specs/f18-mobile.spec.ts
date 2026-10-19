import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { LoginPage } from "../pages/LoginPage";
import { PageViewPage } from "../pages/PageViewPage";
import { PreferencesDialog } from "../pages/PreferencesDialog";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

interface Manifest {
  name: string;
  start_url: string;
  display: string;
  theme_color: string;
  icons: { src: string; sizes: string; type: string; purpose?: string }[];
}

test.describe("F18 · Mobile version", () => {
  test("E2E-18-01 · Explorer as a drawer @mobile @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.title).toHaveText("Ilsa Varenne");

    // Hidden, and the page takes the whole width.
    await expect(tree.tree).toBeHidden();
    const width = page.viewportSize()?.width ?? 0;
    expect((await page.locator("main").boundingBox())?.width).toBe(width);

    await page.getByRole("button", { name: t("shell.showExplorer") }).tap();
    await expect(page.getByRole("dialog", { name: t("explorer.label") })).toBeVisible();
    // A folder unfolds instead of opening (the drawer stays), a page opens and closes it.
    await tree.item("Lieux").tap();
    await expect(tree.item("Lieux")).toHaveAttribute("aria-expanded", "true");
    await tree.item("Hautbois").tap();
    await expect(view.title).toHaveText("Hautbois");
    await expect(tree.tree).toBeHidden();

    // The wide layout was left alone: the explorer is open, at its width.
    await page.setViewportSize({ width: 1280, height: 800 });
    await expect(tree.tree).toBeVisible();
    await expect(page.getByRole("separator", { name: t("shell.resizeExplorer") })).toHaveAttribute(
      "aria-valuenow",
      "280",
    );
  });

  test("E2E-18-09 · Preferences and account from the More menu @mobile", async ({
    page,
    reference,
  }) => {
    const view = new PageViewPage(page);
    const more = page.getByRole("button", { name: t("shell.more") });
    const item = (name: string) => page.getByRole("menuitem", { name });
    await page.goto(reference.url(chroniques.ilsa));
    await view.link("Hautbois").tap();
    await expect(view.title).toHaveText("Hautbois");

    // Back.
    await more.tap();
    await item(t("shell.back")).tap();
    await expect(view.title).toHaveText("Ilsa Varenne");

    // Theme.
    await more.tap();
    await item(t("shell.darkTheme")).tap();
    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");

    // Language, through the preferences.
    const preferences = new PreferencesDialog(page);
    await more.tap();
    await item(t("shell.preferences", { language: "FR" })).tap();
    await preferences.language.selectOption({ label: "English" });
    await page.keyboard.press("Escape");
    await expect(page.locator("html")).toHaveAttribute("lang", "en");

    // Sign out (the menu is in English now).
    await page.getByRole("button", { name: "More actions" }).tap();
    await page.getByRole("menuitem", { name: "Sign out" }).tap();
    // Signed out, the screen follows the browser language again.
    await expect(new LoginPage(page).form).toBeVisible();
  });

  test("E2E-18-11 · Installable web application @mobile", async ({ page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    await page.goto("/");

    const href = await page.locator('link[rel="manifest"]').getAttribute("href");
    expect(href).not.toBeNull();
    const response = await page.request.get(href ?? "");
    expect(response.ok()).toBe(true);
    expect(response.headers()["content-type"]).toContain("application/manifest+json");
    const manifest = (await response.json()) as Manifest;
    expect(manifest.name).toBe("Erable");
    expect(manifest.start_url).toBe("/");
    expect(manifest.display).toBe("standalone");
    expect(manifest.theme_color).toMatch(/^#[0-9a-f]{6}$/i);

    for (const size of ["192x192", "512x512"]) {
      const icon = manifest.icons.find((i) => i.sizes === size && i.type === "image/png");
      expect(icon, `icon ${size}`).toBeDefined();
      const image = await page.request.get(icon?.src ?? "");
      expect(image.ok()).toBe(true);
      expect(image.headers()["content-type"]).toBe("image/png");
    }
    expect(manifest.icons.some((i) => i.purpose === "maskable")).toBe(true);
    await expect(page.locator('meta[name="theme-color"]')).toHaveCount(2);
  });
});
