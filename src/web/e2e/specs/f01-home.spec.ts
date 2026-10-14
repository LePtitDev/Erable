import { readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";
import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { fingerprint } from "../support/files";
import { t } from "../support/i18n";

test.describe("F01 · Home and projects", () => {
  test("E2E-01-01 · Create an empty project @pr @desktop", async ({ home, page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    await home.goto();

    await home.createProject("Atlas", "Français");

    const tree = page.getByRole("tree", { name: t("explorer.title") });
    for (const root of ["Pages", "Catégories", "Propriétés", "Liens"]) {
      await expect(tree.getByRole("treeitem", { name: new RegExp(`^${root}`) })).toBeVisible();
    }
    await page.goto("/");
    await expect(home.recentList.getByRole("listitem").first()).toContainText("Atlas");
  });

  test("E2E-01-02 · Reopen a recent project @pr", async ({ home, page, reference }) => {
    await page.request.put(`/api/v1/me/recent-projects/${reference.id}/last-element`, {
      data: { elementId: chroniques.hautbois },
    });
    await home.goto();

    await home.openRecent(chroniques.name);

    await expect(page).toHaveURL(reference.url(chroniques.hautbois));
    await expect(page.getByRole("heading", { level: 1, name: "Hautbois" })).toBeVisible();
  });

  test("E2E-01-03 · Recent project not found @nightly", async ({ home, page, reference }) => {
    await page.request.post(`/_test/close?projectId=${reference.id}`);
    rmSync(reference.folder, { recursive: true, force: true });
    await home.goto();

    const item = home.recent(chroniques.name);
    await expect(item).toContainText(t("home.recent.status.missing"));
    await item.getByRole("button", { name: t("home.recent.remove") }).click();

    await expect(item).toHaveCount(0);
    expect(() => readFileSync(join(reference.folder, "erable.json"))).toThrow();
  });

  test("E2E-01-04 · Open a project of a newer format @nightly", async ({
    home,
    page,
    reference,
  }) => {
    await page.request.post(`/_test/close?projectId=${reference.id}`);
    const manifest = join(reference.folder, "erable.json");
    const json = JSON.parse(readFileSync(manifest, "utf8")) as Record<string, unknown>;
    writeFileSync(manifest, JSON.stringify({ ...json, formatVersion: 99 }, null, 2));
    const before = fingerprint(reference.folder);
    await home.goto();

    await home.openFolder(reference.folder);

    await expect(home.openDialog.getByRole("alert")).toContainText(
      t("home.open.status.tooNew", { version: 99 }),
    );
    expect(fingerprint(reference.folder)).toEqual(before);
  });
});
