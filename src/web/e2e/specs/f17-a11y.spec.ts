import { expect, test } from "../fixtures";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

test.describe("F17 · Accessibility and keyboard", () => {
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
