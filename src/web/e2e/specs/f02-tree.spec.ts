import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

const pagesRoot = "0000000000000001";

test.describe("F02 · Explorer", () => {
  test("E2E-02-01 · Create a folder and a page @pr @desktop", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(pagesRoot));

    await tree.newFolder("Objets");
    await expect(tree.item("Objets")).toBeVisible();
    await tree.newPage("Lame de givre");

    await expect(tree.item("Lame de givre")).toBeVisible();
    expect(await tree.childrenOf("Objets")).toEqual(["Lame de givre"]);
    await expect(page).toHaveURL(/\/e\/[0-9A-F]{16}\?edit=true$/);
  });

  test("E2E-02-02 · Rename a referenced page @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.orsen));

    await tree.rename("Capitaine Orsen", "Commandant Orsen");
    await expect(tree.item("Commandant Orsen")).toBeVisible();
    await page.goto(reference.url(chroniques.ilsa));

    await expect(
      page.getByRole("article").getByRole("link", { name: "Commandant Orsen" }),
    ).toBeVisible();
    const content = (await (
      await page.request.get(`/api/v1/projects/${reference.id}/pages/${chroniques.ilsa}/content`)
    ).json()) as { markdown: string };
    expect(content.markdown).toContain(`[[${chroniques.orsen}]]`);
  });

  test("E2E-02-03 · Move by drag and drop @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.hautbois));
    await expect(tree.item("Hautbois")).toBeVisible();

    await tree.item("Hautbois").dragTo(tree.item("Factions"));

    await tree.expand("Factions");
    expect(await tree.childrenOf("Factions")).toContain("Hautbois");
    await expect(page.getByRole("navigation", { name: t("shell.breadcrumb") })).toContainText(
      "Factions",
    );
    const resolved = (await (
      await page.request.get(`/api/v1/projects/${reference.id}/resolve?ids=${chroniques.hautbois}`)
    ).json()) as { items: { state: string; path: { name: string }[] }[] };
    expect(resolved.items[0]?.state).toBe("existing");
    expect(resolved.items[0]?.path.map((p) => p.name)).toEqual(["Pages", "Factions"]);
  });

  test("E2E-02-04 · Move to another root is refused @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(pagesRoot));
    const before = await tree.childrenOf("Catégories");

    await tree.item("PNJ", "last").dragTo(tree.root("Pages"));

    expect(await tree.childrenOf("Catégories")).toEqual(before);
    expect(await tree.childrenOf("Pages")).not.toContain("PNJ");
  });

  test("E2E-02-05 · A page cannot become its own descendant @nightly", async ({
    page,
    reference,
  }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.ilsa));
    await tree.expand("Ilsa Varenne");

    await tree.item("Ilsa Varenne").dragTo(tree.item("Dialogues"));

    expect(await tree.childrenOf("PNJ")).toContain("Ilsa Varenne");
    expect(await tree.childrenOf("Ilsa Varenne")).toEqual(["Dialogues", "Inventaire"]);
  });

  test("E2E-02-06 · Reorder siblings @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.hautbois));
    expect(await tree.childrenOf("Lieux")).toEqual(["Hautbois", "Col gelé"]);

    await tree.item("Col gelé").dragTo(tree.item("Hautbois"), { targetPosition: { x: 20, y: 2 } });
    await expect.poll(() => tree.childrenOf("Lieux")).toEqual(["Col gelé", "Hautbois"]);

    await page.reload();
    await expect.poll(() => tree.childrenOf("Lieux")).toEqual(["Col gelé", "Hautbois"]);
  });

  test("E2E-02-07 · Remember the expanded state @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(pagesRoot));
    await tree.reveal("Personnages", "PNJ");
    await tree.collapse("Lieux");

    await page.reload();

    await expect(tree.item("PNJ")).toHaveAttribute("aria-expanded", "true");
    await expect(tree.item("Personnages")).toHaveAttribute("aria-expanded", "true");
    await expect(tree.item("Lieux")).toHaveAttribute("aria-expanded", "false");
  });

  test("E2E-02-08 · Root counters @nightly", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    await page.goto(reference.url(pagesRoot));
    await expect(tree.root("Pages")).toHaveText(/9$/);
    await expect(tree.root("Catégories")).toHaveText(/11$/);

    await tree.newPage("Lanterne d'ambre");

    await expect(tree.root("Pages")).toHaveText(/10$/);
  });
});
