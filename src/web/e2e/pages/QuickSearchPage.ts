import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The quick search palette (F10.R1, Ctrl+K). */
export class QuickSearchPage {
  readonly dialog: Locator;
  readonly field: Locator;
  readonly options: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("dialog", { name: t("search.quick.title") });
    this.field = this.dialog.getByRole("combobox", { name: t("search.quick.field") });
    this.options = this.dialog.getByRole("option");
  }

  async open() {
    // The shortcut is registered once the project screen has rendered (the tree is there): not before.
    await this.page.getByRole("tree").waitFor();
    await this.page.keyboard.press("Control+k");
    await this.field.waitFor();
  }
}
