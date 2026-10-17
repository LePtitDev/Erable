import type { Page } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage, InsertPalette } from "../pages/EditorPage";
import { PreferencesDialog } from "../pages/PreferencesDialog";
import { PageViewPage } from "../pages/PageViewPage";
import { t } from "../support/i18n";

async function editAtEnd(page: Page, url: string) {
  const editor = new EditorPage(page);
  await page.goto(url);
  await editor.open();
  await editor.toEnd();
  await editor.type("\n\n");
  return editor;
}

async function markdownOf(page: Page, projectId: string, pageId: string) {
  const response = await page.request.get(`/api/v1/projects/${projectId}/pages/${pageId}/content`);
  return ((await response.json()) as { markdown: string }).markdown;
}

test.describe("F05 · Insert palette", () => {
  test("E2E-05-03 · Insert a link with the palette @pr", async ({ page, reference }) => {
    const editor = await editAtEnd(page, reference.url(chroniques.ilsa));
    const palette = new InsertPalette(page);

    await editor.type("[[orse");

    await expect(palette.dialog).toBeVisible();
    const first = palette.options.first();
    await expect(first).toContainText("Capitaine Orsen");
    await expect(first).toContainText("Pages / Personnages / PNJ");
    await expect(first).toContainText("7A1C…D615");
    await page.keyboard.press("Enter");

    await expect(palette.dialog).toBeHidden();
    await expect(editor.content.getByText("Capitaine Orsen", { exact: true })).toHaveCount(2);
    await editor.save();
    await expect(page).not.toHaveURL(/edit=true/);
    // Enter after a quote continues it (« > »): the reference ends the document either way.
    expect(await markdownOf(page, reference.id, chroniques.ilsa)).toMatch(
      /\[\[7A1C4E90B3F2D615\]\]$/,
    );
  });

  test("E2E-05-04 · Embed the attachment of another page @pr", async ({ page, reference }) => {
    const editor = await editAtEnd(page, reference.url(chroniques.orsen));
    const palette = new InsertPalette(page);

    await editor.type("![[carte");
    const option = palette.options.filter({ hasText: "Hautbois › carte_hautbois.webp" });
    await expect(option).toBeVisible();
    await expect(option).toHaveAttribute("aria-selected", "true");
    await page.keyboard.press("Shift+Enter");
    await editor.save();
    await expect(page).not.toHaveURL(/edit=true/);

    const markdown = await markdownOf(page, reference.id, chroniques.orsen);
    expect(markdown).toMatch(/!\[\[[0-9A-F]{16}\]\]$/);
    const view = new PageViewPage(page);
    const image = view.article.getByRole("img", { name: "carte_hautbois.webp" });
    await expect(image).toBeVisible();
    await expect
      .poll(() => image.evaluate((img: HTMLImageElement) => img.naturalWidth))
      .toBeGreaterThan(0);
  });

  test("E2E-05-05 · Filter the palette by type @pr", async ({ page, reference }) => {
    const editor = await editAtEnd(page, reference.url(chroniques.ilsa));
    const palette = new InsertPalette(page);

    await editor.type("[[");
    await expect(palette.tab(t("palette.tab.all"))).toHaveAttribute("aria-selected", "true");
    for (let i = 0; i < 3; i++) await page.keyboard.press("Tab");

    await expect(palette.tab(t("palette.tab.category"))).toHaveAttribute("aria-selected", "true");
    await expect(palette.options.first()).toBeVisible();
    for (const option of await palette.options.all()) {
      await expect(option.locator("[title]").first()).toHaveAttribute("title", t("kinds.category"));
    }
  });

  test("E2E-05-06 · Close the palette without inserting @nightly", async ({ page, reference }) => {
    const editor = await editAtEnd(page, reference.url(chroniques.colGeleLieu));
    const palette = new InsertPalette(page);

    await editor.type("[[");
    await expect(palette.dialog).toBeVisible();
    await page.keyboard.press("Escape");

    await expect(palette.dialog).toBeHidden();
    await editor.save();
    await expect(page).not.toHaveURL(/edit=true/);
    expect(await markdownOf(page, reference.id, chroniques.colGeleLieu)).toMatch(/\n\n\[\[$/);
  });
  test("E2E-05-07 · Configurable shortcut @nightly", async ({ page, reference }) => {
    const preferences = new PreferencesDialog(page);
    await page.goto(reference.url(chroniques.ilsa));
    await preferences.openFromProject();
    await preferences.assignShortcut("openPalette", "Control+Shift+L");
    await expect(preferences.shortcut("openPalette")).toContainText("Ctrl+Maj+L");
    await page.keyboard.press("Escape");

    try {
      const editor = new EditorPage(page);
      await editor.open();
      await editor.content.click();
      await page.keyboard.press("Control+Shift+L");

      await expect(new InsertPalette(page).dialog).toBeVisible();
    } finally {
      await page.keyboard.press("Escape");
      await page.request.put("/api/v1/me/preferences", {
        data: {
          ...(await (await page.request.get("/api/v1/me/preferences")).json()),
          shortcuts: {},
        },
      });
    }
  });
});
