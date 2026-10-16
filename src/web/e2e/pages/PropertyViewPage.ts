import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The property screen (F08, mockup « Propriété — définition et règles »). */
export class PropertyViewPage {
  readonly article: Locator;
  readonly title: Locator;
  readonly rules: Locator;
  readonly usedBy: Locator;
  readonly outOfRules: Locator;
  readonly testResult: Locator;

  constructor(readonly page: Page) {
    this.article = page.getByRole("article");
    this.title = this.article.getByRole("heading", { level: 1 });
    this.rules = this.article.getByRole("list", { name: t("property.rules") });
    this.usedBy = this.article.getByRole("list", { name: t("property.usedBy") });
    this.outOfRules = this.article.getByRole("list", { name: t("property.outOfRulesList") });
    this.testResult = this.article.getByRole("status", { name: t("property.test.result") });
  }

  /** « 2 pages hors règles » (the title of the card). */
  outOfRulesTitle(count: number): Locator {
    return this.article.getByText(
      t(`property.outOfRules_${count === 1 ? "one" : "other"}`, { count }),
      { exact: true },
    );
  }

  filled(count: number): Locator {
    return this.article.getByText(
      t(`property.filled_${count === 1 ? "one" : "other"}`, { count }),
      { exact: true },
    );
  }

  async edit() {
    await this.article.getByRole("button", { name: t("page.edit") }).click();
  }

  async save() {
    await this.article.getByRole("button", { name: t("editor.save") }).click();
    await this.article.getByRole("button", { name: t("page.edit") }).waitFor();
  }

  get name(): Locator {
    return this.article.getByRole("textbox", { name: t("property.name") });
  }

  get key(): Locator {
    return this.article.getByRole("textbox", { name: t("property.key"), exact: true });
  }

  async chooseType(type: string) {
    await this.article
      .getByRole("radiogroup", { name: t("property.type") })
      .getByRole("radio", { name: new RegExp(`^${type}\\b`) })
      .check({ force: true });
  }

  /** The line of a rule by operator label (« ≤ »). */
  rule(op: string): Locator {
    return this.article.getByRole("group", { name: t(`property.op.${op}`), exact: true });
  }

  async addRule(op: string) {
    await this.article
      .getByRole("group", { name: t("property.addRule") })
      .getByRole("button", { name: t(`property.op.${op}`), exact: true })
      .click();
  }

  get noBooleanRule(): Locator {
    return this.article.getByText(t("property.noRulesBool")).first();
  }

  get tester(): Locator {
    return this.article.getByRole("textbox", { name: t("property.test.label") });
  }

  /** Option fields of an enum, in order; found by their current text. */
  option(label: string): Locator {
    return this.article
      .getByRole("list", { name: t("property.options") })
      .getByRole("textbox")
      .and(this.page.locator(`[value="${label}"]`));
  }

  async removeOption(label: string) {
    await this.article.getByRole("button", { name: t("property.removeOption", { label }) }).click();
  }
}
