import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The history of a page (F13). */
export class HistoryPage {
  readonly list: Locator;
  readonly entries: Locator;

  constructor(readonly page: Page) {
    this.list = page.getByRole("list", { name: t("history.list") });
    this.entries = this.list.getByRole("listitem");
  }

  async open() {
    await this.page.getByRole("button", { name: t("history.button") }).click();
    await this.list.waitFor();
  }

  async compare(...revisions: number[]) {
    for (const revision of revisions) {
      await this.page.getByRole("checkbox", { name: t("history.compare", { revision }) }).check();
    }
  }

  diff(from: number, to: number): Locator {
    return this.page.getByRole("region", { name: t("history.diffLabel", { from, to }) });
  }

  async restore(revision: number) {
    await this.page.getByRole("button", { name: t("history.restore", { revision }) }).click();
  }
}
