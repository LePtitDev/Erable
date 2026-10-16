import type { APIRequestContext } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { CategoryViewPage } from "../pages/CategoryViewPage";
import { PagePropertiesPanel } from "../pages/PagePropertiesPanel";
import { t } from "../support/i18n";

const categoriesRoot = "0000000000000002";
const pagesRoot = "0000000000000001";

interface Created {
  readonly element: { readonly id: string };
}

/** Creates a category under the Catégories root, with default values by property ID. */
async function createCategory(
  request: APIRequestContext,
  projectId: string,
  name: string,
  values: Record<string, unknown>,
): Promise<string> {
  const base = `/api/v1/projects/${projectId}`;
  const created = await request.post(`${base}/categories`, {
    data: { parentId: categoriesRoot, name },
  });
  const id = ((await created.json()) as Created).element.id;
  for (const [property, value] of Object.entries(values)) {
    await request.put(`${base}/categories/${id}/values/${property}`, { data: { value } });
  }
  return id;
}

async function propertyId(request: APIRequestContext, projectId: string, name: string) {
  const list = await request.get(`/api/v1/projects/${projectId}/properties`);
  const found = ((await list.json()) as { id: string; name: string }[]).find(
    (p) => p.name === name,
  );
  if (found === undefined) throw new Error(`Unknown property ${name}`);
  return found.id;
}

test.describe("F07 · Categories on a page", () => {
  test("E2E-07-04 · Add a category to a page @pr", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    await page.goto(`${reference.url(chroniques.orsen)}?edit=true`);

    await panel.addCategory("Érudit");
    await expect(panel.categories.getByRole("link", { name: "Érudit" })).toBeVisible();
    await panel.save();
    for (const name of ["Garde", "PNJ", "Érudit"]) {
      await expect(panel.categories.getByRole("link", { name, exact: true })).toBeVisible();
    }
    await expect(panel.row("Hostile")).toContainText("PNJ");
  });

  test("E2E-07-05 · Inherited value then overridden @pr", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    await page.goto(`${reference.url(chroniques.orsen)}?edit=true`);
    const hostile = panel.field("Hostile");
    const toggle = hostile.getByRole("switch", { name: "Hostile" });

    await expect(toggle).not.toBeChecked();
    await expect(hostile).toContainText(t("page.inheritedFrom", { categories: "PNJ" }));

    await toggle.click();
    await expect(toggle).toBeChecked();
    await expect(hostile).not.toContainText("PNJ");

    await panel.restoreButton(hostile).click();
    await expect(toggle).not.toBeChecked();
    await expect(hostile).toContainText(t("page.inheritedFrom", { categories: "PNJ" }));
  });

  test("E2E-07-06 · Conflict between categories @pr", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    const hostileId = await propertyId(page.request, reference.id, "Hostile");
    await createCategory(page.request, reference.id, "Montagnard", { [hostileId]: true });
    await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);

    await panel.addCategory("Montagnard");
    await panel.openTab("properties");

    const hostile = panel.field("Hostile");
    await expect(hostile).toContainText(
      t("page.conflict", {
        candidates: `PNJ (${t("property.no")}) / Montagnard (${t("property.yes")})`,
      }),
    );
    await hostile.getByRole("button", { name: t("property.no"), exact: true }).click();
    await expect(hostile).not.toContainText(t("page.conflict", { candidates: "" }));
    await expect(hostile.getByRole("switch", { name: "Hostile" })).not.toBeChecked();
  });

  test("E2E-07-08 · Changing a default propagates @nightly", async ({ page, reference }) => {
    const panel = new PagePropertiesPanel(page);
    const category = new CategoryViewPage(page);
    // A PNJ page with no Niveau of its own.
    const created = await page.request.post(`/api/v1/projects/${reference.id}/pages`, {
      data: { parentId: pagesRoot, name: "Forgeron du village" },
    });
    const pageId = ((await created.json()) as { id: string }).id;
    const revision = await page.request.get(
      `/api/v1/projects/${reference.id}/pages/${pageId}/content`,
    );
    const { revision: current } = (await revision.json()) as { revision: number };
    await page.request.put(`/api/v1/projects/${reference.id}/pages/${pageId}/content`, {
      headers: { "If-Match": `"${String(current)}"` },
      data: { markdown: "", categories: [chroniques.pnj], properties: [] },
    });

    await page.goto(reference.url(chroniques.pnj));
    await category.edit();
    await category.valueOf("Niveau").fill("15");
    await category.save();

    await page.goto(reference.url(pageId));
    await expect(panel.row("Niveau")).toContainText("15");
    await page.goto(reference.url(chroniques.ilsa));
    await expect(panel.row("Niveau")).toContainText("72");
  });
});
