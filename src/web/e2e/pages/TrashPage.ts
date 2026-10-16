import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The delete confirmation and the trash (F12). */
export class TrashPage {
  readonly dialog: Locator;
  readonly view: Locator;
  readonly list: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("alertdialog");
    this.view = page.getByRole("region", { name: t("trash.title"), exact: true });
    this.list = this.view.getByRole("list", { name: t("trash.list") });
  }

  async confirmDelete() {
    await this.dialog.getByRole("button", { name: t("trash.deleteConfirm") }).click();
    await this.dialog.waitFor({ state: "hidden" });
  }

  async open() {
    await this.page.getByRole("link", { name: new RegExp(`^${t("trash.title")}`) }).click();
    await this.view.waitFor();
  }

  entry(name: string): Locator {
    return this.list.getByRole("listitem").filter({ hasText: name });
  }

  async restore(name: string) {
    await this.view.getByRole("button", { name: t("trash.restoreLabel", { name }) }).click();
  }

  async purge(name: string) {
    await this.view.getByRole("button", { name: t("trash.purgeLabel", { name }) }).click();
    await this.dialog.getByRole("button", { name: t("trash.purge") }).click();
    await this.dialog.waitFor({ state: "hidden" });
  }
}
