import { randomBytes } from "node:crypto";
import { cpSync, mkdtempSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
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

  test("E2E-01-05 · Migrate a project of an older format @nightly", async ({
    home,
    page,
    freshProfile,
  }) => {
    expect(freshProfile.slot).not.toBe("");
    const legacy = join(
      import.meta.dirname,
      "..",
      "..",
      "..",
      "..",
      "spec",
      "projects",
      "legacy",
      "v1",
    );
    const folder = mkdtempSync(join(tmpdir(), "erable-legacy-"));
    cpSync(legacy, folder, { recursive: true });
    // Tests share one backend: a copy of the frozen project needs an identifier of its own.
    const manifest = join(folder, "erable.json");
    const projectId = randomBytes(8).toString("hex").toUpperCase();
    writeFileSync(
      manifest,
      JSON.stringify({ ...(JSON.parse(readFileSync(manifest, "utf8")) as object), projectId }),
    );
    await home.goto();

    await home.openFolder(folder);
    const confirm = page.getByRole("alertdialog", { name: t("home.migrate.title") });
    await expect(confirm).toBeVisible();
    // Nothing is written before the user confirms.
    expect(readdirSync(folder).some((name) => name.startsWith("erable.db.bak-"))).toBe(false);
    await confirm.getByRole("button", { name: t("home.migrate.confirm") }).click();

    await expect(page.getByRole("tree", { name: t("explorer.title") })).toBeVisible();
    expect(readdirSync(folder).some((name) => name.startsWith("erable.db.bak-"))).toBe(true);
    await page.goto(`/p/${projectId}/e/${chroniques.ilsa}`);
    // The references of the page are resolved: the link carries the name of its target.
    await expect(page.getByRole("link", { name: "Capitaine Orsen" }).first()).toBeVisible();
    await page.request.post(`/_test/close?projectId=${projectId}`);
    rmSync(folder, { recursive: true, force: true });
  });

  test("M1-17 · Opening a folder lands on the project, not on an element named after its path @pr", async ({
    home,
    page,
    reference,
  }) => {
    await home.goto();

    await home.openFolder(reference.folder);

    await expect(page.getByRole("tree", { name: t("explorer.title") })).toBeVisible();
    await expect(page).toHaveURL(new RegExp(`/p/${reference.id}(/e/[0-9A-F]{16})?$`));
  });
});
