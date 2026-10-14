import { expect, test } from "../fixtures";
import { t } from "../support/i18n";

test.describe("F03 · Stable identifiers", () => {
  test("E2E-03-03 · Unknown ID in the URL @nightly", async ({ page, reference }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));

    await page.goto(reference.url("0123456789ABCDEF"));

    await expect(page.getByRole("heading", { level: 1, name: t("notFound.title") })).toBeVisible();
    await expect(page.getByText(t("shell.search"))).toBeVisible();
    await expect(page.getByRole("link", { name: t("notFound.projectHome") })).toBeVisible();
    await expect(page.locator("body")).not.toContainText("404");
    expect(errors).toEqual([]);
  });
});
