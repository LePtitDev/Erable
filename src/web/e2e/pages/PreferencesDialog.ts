import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** « Préférences » (F15), reachable from the home screen and from a project. */
export class PreferencesDialog {
  readonly dialog: Locator;
  readonly language: Locator;

  constructor(readonly page: Page) {
    this.dialog = page.getByRole("dialog", { name: t("preferences.title") });
    this.language = this.dialog.getByRole("combobox", { name: t("preferences.language") });
  }

  /** From the home screen navigation. */
  async openFromHome() {
    await this.page.getByRole("button", { name: t("home.nav.preferences") }).click();
  }

  /** From the top bar of a project (the button shows the current language). */
  async openFromProject(language = "FR") {
    await this.page.getByRole("button", { name: t("shell.languageNamed", { language }) }).click();
  }

  theme(name: "light" | "dark" | "system"): Locator {
    return this.dialog.getByRole("radio", { name: t(`preferences.theme.${name}`) });
  }
}

/** Relative luminance (0 black, 1 white) of the page background, as painted. */
export async function backgroundLuminance(page: Page): Promise<number> {
  const color = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  const [r = 0, g = 0, b = 0] = (color.match(/\d+(\.\d+)?/g) ?? []).map(Number);
  return (0.2126 * r + 0.7152 * g + 0.0722 * b) / 255;
}
