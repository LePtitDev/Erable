import type { Page } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage } from "../pages/EditorPage";
import { PageViewPage } from "../pages/PageViewPage";
import { TreePanel } from "../pages/TreePanel";
import { t } from "../support/i18n";

async function markdownOf(page: Page, projectId: string, pageId: string) {
  const response = await page.request.get(`/api/v1/projects/${projectId}/pages/${pageId}/content`);
  return ((await response.json()) as { markdown: string }).markdown;
}

test.describe("F05 · Editing a page", () => {
  test("E2E-05-01 · Edit and save @pr @desktop", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const view = new PageViewPage(page);
    await page.goto(reference.url(chroniques.ilsa));

    await editor.open();
    await editor.toEnd();
    await editor.type("\n\nElle parle peu.");
    await editor.save();

    await expect(view.title).toHaveText("Ilsa Varenne");
    await expect(view.content).toContainText("Elle parle peu.");
    await expect(view.article).toContainText(
      t("element.modified", { when: "à l’instant", who: t("element.me") }),
    );
  });

  test("E2E-05-02 · Pills instead of IDs @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await editor.open();

    await expect(editor.pill("Hautbois")).toBeVisible();
    await expect(editor.pill("Capitaine Orsen")).toBeVisible();
    expect(await editor.shownText()).not.toMatch(/[0-9A-F]{16}/);

    // The cursor right after the pill: the 44 characters before it, then the pill, one atomic step.
    await editor.content.click();
    await page.keyboard.press("Control+Home");
    for (let i = 0; i < 45; i++) await page.keyboard.press("ArrowRight");
    await page.keyboard.press("Backspace");

    await expect(editor.pill("Hautbois")).toHaveCount(0);
    expect(await editor.shownText()).toContain(
      "tient le comptoir d’échange de , au pied du Col gelé.",
    );
  });

  test("E2E-05-08 · Show the raw IDs @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const before = await markdownOf(page, reference.id, chroniques.ilsa);
    await page.goto(reference.url(chroniques.ilsa));
    await editor.open();

    await editor.rawIds.check();
    await expect(editor.content).toContainText(`[[${chroniques.orsen}]]`);
    await expect(editor.content.locator(".cm-pill")).toHaveCount(0);
    await editor.rawIds.uncheck();
    await expect(editor.pill("Capitaine Orsen")).toBeVisible();
    await editor.content.click();
    await editor.save();

    await expect(page).not.toHaveURL(/edit=true/);
    expect(await markdownOf(page, reference.id, chroniques.ilsa)).toBe(before);
  });

  test("E2E-05-09 · Draft recovered @pr", async ({ page, context, reference }) => {
    const editor = new EditorPage(page);
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await editor.toEnd();
    await editor.type("\n\nUn brouillon précieux.");
    await expect(editor.draftSaved).toBeVisible();

    await page.close({ runBeforeUnload: false });
    const again = await context.newPage();
    await again.goto(`${reference.url(chroniques.hautbois)}?edit=true`);

    const banner = again
      .getByRole("alert")
      .filter({ hasText: t("editor.draftFound", { when: "" }).split("(")[0]?.trim() ?? "" });
    await expect(banner).toBeVisible();
    await banner.getByRole("button", { name: t("editor.restore") }).click();
    await expect(new EditorPage(again).content).toContainText("Un brouillon précieux.");
  });

  test("E2E-05-10 · Guard before leaving @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const tree = new TreePanel(page);
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await editor.toEnd();
    await editor.type(" Modifié.");

    await tree.item("Col gelé").click();

    const dialog = page.getByRole("alertdialog", { name: t("editor.leaveTitle") });
    await expect(dialog).toBeVisible();
    for (const label of [t("editor.save"), t("editor.discard"), t("common.cancel")]) {
      await expect(dialog.getByRole("button", { name: label })).toBeVisible();
    }
    await dialog.getByRole("button", { name: t("common.cancel") }).click();
    await expect(page).toHaveURL(new RegExp(`${chroniques.hautbois}\\?edit=true`));
    await expect(editor.content).toContainText("Modifié.");
  });

  test("E2E-05-11 · Cancel the edit @pr", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    const view = new PageViewPage(page);
    const before = await markdownOf(page, reference.id, chroniques.hautbois);
    await page.goto(reference.url(chroniques.hautbois));
    await editor.open();
    await editor.toEnd();
    await editor.type(" À oublier.");
    await expect(editor.draftSaved).toBeVisible();

    await page.getByRole("button", { name: t("common.cancel"), exact: true }).click();
    await page
      .getByRole("alertdialog")
      .getByRole("button", { name: t("editor.discard") })
      .click();

    await expect(view.title).toHaveText("Hautbois");
    await expect(view.content).not.toContainText("À oublier.");
    expect(await markdownOf(page, reference.id, chroniques.hautbois)).toBe(before);
    const draft = await page.request.get(
      `/api/v1/me/ui-state/draft-${reference.id}-${chroniques.hautbois}`,
    );
    expect(draft.status()).toBe(404);
  });

  test("E2E-05-13 · Formatting toolbar @nightly", async ({ page, reference }) => {
    const editor = new EditorPage(page);
    await page.goto(reference.url(chroniques.colGeleLieu));
    await editor.open();
    await editor.content.click();
    await page.keyboard.press("Control+Home");
    // Select « Passage », the first word.
    await page.keyboard.press("Shift+Control+ArrowRight");

    await editor.toolbar.getByRole("button", { name: t("editor.bold") }).click();
    await editor.save();

    await expect(page).not.toHaveURL(/edit=true/);
    expect(await markdownOf(page, reference.id, chroniques.colGeleLieu)).toMatch(
      /^\*\*Passage\*\* de haute montagne/,
    );
  });
});

test.describe("perf · typing in a long page", () => {
  test.use({ fakeClock: false });

  test("perf · No frame over 50 ms while typing in a page of 5 000 lines (indicative) @perf", async ({
    page,
    reference,
  }) => {
    const markdown = Array.from(
      { length: 5000 },
      (_, i) => `Ligne ${String(i)} avec [[${chroniques.orsen}]] et un peu de **texte**.`,
    ).join("\n");
    const created = (await (
      await page.request.post(`/api/v1/projects/${reference.id}/pages`, {
        data: { parentId: "0000000000000001", name: "Longue", markdown },
      })
    ).json()) as { id: string };
    await page.goto(`${reference.url(created.id)}?edit=true`);
    const editor = new EditorPage(page);
    await expect(editor.pill("Capitaine Orsen")).toBeVisible();
    await editor.content.click();
    await page.evaluate(() => {
      const w = window as unknown as { __long: number[] };
      w.__long = [];
      new PerformanceObserver((list) => {
        for (const entry of list.getEntries()) w.__long.push(entry.duration);
      }).observe({ type: "longtask", buffered: false });
    });

    await page.keyboard.type("Quelques mots tapés au milieu de la page.", { delay: 20 });

    const long = await page.evaluate(() => (window as unknown as { __long: number[] }).__long);
    test.info().annotations.push({ type: "long-tasks-ms", description: long.join(", ") });
    expect(long.filter((d) => d > 50)).toEqual([]);
  });
});
