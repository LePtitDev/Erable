import { expect, test } from "../fixtures";
import { EditorPage, InsertPalette } from "../pages/EditorPage";
import { QuickSearchPage } from "../pages/QuickSearchPage";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

test.describe("F17 · Accessibility and keyboard", () => {
  test("E2E-17-02 · Write a page without the mouse @a11y", async ({ page, reference }) => {
    const quick = new QuickSearchPage(page);
    const editor = new EditorPage(page);
    const palette = new InsertPalette(page);
    await page.goto(`/p/${reference.id}`);

    // Ctrl+K, a name, Enter: the page opens.
    await quick.open();
    await page.keyboard.type("capitaine");
    await expect(quick.options.first()).toContainText("Capitaine Orsen");
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(reference.url("7A1C4E90B3F2D615"));

    // The edit button by keyboard, then a link with the palette, then Ctrl+S.
    const edit = page.getByRole("button", { name: t("page.edit") });
    await edit.focus();
    await expect(edit).toBeFocused();
    await page.keyboard.press("Enter");
    await expect(editor.content).toBeVisible();
    await editor.toEnd();
    await editor.type("\n\nAlliée : [[ilsa");
    await expect(palette.dialog).toBeVisible();
    await page.keyboard.press("Enter");
    await expect(palette.dialog).toBeHidden();
    await page.keyboard.press("Control+s");

    await expect(page).not.toHaveURL(/edit=true/);
    await expect(page.getByRole("link", { name: "Ilsa Varenne" }).first()).toBeVisible();
  });

  test("E2E-17-03 · Modal dialogs @a11y", async ({ home, page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    await home.goto();
    const opener = page.getByRole("button", { name: t("home.new.title") });
    await opener.focus();
    await page.keyboard.press("Enter");

    const dialog = page.getByRole("dialog", { name: t("home.new.dialogTitle") });
    await expect(dialog).toBeVisible();
    // Focus is trapped: however many Tabs, it stays inside the dialog.
    for (let i = 0; i < 8; i++) {
      await page.keyboard.press("Tab");
      await expect(dialog.locator(":focus")).toHaveCount(1);
    }
    await page.keyboard.press("Escape");

    await expect(dialog).toBeHidden();
    await expect(opener).toBeFocused();
  });

  test("E2E-17-01 · Keyboard navigation in the tree @a11y", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url("0000000000000001"));
    await expect(tree.item("Personnages")).toBeVisible();
    await tree.tree.focus();

    // ↓ to Personnages, → expands, → enters PNJ, → expands, ↓ Ilsa Varenne, Entrée opens it.
    await page.keyboard.press("ArrowDown");
    await page.keyboard.press("ArrowRight");
    await expect(tree.item("Personnages")).toHaveAttribute("aria-expanded", "true");
    await page.keyboard.press("ArrowRight");
    await page.keyboard.press("ArrowRight");
    await expect(tree.item("PNJ")).toHaveAttribute("aria-expanded", "true");
    await expect(tree.item("PNJ")).toHaveAttribute("aria-level", "3");
    await page.keyboard.press("ArrowDown");
    await page.keyboard.press("Enter");
    await expect(page).toHaveURL(reference.url("3F2A9C14E07B5D21"));

    // F2 renames without the mouse; ← collapses.
    await tree.tree.focus();
    await page.keyboard.press("F2");
    await page.keyboard.type("Ilsa V.");
    await page.keyboard.press("Enter");
    await expect(tree.item("Ilsa V.")).toBeVisible();
    await page.keyboard.press("ArrowUp");
    await page.keyboard.press("ArrowLeft");
    await expect(tree.item("PNJ")).toHaveAttribute("aria-expanded", "false");
  });
});
