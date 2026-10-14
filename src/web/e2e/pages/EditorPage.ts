import { expect, type Locator, type Page } from "@playwright/test";
import { t } from "../support/i18n";

/** Editing a page (F05): CodeMirror, toolbar, draft banner, confirmations. */
export class EditorPage {
  readonly content: Locator;
  readonly title: Locator;
  readonly toolbar: Locator;
  readonly rawIds: Locator;

  constructor(readonly page: Page) {
    this.content = page.getByRole("textbox", { name: t("editor.content") });
    this.title = page.getByRole("textbox", { name: t("editor.title") });
    this.toolbar = page.getByRole("toolbar", { name: t("editor.toolbar") });
    this.rawIds = page.getByRole("checkbox", { name: t("editor.rawIds") });
  }

  async open() {
    await this.page.getByRole("button", { name: t("page.edit") }).click();
    await expect(this.content).toBeVisible();
  }

  /** Puts the cursor at the very end of the document. */
  async toEnd() {
    await this.content.click();
    await this.page.keyboard.press("Control+End");
  }

  async type(text: string) {
    await this.page.keyboard.type(text);
  }

  async save() {
    await this.page.keyboard.press("Control+s");
  }

  pill(text: string): Locator {
    // Exact text: « Hautbois » must not match the pill « carte_hautbois.webp ».
    return this.content
      .locator(".cm-pill")
      .filter({ hasText: text })
      .and(this.content.getByText(text, { exact: true }))
      .first();
  }

  /** The text shown by the editor (pills show their name, the raw IDs stay in the document). */
  async shownText(): Promise<string> {
    return (await this.content.textContent()) ?? "";
  }

  get draftSaved(): Locator {
    // « Brouillon enregistré · il y a 2 s »: matched on the part before the date.
    const prefix = t("editor.draftSaved", { when: "" }).split("·")[0]?.trim() ?? "";
    return this.page.getByRole("status").filter({ hasText: prefix });
  }
}
