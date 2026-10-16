import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { SearchPage } from "../pages/SearchPage";
import { CategoryViewPage } from "../pages/CategoryViewPage";
import { TreePanel } from "../pages/TreePanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t } from "../support/i18n";

test.describe("F07 · Categories", () => {
  test("E2E-07-01 · View a category @pr", async ({ page, reference }) => {
    const view = new CategoryViewPage(page);

    await page.goto(reference.url(chroniques.pnj));

    await expect(view.title).toHaveText("PNJ");
    await expect(view.article).toContainText("Personnage non joueur");
    await expect(view.lineage).toHaveText(/Entité.*Personnage.*PNJ/);
    for (const name of ["Marchand", "Garde", "Érudit"]) {
      await expect(view.subCategory(name)).toBeVisible();
    }
    await expect(view.property("Hostile")).toContainText("PNJ");
    await expect(view.property("Niveau")).toContainText("≥ 1, ≤ 60");
    await expectNoSeriousViolations(page);
  });

  test("E2E-07-02 · Create a category with a parent @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new CategoryViewPage(page);
    await page.goto(reference.url(chroniques.pnj));

    await tree.newCategory("Forgeron");
    await expect(view.title).toHaveText("Forgeron");
    await view.parent.selectOption({ label: "PNJ" });
    await view.description.fill("Artisan du métal");
    await view.save();

    await expect(view.article).toContainText("Artisan du métal");
    await expect(view.lineage).toHaveText(/Entité.*Personnage.*PNJ.*Forgeron/);
    const hostile = view.property("Hostile");
    await expect(hostile).toContainText(t("property.no"));
    await expect(hostile).toContainText("PNJ");
  });

  test("E2E-07-03 · Cycle refused @pr", async ({ page, reference }) => {
    const view = new CategoryViewPage(page);
    await page.goto(reference.url(chroniques.personnage));

    await view.edit();
    await view.parent.selectOption({ label: "Marchand" });

    await expect(view.article.getByRole("alert")).toHaveText(
      t("category.loop", { loop: "Personnage › PNJ › Marchand › Personnage" }),
    );
    await expect(view.parent).toHaveValue(chroniques.entite);
  });

  test("E2E-07-07 · Pages of the category with sub-categories @pr", async ({ page, reference }) => {
    const view = new CategoryViewPage(page);
    await page.goto(reference.url(chroniques.pnj));

    await expect(view.includeSubCategories).toBeChecked();
    await expect(view.pages.getByRole("link", { name: /Ilsa Varenne\s*Marchand/ })).toBeVisible();
    await expect(view.pages.getByRole("link", { name: /Capitaine Orsen\s*Garde/ })).toBeVisible();

    await view.article
      .getByRole("link", { name: t("category.seeInSearch_other", { count: 2 }) })
      .click();

    const search = new SearchPage(page);
    await expect(page).toHaveURL(/\/search\?/);
    await expect(search.category("PNJ")).toHaveAttribute("aria-pressed", "true");
    await expect(search.includeSubCategories).toBeChecked();
    // Without text the newest comes first: Ilsa changed two hours ago, Orsen on 10 October.
    await expect(search.titles).toHaveText(["Ilsa Varenne", "Capitaine Orsen"]);
  });
});
