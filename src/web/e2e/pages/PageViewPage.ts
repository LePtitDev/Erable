import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** Reading a page (F04): article, table of contents, side panel. */
export class PageViewPage {
  readonly article: Locator;
  readonly title: Locator;
  readonly breadcrumb: Locator;
  readonly toc: Locator;
  readonly content: Locator;

  constructor(readonly page: Page) {
    this.article = page.getByRole("article");
    this.title = this.article.getByRole("heading", { level: 1 });
    this.breadcrumb = this.article.getByRole("navigation", { name: t("shell.breadcrumb") });
    this.toc = page.getByRole("navigation", { name: t("page.toc") });
    this.content = this.article.locator("[lang]").first();
  }

  idButton(id: string): Locator {
    return this.article.getByRole("button", { name: t("element.copyId", { id }) });
  }

  /** A reference or link of the content, by its visible text. */
  link(text: string): Locator {
    return this.content.getByRole("link", { name: text, exact: true }).first();
  }

  /** Captures what the page writes to the clipboard (same behaviour in Chromium and WebKit). */
  static async captureClipboard(page: Page) {
    await page.addInitScript(() => {
      const w = window as unknown as { __clipboard?: string };
      Object.defineProperty(navigator, "clipboard", {
        configurable: true,
        value: {
          writeText: (text: string) => {
            w.__clipboard = text;
            return Promise.resolve();
          },
          readText: () => Promise.resolve(w.__clipboard ?? ""),
        },
      });
    });
  }

  async clipboard(): Promise<string | undefined> {
    return this.page.evaluate(() => (window as unknown as { __clipboard?: string }).__clipboard);
  }
}
