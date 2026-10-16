import { chroniques } from "../fixtures/chroniques";
import { expect, test } from "../fixtures";
import { EditorPage } from "../pages/EditorPage";
import { t } from "../support/i18n";
import { users } from "../support/users";

/** Puts the cursor at the end of the paragraph that contains `text`, then types `added` (nothing is saved). */
async function appendTo(editor: EditorPage, text: string, added: string) {
  await editor.content.getByText(text).first().click();
  await editor.page.keyboard.press("End");
  await editor.type(added);
}

test.describe("F14 · Concurrent editing", () => {
  test.use({ user: "camille" });

  test("E2E-14-07 · Concurrent saves are merged @multi @server", async ({
    page,
    reference,
    openAs,
  }) => {
    const malo = await openAs("malo");
    const camilleEditor = new EditorPage(page);
    const maloEditor = new EditorPage(malo);
    // Both start from the same revision.
    await page.goto(reference.url(chroniques.ilsa));
    await camilleEditor.open();
    await malo.goto(reference.url(chroniques.ilsa));
    await maloEditor.open();

    await appendTo(camilleEditor, "Méfiante envers les soldats", " [Camille: présentation]");
    await camilleEditor.save();
    await expect(page.getByRole("button", { name: t("page.edit") })).toBeVisible();

    await appendTo(maloEditor, "Née à Hautbois", " [Malo: histoire]");
    await maloEditor.save();

    await expect(
      malo.getByRole("status").filter({ hasText: "Fusionné avec les modifications de Camille R." }),
    ).toBeVisible();
    await expect(malo.getByRole("button", { name: t("page.edit") })).toBeVisible();
    await expect(malo.getByText("[Camille: présentation]")).toBeVisible();
    await expect(malo.getByText("[Malo: histoire]")).toBeVisible();
  });

  test("E2E-14-08 · A real conflict opens the resolution view @multi @server", async ({
    page,
    reference,
    openAs,
  }) => {
    const malo = await openAs("malo");
    const camilleEditor = new EditorPage(page);
    const maloEditor = new EditorPage(malo);
    await page.goto(reference.url(chroniques.ilsa));
    await camilleEditor.open();
    await malo.goto(reference.url(chroniques.ilsa));
    await maloEditor.open();

    // The same sentence, continued in two different ways.
    await appendTo(camilleEditor, "Née à Hautbois", " La version de Camille.");
    await camilleEditor.save();
    await expect(page.getByRole("button", { name: t("page.edit") })).toBeVisible();
    await appendTo(maloEditor, "Née à Hautbois", " La version de Malo.");
    await maloEditor.save();

    const dialog = malo.getByRole("dialog", { name: t("conflict.title") });
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("La version de Camille.").first()).toBeVisible();
    await expect(dialog.getByText("La version de Malo.").first()).toBeVisible();

    await dialog.getByRole("radio", { name: t("conflict.keepMine") }).check();
    await dialog.getByRole("button", { name: t("conflict.save") }).click();

    await expect(malo.getByRole("button", { name: t("page.edit") })).toBeVisible();
    await expect(malo.getByText("La version de Malo.")).toBeVisible();
    await expect(malo.getByText("La version de Camille.")).toHaveCount(0);

    // The version of camille stays in the history.
    await malo.goto(`/p/${reference.id}/e/${chroniques.ilsa}/history`);
    await expect(malo.getByText(users.camille.name).first()).toBeVisible();
    const revisions = await malo.request.get(
      `/api/v1/projects/${reference.id}/elements/${chroniques.ilsa}/revisions`,
    );
    const list = (await revisions.json()) as { revision: number; author: string }[];
    const camilleRevision = list.find((r) => r.author === "camille" && r.revision > 1);
    expect(camilleRevision).toBeDefined();
    const detail = await malo.request.get(
      `/api/v1/projects/${reference.id}/elements/${chroniques.ilsa}/revisions/${String(camilleRevision?.revision)}`,
    );
    expect(await detail.text()).toContain("La version de Camille.");
  });

  test("E2E-14-09 · An expired session asks for the password and loses nothing @server", async ({
    page,
    reference,
  }) => {
    const editor = new EditorPage(page);
    await page.goto(reference.url(chroniques.ilsa));
    await editor.open();
    await appendTo(editor, "Méfiante envers les soldats", " [écrit avant l’expiration]");

    // The server forgets every session while camille is writing.
    expect((await page.request.post("/_test/expire-sessions")).ok()).toBe(true);
    await editor.save();

    const dialog = page.getByRole("dialog", { name: t("auth.expired.title") });
    await expect(dialog).toBeVisible();
    await dialog.getByLabel(t("auth.login")).fill(users.camille.id);
    await dialog.getByLabel(t("auth.password")).fill(users.camille.password);
    await dialog.getByRole("button", { name: t("auth.submit") }).click();

    await expect(page.getByRole("button", { name: t("page.edit") })).toBeVisible();
    await expect(page.getByText("[écrit avant l’expiration]")).toBeVisible();
  });
});
