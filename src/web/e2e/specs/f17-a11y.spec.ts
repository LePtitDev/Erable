import { expect, test } from "../fixtures";
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
});
