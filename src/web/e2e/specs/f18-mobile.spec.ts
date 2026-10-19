import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage, InsertPalette } from "../pages/EditorPage";
import { LoginPage } from "../pages/LoginPage";
import { PageViewPage } from "../pages/PageViewPage";
import { PagePropertiesPanel } from "../pages/PagePropertiesPanel";
import { PreferencesDialog } from "../pages/PreferencesDialog";
import { QuickSearchPage } from "../pages/QuickSearchPage";
import { SearchPage } from "../pages/SearchPage";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";
import { expectNoHorizontalScroll, expectTouchTargets } from "../support/mobile";
import { screens } from "../support/screens";

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

  test("E2E-18-02 · No horizontal scrolling @mobile", async ({ page, reference }) => {
    // The six screens of the mockup, then the others.
    for (const screen of screens) {
      await screen.open(page, reference);
      await expectNoHorizontalScroll(page, screen.name);
    }
    const others = [
      { name: "trash", url: `/p/${reference.id}/trash` },
      { name: "history", url: `/p/${reference.id}/e/${chroniques.ilsa}/history` },
      { name: "members", url: `/p/${reference.id}/members` },
      { name: "folder", url: `/p/${reference.id}/e/0000000000000001` },
    ];
    for (const other of others) {
      await page.goto(other.url);
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
      await expectNoHorizontalScroll(page, other.name);
    }
    // Signed out: the login screen.
    await page.context().clearCookies();
    await page.goto("/");
    await expect(new LoginPage(page).form).toBeVisible();
    await expectNoHorizontalScroll(page, "login");
  });

  test("E2E-18-03 · Read a page on a phone @mobile @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.title).toHaveText("Ilsa Varenne");

    // Title, categories and the start of the text without scrolling.
    const height = page.viewportSize()?.height ?? 0;
    for (const visible of [view.title, new PagePropertiesPanel(page).categories, view.content]) {
      await expect(visible).toBeInViewport();
    }
    expect((await view.title.boundingBox())?.y ?? height).toBeLessThan(height);
    // Cut between words: every word of the title sits on one line.
    const splitWords = await view.title.evaluate((title) => {
      const text = title.firstChild;
      if (text === null) return [];
      const content = text.textContent ?? "";
      const split: string[] = [];
      let start = 0;
      for (const word of content.split(" ")) {
        const range = document.createRange();
        range.setStart(text, start);
        range.setEnd(text, start + word.length);
        const lines = new Set([...range.getClientRects()].map((r) => Math.round(r.top)));
        if (lines.size > 1) split.push(word);
        start += word.length + 1;
      }
      return split;
    });
    expect(splitWords).toEqual([]);

    // The page panel, folded above the content.
    const panel = page.getByRole("button", { name: t("page.sidePanel") });
    await expect(panel).toHaveAttribute("aria-expanded", "false");
    await panel.tap();
    const side = page.getByRole("complementary", { name: t("page.sidePanel") });
    await expect(side).toContainText(t("page.via", { categories: "PNJ" }));
    await expect(side).toContainText("Allié de");
    await expect(side.getByRole("link", { name: "Capitaine Orsen" }).first()).toBeVisible();

    await view.link("Hautbois").tap();
    await expect(view.title).toHaveText("Hautbois");
  });

  test("E2E-18-04 · Edit and save a page @mobile @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    const editor = new EditorPage(page);
    const palette = new InsertPalette(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.link("Capitaine Orsen")).toBeVisible();
    const before = await view.content.getByRole("link", { name: "Capitaine Orsen" }).count();

    await page.getByRole("button", { name: t("page.edit") }).tap();
    await expect(editor.content).toBeVisible();
    // The editor takes the width; the panel waits behind its own button (F18.R6).
    await expect(page.getByRole("tablist", { name: t("page.tabs.label") })).toBeHidden();
    await editor.content.tap();
    await page.keyboard.press("Control+End");
    await page.keyboard.press("Enter");
    await page.keyboard.press("Enter");
    await editor.type("Voir [[orse");
    await expect(palette.dialog).toBeVisible();
    await expect(palette.dialog).toBeInViewport({ ratio: 1 });
    await palette.options.filter({ hasText: "Capitaine Orsen" }).first().tap();
    await expect(palette.dialog).toBeHidden();

    await page.getByRole("button", { name: t("editor.save") }).tap();
    await expect(page).not.toHaveURL(/edit=true/);
    await expect(view.content.getByRole("link", { name: "Capitaine Orsen" })).toHaveCount(
      before + 1,
    );
  });

  test("E2E-18-05 · Properties while editing on a phone @mobile", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    const view = new PageViewPage(page);
    await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);
    const panelButton = page.getByRole("button", { name: t("page.sidePanel") });
    await expect(panelButton).toHaveAttribute("aria-pressed", "false");

    await panelButton.tap();
    await expect(page.getByRole("textbox", { name: t("editor.content") })).toBeHidden();
    const niveau = panel.field("Niveau");
    await expect(niveau).toContainText(t("property.violation.less_or_equal", { value: 60 }));
    await niveau.getByRole("textbox", { name: "Niveau" }).fill("50");
    await expect(niveau).not.toContainText(t("property.violation.less_or_equal", { value: 60 }));

    await page.getByRole("button", { name: t("editor.save") }).tap();
    await expect(page).not.toHaveURL(/edit=true/);
    await page.getByRole("button", { name: t("page.sidePanel") }).tap();
    await expect(panel.row("Niveau")).toContainText("50");
    await expect(view.title).toHaveText("Ilsa Varenne");
  });

  test("E2E-18-06 · Search on a phone @mobile @pr", async ({ page, reference }) => {
    const quick = new QuickSearchPage(page);
    const search = new SearchPage(page);
    const width = page.viewportSize()?.width ?? 0;
    await page.goto(reference.url(chroniques.ilsa));

    // The quick search takes the screen.
    await page.getByRole("button", { name: t("shell.searchShort") }).tap();
    await expect(quick.field).toBeVisible();
    expect((await quick.dialog.boundingBox())?.width).toBe(width);
    await quick.field.fill("ilsa");
    await expect(quick.options.filter({ hasText: "Ilsa Varenne" }).first()).toBeVisible();

    // The search screen: the filters wait in their own panel.
    await quick.dialog.getByRole("button", { name: t("search.quick.advanced") }).tap();
    await expect(search.field).toBeVisible();
    await expect(search.filters).toBeHidden();
    await page.getByRole("button", { name: t("search.filters"), exact: true }).tap();
    await expect(search.filters).toBeVisible();
    await search.chooseCategory("PNJ");
    const count = String.raw`\d+`;
    const show = new RegExp(
      `^(${t("search.showResults_one", { count })}|${t("search.showResults_other", { count })})$`,
    );
    await page.getByRole("button", { name: show }).tap();

    await expect(search.filters).toBeHidden();
    await expect(page.getByRole("button", { name: t("search.filtersActive") })).toBeVisible();
    await expect(search.result("Ilsa Varenne")).toBeVisible();
    // Only PNJ (Ilsa Varenne, Capitaine Orsen) are left.
    for (const title of await search.titles.allTextContents()) {
      expect(["Ilsa Varenne", "Capitaine Orsen"]).toContain(title);
    }
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

  test("E2E-18-07 · Menu of an element without a right click @mobile", async ({
    page,
    reference,
  }) => {
    const tree = new TreePanel(page);
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await page.getByRole("button", { name: t("shell.showExplorer") }).tap();
    await tree.item("Lieux").tap();

    await tree.tree
      .getByRole("button", { name: t("explorer.rowActions", { name: "Hautbois" }) })
      .tap();
    await page.getByRole("menuitem", { name: t("explorer.rename") }).tap();
    const name = tree.tree.getByRole("textbox", { name: t("explorer.renameLabel") });
    await name.fill("Hautbois-sur-Givre");
    await name.press("Enter");

    await expect(tree.item("Hautbois-sur-Givre")).toBeVisible();
    await page.keyboard.press("Escape");
    await expect(view.link("Hautbois-sur-Givre")).toBeVisible();
  });

  test("E2E-18-08 · Move an element without dragging @mobile @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PageViewPage(page);
    const actions = (name: string) =>
      // The first in tree order (« Personnages » is also a folder of Propriétés).
      tree.tree.getByRole("button", { name: t("explorer.rowActions", { name }) }).first();
    const moveDialog = (name: string) =>
      page.getByRole("dialog", { name: t("explorer.moveDialog.title", { name }) });
    const destinations = (dialog: ReturnType<typeof moveDialog>) =>
      dialog.getByRole("list", { name: t("explorer.moveDialog.destinations") }).getByRole("button");
    await page.goto(reference.url(chroniques.ilsa));
    await page.getByRole("button", { name: t("shell.showExplorer") }).tap();

    await actions("Capitaine Orsen").tap();
    await page.getByRole("menuitem", { name: t("explorer.moveTo") }).tap();
    const orsen = moveDialog("Capitaine Orsen");
    await destinations(orsen)
      .filter({ hasText: /^Lieux/ })
      .tap();
    await expect(orsen).toBeHidden();

    // Filed under Lieux, and still referenced by Ilsa Varenne.
    await tree.item("Capitaine Orsen").tap();
    await expect(view.breadcrumb).toContainText("Lieux");
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.link("Capitaine Orsen")).toBeVisible();

    // A folder is offered neither itself, nor what is below it, nor anything outside Pages.
    await page.getByRole("button", { name: t("shell.showExplorer") }).tap();
    await actions("Personnages").tap();
    await page.getByRole("menuitem", { name: t("explorer.moveTo") }).tap();
    const offered = await destinations(moveDialog("Personnages")).allInnerTexts();
    // The name is the first line of each destination, its path the second.
    const names = offered.map((text) => text.split(/\r?\n/)[0]?.trim());
    expect(names).toContain("Lieux");
    for (const refused of ["Personnages", "PNJ", "Catégories", "Pages"]) {
      expect(names).not.toContain(refused);
    }
  });

  test("E2E-18-10 · Touch targets @mobile", async ({ page, reference }) => {
    await page.goto("/");
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await expectTouchTargets(page, "home");

    await page.goto(reference.url(chroniques.ilsa));
    await expect(new PageViewPage(page).title).toBeVisible();
    await expectTouchTargets(page, "page");

    await page.getByRole("button", { name: t("shell.showExplorer") }).tap();
    await expect(new TreePanel(page).tree).toBeVisible();
    await expectTouchTargets(page, "explorer");
    await page.keyboard.press("Escape");

    await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);
    await expect(page.getByRole("textbox", { name: t("editor.content") })).toBeVisible();
    await expectTouchTargets(page, "editor");
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
