import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The « Liens » section of a page: typed links and implicit references (F09). */
export class LinksPanel {
  readonly panel: Locator;
  readonly outgoing: Locator;
  readonly mentions: Locator;

  constructor(readonly page: Page) {
    this.panel = page.getByRole("region", { name: t("links.title"), exact: true });
    this.outgoing = this.panel.getByRole("list", { name: t("links.outgoing") });
    this.mentions = this.panel.getByRole("list", { name: t("links.mentionsList") });
  }

  /** Incoming typed links, whatever their count (« 2 liens entrants »). */
  get incoming(): Locator {
    return this.panel.getByRole("list", { name: /entrants?$/ });
  }

  async link(type: string, target: string) {
    await this.panel.getByRole("button", { name: t("links.link"), exact: true }).click();
    const dialog = this.page.getByRole("dialog", { name: t("links.linkTitle") });
    await dialog.getByRole("combobox", { name: t("links.type") }).selectOption({ label: type });
    await dialog.getByRole("combobox", { name: t("links.target") }).selectOption({ label: target });
    await dialog.getByRole("button", { name: t("links.confirm") }).click();
    await dialog.waitFor({ state: "hidden" });
  }

  async remove(label: string, name: string) {
    await this.panel.getByRole("button", { name: t("links.remove", { label, name }) }).click();
  }
}
