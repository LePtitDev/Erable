import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { PagePropertiesPanel } from "../pages/PagePropertiesPanel";
import { PropertyViewPage } from "../pages/PropertyViewPage";
import { TreePanel } from "../pages/TreePanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t } from "../support/i18n";

/** Opens a property of the reference project from the explorer (« Propriétés › Personnages › Rôle »). */
async function openProperty(tree: TreePanel, name: string) {
  for (const line of [tree.root("Propriétés"), tree.item("Personnages", "last")]) {
    if ((await line.getAttribute("aria-expanded")) === "false") {
      await line.click();
      await tree.tree.press("ArrowRight");
    }
  }
  await tree.item(name).click();
}

test.describe("F08 · Properties and validation", () => {
  test("E2E-08-01 · View a definition @pr", async ({ page, reference }) => {
    const view = new PropertyViewPage(page);

    await page.goto(reference.url(chroniques.niveau));

    await expect(view.title).toHaveText("Niveau");
    await expect(view.article.getByText("int", { exact: true })).toBeVisible();
    await expect(view.rules.getByRole("listitem")).toHaveText(["≥ 1", "≤ 60"]);
    await expect(view.usedBy.getByRole("link")).toContainText(["Créature", "Joueur", "PNJ"]);
    await expect(view.filled(3)).toBeVisible();
    await expect(view.outOfRulesTitle(2)).toBeVisible();
    await expect(
      view.outOfRules.getByRole("listitem").filter({ hasText: "Ilsa Varenne" }),
    ).toContainText("(72)");
    await expect(
      view.outOfRules.getByRole("listitem").filter({ hasText: "Dragon des Cimes" }),
    ).toContainText("(99)");
    await expectNoSeriousViolations(page);
  });

  test("E2E-08-02 · Create a typed property @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));

    await tree.newProperty("Poids");
    await expect(view.name).toHaveValue("Poids");
    await view.chooseType("float");
    await view.addRule("greater_than");
    await view
      .rule("greater_than")
      .getByRole("textbox", { name: t("property.ruleValue") })
      .fill("0");
    await view.save();

    await expect(view.title).toHaveText("Poids");
    await expect(view.article.getByText("float", { exact: true })).toBeVisible();
    await expect(view.rules.getByRole("listitem")).toHaveText(["> 0"]);

    // Offered by « Ajouter une propriété » on a page, and validated there.
    const panel = new PagePropertiesPanel(page);
    await page.goto(`${reference.url(chroniques.orsen)}?edit=true`);
    await panel.addPropertyButton.click();
    const dialog = page.getByRole("dialog", { name: t("category.addPropertyTitle") });
    await dialog
      .getByRole("combobox", { name: t("category.column.property") })
      .selectOption({ label: "Poids" });
    await dialog
      .getByRole("textbox", { name: t("property.valueOf", { name: "Poids" }) })
      .fill("-2");
    await dialog.getByRole("button", { name: t("category.addProperty") }).click();
    await expect(panel.field("Poids")).toContainText(
      t("property.violation.greater_than", { value: 0 }),
    );
  });

  test("E2E-08-03 · Rules offered by type @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));
    await tree.newProperty("Taille");

    await view.chooseType("string");
    const add = view.article.getByRole("group", { name: t("property.addRule") });
    await expect(add.getByRole("button")).toHaveText([
      t("property.op.min_length"),
      t("property.op.max_length"),
      t("property.op.match"),
      t("property.op.not_empty"),
    ]);

    await view.chooseType("bool");
    await expect(view.noBooleanRule).toBeVisible();
    await expect(add).toHaveCount(0);
  });

  test("E2E-08-04 · Test a value @pr", async ({ page, reference }) => {
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));

    await view.tester.fill("72");
    await expect(view.testResult).toHaveText(
      t("property.test.fails", { rules: "less_or_equal 60" }),
    );

    await view.tester.fill("30");
    await expect(view.testResult).toHaveText(t("property.test.valid"));
  });

  test("E2E-08-05 · Changing a rule recomputes the violations @pr", async ({ page, reference }) => {
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));

    await view.edit();
    await view
      .rule("less_or_equal")
      .getByRole("textbox", { name: t("property.ruleValue") })
      .fill("80");
    await view.save();

    await expect(view.rules.getByRole("listitem")).toHaveText(["≥ 1", "≤ 80"]);
    await expect(view.outOfRulesTitle(1)).toBeVisible();
    await expect(view.outOfRules.getByRole("listitem")).toHaveCount(1);
    await expect(view.outOfRules).toContainText("Dragon des Cimes");
    await expect(view.outOfRules).not.toContainText("Ilsa Varenne");

    const panel = new PagePropertiesPanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(panel.row("Niveau")).toContainText("72");
    await expect(panel.outOfRulesBadge).toHaveCount(0);
    await page.goto(reference.url(chroniques.dragon));
    await expect(panel.outOfRulesBadge).toBeVisible();
  });

  test("E2E-08-06 · Entry by type on a page @pr", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    await page.goto(`${reference.url(chroniques.orsen)}?edit=true`);

    await expect(panel.field("Hostile").getByRole("switch", { name: "Hostile" })).toBeVisible();
    await expect(panel.field("Rôle").getByRole("combobox", { name: "Rôle" })).toBeVisible();
    const region = panel.field("Région").getByRole("combobox", { name: "Région" });
    await expect(region.getByRole("option")).toHaveText(["—", "Col gelé", "Hautbois"]);

    await panel.addPropertyButton.click();
    const dialog = page.getByRole("dialog", { name: t("category.addPropertyTitle") });
    await dialog
      .getByRole("combobox", { name: t("category.column.property") })
      .selectOption({ label: "Âge" });
    const age = dialog.getByRole("textbox", { name: t("property.valueOf", { name: "Âge" }) });
    await age.clear();
    await age.pressSequentially("ab12");
    await expect(age).toHaveValue("12");
  });

  test("E2E-08-07 · The match rule on a string @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));
    await tree.newProperty("Code");

    await view.addRule("match");
    await view.article
      .getByRole("textbox", { name: t("property.rulePattern") })
      .fill("^[A-Z]{3}-\\d{2}$");
    await view.save();

    await view.tester.fill("ab-1");
    await expect(view.testResult).toContainText(t("property.test.fails", { rules: "" }).trim());
    await view.tester.fill("ABC-12");
    await expect(view.testResult).toHaveText(t("property.test.valid"));
  });

  test("E2E-08-08 · Rename an enum option @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));
    await openProperty(tree, "Rôle");

    await view.edit();
    await view.option("Marchande").fill("Négociante");
    await view.save();

    await expect(view.article.getByRole("list", { name: t("property.options") })).toContainText(
      "Négociante",
    );
    await expect(view.article.getByRole("list", { name: t("property.options") })).not.toContainText(
      "Marchande",
    );

    // Pages keep their value: it is stored by option key.
    const panel = new PagePropertiesPanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(panel.row("Rôle")).toContainText("Négociante");
  });

  test("E2E-08-09 · Remove a used enum option @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));
    await openProperty(tree, "Rôle");

    await view.edit();
    await view.removeOption("Garde");
    await view.article.getByRole("button", { name: t("editor.save") }).click();

    const dialog = page.getByRole("alertdialog", { name: t("property.replace.title") });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText("Capitaine Orsen");
    await dialog.getByRole("combobox").selectOption({ label: "Érudit" });
    await dialog.getByRole("button", { name: t("property.replace.confirm") }).click();

    await expect(view.article.getByRole("button", { name: t("page.edit") })).toBeVisible();
    await expect(view.article.getByRole("list", { name: t("property.options") })).not.toContainText(
      "Garde",
    );
  });

  test("E2E-08-10 · Incompatible type change @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));
    await openProperty(tree, "Rôle");

    await view.edit();
    await view.chooseType("int");
    await view.article.getByRole("button", { name: t("editor.save") }).click();

    const dialog = page.getByRole("alertdialog", { name: t("property.blocked.title") });
    await expect(dialog).toBeVisible();
    await expect(dialog).toContainText("Ilsa Varenne");
    await expect(dialog).toContainText("Capitaine Orsen");
    await dialog.getByRole("button", { name: t("common.close") }).click();
    await view.article.getByRole("button", { name: t("common.cancel") }).click();
    await expect(view.article.getByText("enum", { exact: true })).toBeVisible();
  });

  test("E2E-08-11 · Rename the key of a property @pr", async ({ page, reference }) => {
    const view = new PropertyViewPage(page);
    await page.goto(reference.url(chroniques.niveau));

    await view.edit();
    await view.key.fill("level");
    await view.save();

    await expect(view.article.getByText("level", { exact: true })).toBeVisible();
    // Everything refers to the property by ID: usage and violations are unchanged.
    await expect(view.usedBy.getByRole("link")).toContainText(["Créature", "Joueur", "PNJ"]);
    await expect(view.outOfRulesTitle(2)).toBeVisible();
    await page.goto(reference.url(chroniques.pnj));
    await expect(page.getByRole("row", { name: /^Niveau\b/ })).toContainText("≥ 1, ≤ 60");

    const panel = new PagePropertiesPanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(panel.row("Niveau")).toContainText("72");
  });
});
