import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** « Récemment consultées » under the explorer (F11). */
export class RecentPanel {
  readonly section: Locator;
  readonly entries: Locator;

  constructor(readonly page: Page) {
    this.section = page.getByRole("region").filter({
      has: page.getByRole("heading", { name: t("explorer.recent") }),
    });
    this.entries = this.section.getByRole("link");
  }

  /** The element being shown stays two seconds on screen, which counts as a view (F11.R1). */
  async dwell() {
    const recorded = this.page.waitForResponse(
      (r) => r.url().includes("/me/views") && r.request().method() === "POST",
    );
    await this.page.clock.runFor(2500);
    await recorded;
  }
}
