import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage } from "../pages/EditorPage";
import { HistoryPage } from "../pages/HistoryPage";
import { PageViewPage } from "../pages/PageViewPage";
import { t } from "../support/i18n";

/** Hautbois saved three times: four revisions in all (the reference project holds the first). */
async function editThreeTimes(page: import("@playwright/test").Page, url: string) {
  const editor = new EditorPage(page);
  await page.goto(url);
  for (const text of ["Un puits.", "Une tour.", "Un marché."]) {
    await editor.open();
    await editor.toEnd();
    await editor.type(`\n\n${text}`);
    await editor.save();
    await expect(page.getByRole("button", { name: t("page.edit") })).toBeVisible();
  }
}

test.describe("F13 · Revision history", () => {
  test("E2E-13-01 · View the history @pr", async ({ page, reference }) => {
    const history = new HistoryPage(page);
    await editThreeTimes(page, reference.url(chroniques.hautbois));

    await history.open();

    await expect(history.entries).toHaveCount(4);
    await expect(history.entries.first()).toContainText(t("history.revision", { revision: 4 }));
    await expect(history.entries.first()).toContainText(t("element.me"));
    await expect(history.entries.last()).toContainText(t("history.change.created"));
  });

  test("E2E-13-02 · Compare two revisions @pr", async ({ page, reference }) => {
    const history = new HistoryPage(page);
    await editThreeTimes(page, reference.url(chroniques.hautbois));
    await history.open();

    await history.compare(1, 4);

    const diff = history.diff(1, 4);
    await expect(diff).toBeVisible();
    await expect(diff.locator("ins", { hasText: "Un puits." })).toBeVisible();
    await expect(diff.locator("ins", { hasText: "Un marché." })).toBeVisible();
    await expect(diff.locator("del")).toHaveCount(0);
    await expect(page).toHaveURL(/from=1&to=4/);
  });

  test("E2E-13-03 · Restore a revision @pr", async ({ page, reference }) => {
    const history = new HistoryPage(page);
    const view = new PageViewPage(page);
    await editThreeTimes(page, reference.url(chroniques.hautbois));
    await history.open();

    await history.restore(2);

    await expect(history.entries).toHaveCount(5);
    await expect(history.entries.first()).toContainText(t("history.restoration", { revision: 2 }));
    await page.getByRole("link", { name: t("history.back") }).click();
    await expect(view.content).toContainText("Un puits.");
    await expect(view.content).not.toContainText("Une tour.");
    await expect(view.content).not.toContainText("Un marché.");
  });
});
