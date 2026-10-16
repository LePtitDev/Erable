import { expect, test } from "../fixtures";
import { resetProject } from "../fixtures";
import type { Page } from "@playwright/test";
import { EditorPage } from "../pages/EditorPage";
import { PageViewPage } from "../pages/PageViewPage";
import { TreePanel } from "../pages/TreePanel";

// Browser budgets on the large generated project (docs/testing.md §11.1, M3-06). Run them alone:
//   ERABLE_LARGE_PROJECT=<folder made by `erable.benchmarks generate --light`> pnpm e2e:perf
// They are skipped when the project is not there. The attachments of the light project are small on purpose.
const large = process.env["ERABLE_LARGE_PROJECT"];

test.describe("perf · large project", () => {
  test.skip(large === undefined, "ERABLE_LARGE_PROJECT is not set.");
  test.use({ fakeClock: false });

  async function openLarge(page: Page, slot: string) {
    const result = await resetProject(page.request, slot, "large");
    if (result.projectId === null) throw new Error("The large project did not open.");
    return result.projectId;
  }

  test("perf · Project → tree displayed in less than 1 s @perf", async ({ page, browserName }) => {
    const id = await openLarge(page, `${browserName}-large-tree`);
    const tree = new TreePanel(page);

    await page.goto(`/p/${id}`);
    await expect(tree.root("Pages")).toBeVisible();

    const shown = await page.evaluate(
      () => performance.getEntriesByName("erable:tree-shown")[0]?.startTime ?? null,
    );
    test.info().annotations.push({ type: "tree-shown-ms", description: String(shown) });
    expect(shown).not.toBeNull();
    expect(shown ?? Number.POSITIVE_INFINITY).toBeLessThan(1000);
  });

  test("perf · Opening a page of the large project in less than 200 ms @perf", async ({
    page,
    browserName,
  }) => {
    const id = await openLarge(page, `${browserName}-large-page`);
    const tree = (await (await page.request.get(`/api/v1/projects/${id}/tree`)).json()) as {
      nodes: { id: string; kind: string }[];
    };
    const first = tree.nodes.find((n) => n.kind === "page")?.id ?? "";
    await page.goto(`/p/${id}/e/${first}`);
    // A reference of the content (not the breadcrumb): a page of the same project.
    const link = new PageViewPage(page).content.getByRole("link").first();
    await expect(link).toBeVisible();
    await page.evaluate(() => {
      performance.clearMeasures("erable:page");
    });

    await link.click();

    const measure = async () =>
      page.evaluate(() => performance.getEntriesByName("erable:page").at(-1)?.duration ?? null);
    await expect.poll(measure).not.toBeNull();
    const duration = (await measure()) ?? Number.POSITIVE_INFINITY;
    test.info().annotations.push({ type: "page-open-ms", description: duration.toFixed(1) });
    expect(duration).toBeLessThan(200);
  });

  test("perf · Insert palette: key → proposals in less than 50 ms @perf", async ({
    page,
    browserName,
  }) => {
    const id = await openLarge(page, `${browserName}-large-palette`);
    const editor = new EditorPage(page);
    const tree = (await (await page.request.get(`/api/v1/projects/${id}/tree`)).json()) as {
      nodes: { id: string; kind: string }[];
    };
    const target = tree.nodes.find((n) => n.kind === "page")?.id ?? "";
    await page.goto(`/p/${id}/e/${target}?edit=true`);
    await expect(editor.content).toBeVisible();
    await editor.toEnd();
    await editor.type("\n\n[[page 0");
    await expect(page.getByRole("dialog", { name: "Insérer une référence" })).toBeVisible();

    // From the key press to the proposals of the new text. The Playwright round trips are inside the measure, so the
    // real figure is lower: if this passes, the budget is met.
    const before = await page.getByRole("listbox").textContent();
    const start = Date.now();
    await page.keyboard.type("1");
    await expect.poll(async () => page.getByRole("listbox").textContent()).not.toBe(before);
    const elapsed = Date.now() - start;

    test
      .info()
      .annotations.push({ type: "palette-ms-with-roundtrips", description: String(elapsed) });
    expect(elapsed).toBeLessThan(50);
  });
});
