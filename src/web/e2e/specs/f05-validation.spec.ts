import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { PagePropertiesPanel } from "../pages/PagePropertiesPanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t } from "../support/i18n";

test.describe("F05 · Live validation", () => {
  test("E2E-05-12 · Non-blocking live validation @pr", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);

    const niveau = panel.field("Niveau");
    await expect(niveau).toContainText(t("property.violation.less_or_equal", { value: 60 }));
    await expect(page.getByText(t("shell.status.errors_one", { count: 1 }))).toBeVisible();

    await expectNoSeriousViolations(page);
    await panel.save();
    await expect(panel.outOfRulesBadge).toBeVisible();
    await expectNoSeriousViolations(page);

    await page.getByRole("button", { name: t("page.edit") }).click();
    await niveau.getByRole("textbox", { name: "Niveau" }).fill("55");
    await expect(niveau).not.toContainText(t("property.violation.less_or_equal", { value: 60 }));
    await expect(page.getByText(t("shell.status.errors_one", { count: 1 }))).toHaveCount(0);
  });
});
