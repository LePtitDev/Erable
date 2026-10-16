import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The members screen of a project (M4-08, F14.R1). */
export class MembersPage {
  readonly heading: Locator;

  constructor(private readonly page: Page) {
    this.heading = page.getByRole("heading", { level: 1, name: t("members.title") });
  }

  async open(projectId: string): Promise<void> {
    await this.page.goto(`/p/${projectId}/members`);
  }

  roleOf(name: string): Locator {
    return this.page.getByRole("combobox", { name: t("members.roleOf", { name }) });
  }
}
