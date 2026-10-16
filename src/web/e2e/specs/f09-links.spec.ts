import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { LinksPanel } from "../pages/LinksPanel";
import { TreePanel } from "../pages/TreePanel";
import { expectNoSeriousViolations } from "../support/a11y";
import { t } from "../support/i18n";

test.describe("F09 · Typed links", () => {
  test("E2E-09-01 · Create a link type @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.ilsa));

    await tree.newLinkType("Rival de");
    await page.getByRole("textbox", { name: t("links.inverse") }).fill("Rival de");
    await page.getByRole("button", { name: t("editor.save") }).click();

    await expect(page.getByRole("heading", { level: 1, name: "Rival de" })).toBeVisible();
    await expect(page.getByRole("article")).toContainText("Rival de");
    await expect(tree.root("Liens")).toBeVisible();
  });

  test("E2E-09-02 · Link two pages @pr", async ({ page, reference }) => {
    const links = new LinksPanel(page);
    await page.goto(reference.url(chroniques.ilsa));

    await links.link("Mentor de", "Capitaine Orsen");

    await expect(links.outgoing).toContainText("Mentor de · Capitaine Orsen");
    await expectNoSeriousViolations(page);
    await page.goto(reference.url(chroniques.orsen));
    await expect(links.incoming).toContainText("Élève de · Ilsa Varenne");
  });

  test("E2E-09-03 · Incoming links @pr", async ({ page, reference }) => {
    const links = new LinksPanel(page);
    await page.goto(reference.url(chroniques.hautbois));

    await expect(links.incoming).toContainText("Lieu de · Ilsa Varenne");
    // The pages that mention Hautbois in their text are listed apart.
    await expect(links.mentions.getByRole("link", { name: "Ilsa Varenne" })).toBeVisible();
    await expect(links.incoming.getByRole("link", { name: "Ilsa Varenne" })).toHaveCount(1);
  });

  test("E2E-09-04 · Delete a link @nightly", async ({ page, reference }) => {
    const links = new LinksPanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(links.outgoing).toContainText("Allié de · Capitaine Orsen");

    await links.remove("Allié de", "Capitaine Orsen");

    await expect(links.outgoing).not.toContainText("Allié de");
    await page.goto(reference.url(chroniques.orsen));
    await expect(links.panel).not.toContainText("Allié de");
  });
});
