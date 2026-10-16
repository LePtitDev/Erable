import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { PagePropertiesPanel } from "../pages/PagePropertiesPanel";
import { PageViewPage } from "../pages/PageViewPage";
import { TrashPage } from "../pages/TrashPage";
import { TreePanel } from "../pages/TreePanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t, tp } from "../support/i18n";

test.describe("F12 · Deletion and trash", () => {
  test("E2E-12-01 · Delete a referenced page @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    await page.goto(reference.url(chroniques.orsen));

    await tree.startDelete("Capitaine Orsen");

    await expect(trash.dialog).toContainText(
      tp("trash.referencedBy", 1, { names: "Ilsa Varenne" }),
    );
    await expectNoSeriousViolations(page);
    await trash.confirmDelete();
    await expect(
      tree.tree.getByRole("treeitem", { name: "Capitaine Orsen", exact: true }),
    ).toHaveCount(0);
    await trash.open();
    await expect(trash.entry("Capitaine Orsen")).toBeVisible();
  });

  test("E2E-12-02 · Restore from the trash @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.orsen));
    await tree.startDelete("Capitaine Orsen");
    await trash.confirmDelete();
    await trash.open();

    await trash.restore("Capitaine Orsen");

    await expect(trash.view.getByRole("status")).toContainText("PNJ");
    await page.goto(reference.url(chroniques.ilsa));
    await view.link("Capitaine Orsen").click();
    await expect(page).toHaveURL(new RegExp(chroniques.orsen));
    await expect(view.title).toHaveText("Capitaine Orsen");
    expect(await tree.childrenOf("PNJ")).toContain("Capitaine Orsen");
  });

  test("E2E-12-03 · Sub-pages follow the page @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    await tree.startDelete("Ilsa Varenne");

    await expect(trash.dialog).toContainText(tp("trash.cascade", 2, { name: "Ilsa Varenne" }));
    await trash.confirmDelete();
    await trash.open();
    await expect(trash.entry("Ilsa Varenne")).toContainText(tp("trash.withDescendants", 3));
    await trash.restore("Ilsa Varenne");
    await page.goto(reference.url(chroniques.ilsa));
    const subPages = page.getByRole("region", { name: t("page.subPages") });
    await expect(subPages.getByRole("link", { name: "Dialogues" })).toBeVisible();
    await expect(subPages.getByRole("link", { name: "Inventaire" })).toBeVisible();
  });

  test("E2E-12-04 · Delete a category @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    const panel = new PagePropertiesPanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(panel.categories.getByRole("link", { name: "Marchand" })).toBeVisible();

    await tree.startDelete("Marchand");
    await trash.confirmDelete();

    await page.goto(reference.url(chroniques.ilsa));
    await expect(page.getByRole("heading", { level: 1, name: "Ilsa Varenne" })).toBeVisible();
    await expect(panel.categories.getByRole("link", { name: "PNJ" })).toBeVisible();
    await expect(panel.categories.getByRole("link", { name: "Marchand" })).toHaveCount(0);
  });

  test("E2E-12-05 · Restore when the parent is gone @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await tree.startDelete("Hautbois");
    await trash.confirmDelete();
    await page.goto(reference.url(chroniques.ordreDuGivre));
    await tree.startDelete("Lieux");
    await trash.confirmDelete();
    await trash.open();
    await trash.purge("Lieux");

    await trash.restore("Hautbois");

    await expect(trash.view.getByRole("status")).toContainText(
      t("trash.relocated", { name: "Hautbois", parent: "Pages" }),
    );
    expect(await tree.childrenOf("Pages")).toContain("Hautbois");
  });

  test("E2E-12-06 · Empty the trash @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const trash = new TrashPage(page);
    await page.goto(reference.url(chroniques.orsen));
    await tree.startDelete("Capitaine Orsen");
    await trash.confirmDelete();
    await trash.open();

    await trash.view.getByRole("button", { name: t("trash.emptyAction") }).click();
    const confirm = trash.dialog.getByRole("button", { name: t("trash.emptyConfirm") });
    await expect(confirm).toBeDisabled();
    await trash.dialog.getByRole("textbox").fill(reference.name);
    await confirm.click();

    await expect(trash.view.getByText(t("trash.empty"))).toBeVisible();
    await page.goto(reference.url(chroniques.orsen));
    await expect(page.getByText(t("notFound.title"))).toBeVisible();
  });
});
