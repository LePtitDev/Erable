import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { RecentPanel } from "../pages/RecentPanel";
import { t } from "../support/i18n";

test.describe("F11 · Recently viewed and edited", () => {
  test("E2E-11-01 · Recently viewed @pr", async ({ page, reference }) => {
    const recent = new RecentPanel(page);

    for (const id of [chroniques.pnj, chroniques.hautbois, chroniques.niveau]) {
      await page.goto(reference.url(id));
      await expect(page.getByRole("main")).toBeVisible();
      await recent.dwell();
    }

    await expect(recent.entries).toHaveCount(3);
    await expect(recent.entries.nth(0)).toContainText("Niveau");
    await expect(recent.entries.nth(0)).toHaveAttribute("title", t("kinds.propertyDefinition"));
    await expect(recent.entries.nth(1)).toContainText("Hautbois");
    await expect(recent.entries.nth(1)).toHaveAttribute("title", t("kinds.page"));
    await expect(recent.entries.nth(2)).toContainText("PNJ");
    await expect(recent.entries.nth(2)).toHaveAttribute("title", t("kinds.category"));
  });

  test("E2E-11-01 · An element left before two seconds is not listed @pr", async ({
    page,
    reference,
  }) => {
    const recent = new RecentPanel(page);
    await page.goto(reference.url(chroniques.hautbois));
    await expect(page.getByRole("main")).toBeVisible();
    await page.clock.runFor(1000);

    await page.goto(reference.url(chroniques.pnj));
    await expect(page.getByRole("main")).toBeVisible();
    await recent.dwell();

    await expect(recent.entries).toHaveCount(1);
    await expect(recent.entries.first()).toContainText("PNJ");
  });
});
