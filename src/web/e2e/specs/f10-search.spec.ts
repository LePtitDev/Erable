import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage } from "../pages/EditorPage";
import { QuickSearchPage } from "../pages/QuickSearchPage";
import { SearchPage } from "../pages/SearchPage";
import { TreePanel } from "../pages/TreePanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t } from "../support/i18n";

test.describe("F10 · Search and filters", () => {
  test("E2E-10-01 · Quick search @pr @desktop", async ({ page, reference }) => {
    const quick = new QuickSearchPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await expect(page.getByRole("main")).toBeVisible();

    await quick.open();
    await quick.field.fill("ilsa");

    await expect(quick.options.first()).toContainText("Ilsa Varenne");
    await expect(quick.options.first()).toHaveAttribute("aria-selected", "true");
    await page.keyboard.press("Enter");

    await expect(quick.dialog).toBeHidden();
    await expect(page).toHaveURL(reference.url(chroniques.ilsa));
    await expect(page.getByRole("heading", { level: 1, name: "Ilsa Varenne" })).toBeVisible();
  });

  test("E2E-10-01 · Quick search offers the recent elements and the advanced search @pr", async ({
    page,
    reference,
  }) => {
    const quick = new QuickSearchPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    await page.clock.runFor(2500);
    await page.goto(reference.url(chroniques.pnj));
    await expect(page.getByRole("main")).toBeVisible();

    await quick.open();
    await expect(quick.options.first()).toContainText("Hautbois");
    await quick.field.fill("col gele");
    await page.keyboard.press("Control+Enter");

    await expect(quick.dialog).toBeHidden();
    await expect(page).toHaveURL(/\/search\?q=col(%20|\+)gele/);
    await expect(new SearchPage(page).field).toHaveValue("col gele");
  });

  test("E2E-10-02 · Full text ignores accents @pr", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id);

    await search.search("col gele");

    await expect(search.result("Le Col gelé")).toBeVisible();
    await expect(search.result("Col gelé")).toBeVisible();
    await expect(search.result("Ilsa Varenne")).toBeVisible();
    // The passages that contain « Col gelé » are marked.
    await expect(search.result("Ilsa Varenne").locator("mark").first()).toHaveText("Col gelé");
    await expect(search.page).toHaveURL(/q=col(%20|\+)gele/);
    await expectNoSeriousViolations(page);
  });

  test("E2E-10-03 · Filter by type and category @pr", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?sort=name");
    for (const kind of [
      t("search.kinds.folder"),
      t("search.kinds.category"),
      t("search.kinds.propertyDefinition"),
      t("search.kinds.linkType"),
      t("search.kinds.attachment"),
    ]) {
      await search.kind(kind).uncheck();
    }

    await search.chooseCategory("PNJ");
    await search.includeSubCategories.check();

    await expect(search.titles).toHaveText(["Capitaine Orsen", "Ilsa Varenne"]);
  });

  test("E2E-10-04 · Condition on an inherited property @pr", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?sort=name");

    await search.addCondition("Hostile", t("search.ops.eq"), t("property.no"));

    await expect(search.titles).toHaveText(["Capitaine Orsen", "Ilsa Varenne"]);
  });

  test("E2E-10-05 · Numeric condition @pr", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?sort=name");

    await search.addCondition("Niveau", t("search.ops.gt"), "50");

    await expect(search.titles).toHaveText(["Dragon des Cimes", "Ilsa Varenne"]);
    await expect(search.result("Capitaine Orsen")).toHaveCount(0);
  });

  test("E2E-10-06 · Out of rules @nightly", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?sort=name");

    await search.outOfRules.check();

    await expect(search.titles).toHaveText(["Dragon des Cimes", "Ilsa Varenne"]);
    await expect(search.result("Ilsa Varenne")).toContainText(
      "Niveau : Doit être inférieur ou égal à 60",
    );
  });

  test("E2E-10-07 · Date filter @nightly", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?kind=%5B%22page%22%5D");

    await search.period(t("search.modified.day")).click();

    // The clock is 13 October 2026 10:00: since 12 October 10:00 Ilsa, Hautbois and the quest were changed, newest first.
    await expect(search.titles).toHaveText(["Ilsa Varenne", "Hautbois", "Le Col gelé"]);
  });

  test("E2E-10-08 · Sorting and a shareable URL @nightly", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?kind=%5B%22page%22%5D&q=col");
    await search.sort.selectOption("name");
    await expect(search.titles.first()).toHaveText("Capitaine Orsen");
    const names = await search.titles.allTextContents();
    expect(names.length).toBeGreaterThan(2);
    expect(names).toEqual([...names].sort((a, b) => a.localeCompare(b, "fr")));

    const copy = await page.context().newPage();
    await copy.goto(page.url());

    const same = new SearchPage(copy);
    await expect(same.field).toHaveValue("col");
    await expect(same.sort).toHaveValue("name");
    await expect(same.titles).toHaveText(names);
  });

  test("E2E-10-09 · No result @nightly", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?kind=%5B%22page%22%5D");

    await search.search("zzzz");

    await expect(page.getByText(t("search.none.title"))).toBeVisible();
    await page.getByRole("button", { name: t("search.none.reset") }).click();
    await expect(page.getByText(t("search.none.title"))).toBeVisible();
    await expect(search.reset).toBeDisabled();
  });

  test("E2E-10-10 · Reset the filters @nightly", async ({ page, reference }) => {
    const search = new SearchPage(page);
    await search.goto(reference.id, "?q=ilsa&oor=true&mod=week");
    await expect(search.outOfRules).toBeChecked();

    await search.reset.click();

    await expect(search.outOfRules).not.toBeChecked();
    await expect(search.period(t("search.modified.all"))).toHaveAttribute("aria-pressed", "true");
    await expect(search.field).toHaveValue("ilsa");
    await expect(search.result("Ilsa Varenne")).toBeVisible();
  });

  test("E2E-10-11 · A new element is found at once @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const search = new SearchPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await tree.newPage("Lanterne d’ambre");

    await page.goto(`/p/${reference.id}/search?q=ambre`);

    await expect(search.result("Lanterne d’ambre")).toBeVisible();
  });

  test("E2E-11-02 · Recently edited @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const search = new SearchPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await editor.toEnd();
    await editor.type("\n\nUn nouveau puits.");
    await editor.save();
    await expect(page.getByRole("button", { name: t("page.edit") })).toBeVisible();

    await search.goto(reference.id);
    await search.activity.getByRole("tab", { name: t("recent.edited") }).click();

    const first = search.activity.getByRole("link").first();
    await expect(first).toContainText("Hautbois");
    await expect(first).toContainText(t("element.me"));
    await expect(first).toContainText("à l’instant");
  });
});
