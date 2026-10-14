import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { PageViewPage } from "../pages/PageViewPage";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

test.describe("F04 · Reading a page", () => {
  test("E2E-04-01 · Full rendering of a page @pr @desktop", async ({ page, reference }) => {
    const view = new PageViewPage(page);

    await page.goto(reference.url(chroniques.ilsa));

    await expect(view.title).toHaveText("Ilsa Varenne");
    await expect(view.breadcrumb).toHaveText(
      /Pages\s*\/\s*Personnages\s*\/\s*PNJ\s*\/\s*Ilsa Varenne/,
    );
    // Category chips arrive with M2-06 (no category data in M1).
    await expect(view.article).toContainText("Modifiée il y a 2 h par Camille R.");
    for (const name of ["Hautbois", "Ordre du Givre", "Capitaine Orsen"]) {
      await expect(view.link(name)).toBeVisible();
    }
    await expect(view.article.getByRole("img", { name: "Portrait par Malo L." })).toBeVisible();
    const carte = view.article.getByRole("img", { name: "carte_hautbois.webp" });
    await expect(carte).toBeVisible();
    await expect(
      view.article.getByRole("figure", {
        name: new RegExp(`^${t("embed.attachmentOf")} Hautbois`),
      }),
    ).toBeVisible();
    // The images really load (not a broken icon).
    await expect
      .poll(() => carte.evaluate((img: HTMLImageElement) => img.naturalWidth))
      .toBeGreaterThan(0);
  });

  test("E2E-04-02 · Table of contents @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    const entry = view.toc.getByRole("link", { name: "Les années de cartographe" });
    await entry.click();

    await expect(page).toHaveURL(/#les-annees-de-cartographe$/);
    await expect(entry).toHaveAttribute("aria-current", "location");
    await expect(page.locator("#les-annees-de-cartographe")).toBeInViewport();
  });

  test("E2E-04-03 · Preview on hover of a reference @nightly", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    await view.link("Capitaine Orsen").hover();

    const card = page.locator("[data-radix-popper-content-wrapper]");
    await expect(card).toContainText("Capitaine Orsen");
    await expect(card).toContainText("7A1C…D615");
    await expect(card).toContainText("Pages / Personnages / PNJ");
    await expect(card).toContainText("Commandant de la garnison de Hautbois");
  });

  test("E2E-04-04 · Navigation by reference @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    const main = page.locator("#main");
    await expect(view.link("Le Col gelé")).toBeVisible();
    await view.link("Le Col gelé").scrollIntoViewIfNeeded();
    const scrolled = await main.evaluate((m) => m.scrollTop);

    await view.link("Le Col gelé").click();
    await expect(view.title).toHaveText("Le Col gelé");
    await page.goBack();

    await expect(view.title).toHaveText("Ilsa Varenne");
    await expect
      .poll(() => main.evaluate((m) => m.scrollTop))
      .toBeGreaterThan(Math.max(0, scrolled - 40));
    expect(scrolled).toBeGreaterThan(0);
  });

  test("E2E-04-06 · Sub-pages @pr", async ({ page, reference }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    const section = view.article.getByRole("region", { name: t("page.subPages") });

    await expect(section.getByRole("link")).toHaveText(["PDialogues", "PInventaire"]);
    await section.getByRole("button", { name: t("page.newSubPage") }).click();

    await expect(page).toHaveURL(/\?edit=true/);
    expect(await new TreePanel(page).childrenOf("Ilsa Varenne")).toContain(
      t("page.newSubPageName"),
    );
  });

  test("E2E-04-07 · Raw HTML is neutralised @pr", async ({ page, reference }) => {
    const markdown = `Avant <img src=x onerror="window.__xss=1"> après.`;
    const created = (await (
      await page.request.post(`/api/v1/projects/${reference.id}/pages`, {
        data: { parentId: "0000000000000001", name: "Piège", markdown },
      })
    ).json()) as { id: string };

    await page.goto(reference.url(created.id));

    const view = new PageViewPage(page);
    await expect(view.content).toContainText(`<img src=x onerror="window.__xss=1">`);
    expect(
      await page.evaluate(() => (window as unknown as { __xss?: number }).__xss),
    ).toBeUndefined();
    await expect(view.content.locator("img")).toHaveCount(0);
  });
});

test.describe("F03 · Stable identifiers", () => {
  test("E2E-03-01 · Show and copy the ID @pr", async ({ page, reference }) => {
    await PageViewPage.captureClipboard(page);
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    const id = view.idButton(chroniques.ilsa);
    await expect(id).toContainText("3F2A 9C14 E07B 5D21");
    await id.click();

    expect(await view.clipboard()).toBe("3F2A9C14E07B5D21");
    await expect(view.article.getByRole("status")).toHaveText(t("element.idCopied"));
  });

  test("E2E-03-02 · Permanent link @pr", async ({ page, reference }) => {
    const tree = new TreePanel(page);
    const saved = reference.url(chroniques.hautbois);
    await page.goto(saved);

    await tree.rename("Hautbois", "Hautbois-sur-Givre");
    await expect(tree.item("Hautbois-sur-Givre")).toBeVisible();
    await tree.item("Hautbois-sur-Givre").dragTo(tree.item("Factions"));
    await expect.poll(() => tree.childrenOf("Factions")).toContain("Hautbois-sur-Givre");
    await page.goto(saved);

    const view = new PageViewPage(page);
    await expect(view.title).toHaveText("Hautbois-sur-Givre");
    await expect(view.breadcrumb).toContainText("Factions");
  });
});

test.describe("perf · page opening", () => {
  test.use({ fakeClock: false });

  test("perf · Opening a page takes less than 200 ms (indicative, budget M3-06) @perf", async ({
    page,
    reference,
  }) => {
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await expect(view.link("Hautbois")).toBeVisible();
    await page.evaluate(() => {
      performance.clearMeasures("erable:page");
    });

    await view.link("Hautbois").click();
    await expect(view.title).toHaveText("Hautbois");

    const measure = async () =>
      page.evaluate(() => performance.getEntriesByName("erable:page").at(-1)?.duration ?? null);
    await expect.poll(measure).not.toBeNull();
    const duration = (await measure()) ?? Number.POSITIVE_INFINITY;
    test.info().annotations.push({ type: "page-open-ms", description: duration.toFixed(1) });
    expect(duration).toBeLessThan(200);
  });
});
