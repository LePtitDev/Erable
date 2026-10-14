import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** Home screen (F01): actions, recent projects, dialogs. */
export class HomePage {
  readonly greeting: Locator;
  readonly recentList: Locator;

  constructor(readonly page: Page) {
    this.greeting = page.getByRole("heading", { level: 1 });
    this.recentList = page.getByRole("region", { name: t("home.recent.title") });
  }

  async goto() {
    await this.page.goto("/");
  }

  version(appVersion: string, mode: "desktop" | "server") {
    return this.page.getByText(
      `${t("home.version", { version: appVersion })} · ${t(`app.mode.${mode}`)}`,
    );
  }

  /** « Nouveau projet »: name, content language, location left to its default unless given. */
  async createProject(name: string, language: string, location?: string) {
    await this.page.getByRole("button", { name: t("home.new.title") }).click();
    const dialog = this.page.getByRole("dialog", { name: t("home.new.dialogTitle") });
    await dialog.getByLabel(t("home.new.name")).fill(name);
    await dialog.getByLabel(t("home.new.language")).selectOption({ label: language });
    if (location !== undefined) {
      await dialog.getByLabel(t("home.new.location")).fill(location);
    }
    await dialog.getByRole("button", { name: t("home.new.create") }).click();
  }

  /** « Ouvrir un dossier… » through Ctrl+O, with a typed path. */
  async openFolder(folder: string) {
    // The shortcut is listened to once the screen is rendered.
    await this.page.getByRole("button", { name: t("home.open.title") }).waitFor();
    await this.page.keyboard.press("Control+o");
    await this.openDialog.getByLabel(t("home.open.folder")).fill(folder);
    await this.openDialog.getByRole("button", { name: t("home.open.submit") }).click();
  }

  get openDialog(): Locator {
    return this.page.getByRole("dialog", { name: t("home.open.dialogTitle") });
  }

  recent(name: string): Locator {
    return this.recentList.getByRole("listitem").filter({ hasText: name });
  }

  async openRecent(name: string) {
    await this.recent(name).getByRole("button", { name }).click();
  }
}
