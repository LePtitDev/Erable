import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";
import type { SampleFile } from "../support/samples";

/** « Pièces jointes » of the page side panel (F06). */
export class AttachmentsPanel {
  readonly section: Locator;
  readonly addButton: Locator;

  constructor(readonly page: Page) {
    this.section = page.getByRole("region", { name: t("attachments.title") });
    this.addButton = this.section.getByRole("button", { name: t("attachments.addLabel") });
  }

  /** A stored attachment (its name is a link; a file still uploading shows a progress bar instead). */
  item(fileName: string): Locator {
    return this.section
      .getByRole("listitem")
      .filter({ has: this.page.getByRole("link", { name: fileName, exact: true }) });
  }

  /** « + Ajouter », then the files in the OS file chooser. */
  async add(...files: SampleFile[]) {
    const chooser = this.page.waitForEvent("filechooser");
    await this.addButton.click();
    await (await chooser).setFiles(files.map((f) => ({ ...f })));
  }
}
