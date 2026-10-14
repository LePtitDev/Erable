import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** Temporary M0 home page (replaced by HomePage in M1-08). */
export class HelloPage {
  readonly heading: Locator;
  readonly languageSelect: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { level: 1 });
    this.languageSelect = page.getByLabel(t("preferences.language"));
  }

  async goto() {
    await this.page.goto("/");
  }

  version(appVersion: string, mode: "desktop" | "server") {
    const text = `${t("app.version", { version: appVersion, apiVersion: "v1" })} · ${t(`app.mode.${mode}`)}`;
    return this.page.getByText(text);
  }
}
