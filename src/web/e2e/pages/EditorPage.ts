import { expect, type Locator, type Page } from "@playwright/test";
import { t } from "../support/i18n";
import type { SampleFile } from "../support/samples";

type Transfer = { name: string; mimeType: string; base64: string }[];

function toTransfer(files: SampleFile[]): Transfer {
  return files.map((f) => ({
    name: f.name,
    mimeType: f.mimeType,
    base64: f.buffer.toString("base64"),
  }));
}

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
    await this.page.keyboard.press("ControlOrMeta+s");
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

  /** Pastes files at the cursor, as from the OS clipboard. */
  async pasteFiles(...files: SampleFile[]) {
    await this.content.evaluate((target, transfer: Transfer) => {
      const data = new DataTransfer();
      for (const f of transfer) {
        const bytes = Uint8Array.from(atob(f.base64), (c) => c.charCodeAt(0));
        data.items.add(new File([bytes], f.name, { type: f.mimeType }));
      }
      const event = new Event("paste", { bubbles: true, cancelable: true });
      Object.defineProperty(event, "clipboardData", { value: data });
      target.dispatchEvent(event);
    }, toTransfer(files));
  }

  /** Drops files at the start of the content, as from the OS file manager. */
  async dropFiles(...files: SampleFile[]) {
    const box = await this.content.boundingBox();
    await this.content.evaluate(
      (target, { transfer, x, y }: { transfer: Transfer; x: number; y: number }) => {
        const data = new DataTransfer();
        for (const f of transfer) {
          const bytes = Uint8Array.from(atob(f.base64), (c) => c.charCodeAt(0));
          data.items.add(new File([bytes], f.name, { type: f.mimeType }));
        }
        const event = new DragEvent("drop", {
          bubbles: true,
          cancelable: true,
          clientX: x,
          clientY: y,
          dataTransfer: data,
        });
        target.dispatchEvent(event);
      },
      { transfer: toTransfer(files), x: (box?.x ?? 0) + 4, y: (box?.y ?? 0) + 4 },
    );
  }

  /** Placeholders of the uploads still running. */
  get uploads(): Locator {
    return this.content.getByRole("progressbar");
  }

  get draftSaved(): Locator {
    // « Brouillon enregistré · il y a 2 s »: matched on the part before the date.
    const prefix = t("editor.draftSaved", { when: "" }).split("·")[0]?.trim() ?? "";
    return this.page.getByRole("status").filter({ hasText: prefix });
  }
}

/** The insert palette (F05.R3). */
export class InsertPalette {
  readonly dialog: Locator;
  readonly options: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("dialog", { name: t("palette.title") });
    this.options = this.dialog.getByRole("option");
  }

  tab(name: string): Locator {
    return this.dialog.getByRole("tab", { name, exact: true });
  }
}
