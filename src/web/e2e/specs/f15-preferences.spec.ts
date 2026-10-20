import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { t } from "../support/i18n";
import { PageViewPage } from "../pages/PageViewPage";
import { backgroundLuminance, PreferencesDialog } from "../pages/PreferencesDialog";

test.describe("F15 · Preferences", () => {
  test("E2E-15-01 · Change the interface language @pr", async ({ page, reference }) => {
    const preferences = new PreferencesDialog(page);
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.link("Hautbois")).toBeVisible();

    await preferences.openFromProject();
    await preferences.language.selectOption({ label: "English" });
    await page.keyboard.press("Escape");

    // English labels (« Properties » joins with the properties panel).
    await expect(page.getByRole("heading", { name: "Explorer", exact: true })).toBeVisible();
    await expect(page.getByRole("button", { name: "Edit", exact: true })).toBeVisible();
    await expect(page.locator("html")).toHaveAttribute("lang", "en");
    // Content and element names are unchanged.
    await expect(view.title).toHaveText("Ilsa Varenne");
    await expect(view.content).toContainText(
      "Ilsa Varenne tient le comptoir d’échange de Hautbois, au pied du Col gelé.",
    );
    await expect(view.content).toContainText("Marchande de cartes et d’équipement de montagne.");
    await expect(page.getByRole("treeitem", { name: "Ilsa Varenne", exact: true })).toBeVisible();
    // Dates in English.
    await expect(view.article).toContainText(/Modified 2 hr\. ago by Camille R\./);

    // Remembered: the next load starts in English.
    await page.reload();
    await expect(page.getByRole("button", { name: "Edit", exact: true })).toBeVisible();
  });

  test("E2E-15-02 · Dark theme @pr", async ({ page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    const preferences = new PreferencesDialog(page);
    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");

    await preferences.openFromHome();
    await preferences.theme("dark").check();

    await expect(page.locator("html")).toHaveAttribute("data-theme", "dark");
    expect(await backgroundLuminance(page)).toBeLessThan(0.3);

    // The first render after a reload is already dark: the theme is in the served HTML.
    const response = await page.reload();
    expect(await response?.text()).toContain('data-theme="dark"');
    expect(await backgroundLuminance(page)).toBeLessThan(0.3);
  });

  test("E2E-15-03 · System theme @nightly", async ({ page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    const preferences = new PreferencesDialog(page);
    await page.emulateMedia({ colorScheme: "light" });
    await page.goto("/");
    await preferences.openFromHome();
    await preferences.theme("system").check();
    expect(await backgroundLuminance(page)).toBeGreaterThan(0.7);

    await page.emulateMedia({ colorScheme: "dark" });

    await expect.poll(() => backgroundLuminance(page)).toBeLessThan(0.3);
    await expect(page.locator("html")).toHaveAttribute("data-theme", "system");
  });

  test("E2E-15-04 · Shortcut conflict @nightly", async ({ page, reference }) => {
    const preferences = new PreferencesDialog(page);
    await page.goto(reference.url(chroniques.ilsa));

    await preferences.openFromProject();
    await preferences.assignShortcut("openPalette", "Control+k");

    const alert = preferences.dialog.getByRole("alert");
    await expect(alert).toContainText(
      t("shortcuts.conflict", { action: t("shortcuts.actions.quickSearch"), combo: "Ctrl+K" }),
    );
    await expect(alert).toContainText("Recherche");
    // Replacing gives the combination to the palette; the search loses it.
    await alert.getByRole("button", { name: t("shortcuts.replace") }).click();
    await expect(preferences.shortcut("openPalette")).toContainText("Ctrl+K");
    await expect(preferences.shortcut("quickSearch")).toContainText(t("shortcuts.none"));

    await preferences.dialog.getByRole("button", { name: t("shortcuts.resetAll") }).click();
    await expect(preferences.shortcut("quickSearch")).toContainText("Ctrl+K");
  });
});
