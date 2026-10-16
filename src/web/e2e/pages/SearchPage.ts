import { expect, type Locator, type Page } from "@playwright/test";
import { t, tp } from "../support/i18n";

/** The search screen (F10, mockup « Recherche »). */
export class SearchPage {
  readonly field: Locator;
  readonly filters: Locator;
  readonly list: Locator;
  readonly titles: Locator;
  readonly total: Locator;
  readonly sort: Locator;
  readonly reset: Locator;
  readonly includeSubCategories: Locator;
  readonly outOfRules: Locator;
  readonly activity: Locator;

  constructor(readonly page: Page) {
    this.field = page.getByRole("searchbox", { name: t("search.field") });
    this.filters = page.getByRole("complementary", { name: t("search.filters") });
    this.list = page.getByRole("list", { name: t("search.title") });
    this.titles = this.list.getByTestId("result-title");
    this.total = page.getByRole("heading", { level: 2 }).filter({ hasText: /résultat/ });
    this.sort = page.getByRole("combobox", { name: t("search.sortBy") });
    this.reset = this.filters.getByRole("button", { name: t("search.reset"), exact: true });
    this.includeSubCategories = this.filters.getByRole("checkbox", {
      name: t("search.includeSub"),
    });
    this.outOfRules = this.filters.getByRole("checkbox", { name: t("search.outOfRules") });
    this.activity = page.getByRole("complementary", { name: t("search.activity") });
  }

  async goto(projectId: string, query = "") {
    await this.page.goto(`/p/${projectId}/search${query}`);
    await this.field.waitFor();
  }

  async search(text: string) {
    await this.field.fill(text);
    await this.field.press("Enter");
  }

  expectedTotal(count: number, text?: string): string {
    return text === undefined
      ? tp("search.results", count)
      : tp("search.resultsFor", count, { query: text });
  }

  result(name: string): Locator {
    // By title only: the excerpts of other results may contain the same words.
    return this.list.getByRole("listitem").filter({
      has: this.page.getByTestId("result-title").filter({ hasText: new RegExp(`^${name}$`) }),
    });
  }

  /** A line of the type filter; its accessible name is the label followed by the counter (« Pages 3 »). */
  kind(label: string): Locator {
    return this.filters.getByRole("checkbox", { name: label });
  }

  category(name: string): Locator {
    return this.filters.getByRole("button", { name, exact: true });
  }

  async chooseCategory(name: string) {
    const chip = this.category(name);
    if ((await chip.count()) === 0) {
      await this.filters.getByRole("button", { name: t("search.otherCategory") }).click();
      await this.page.getByRole("menuitem", { name, exact: true }).click();
    } else {
      await chip.click();
    }
    await expect(this.category(name)).toHaveAttribute("aria-pressed", "true");
  }

  async addCondition(property: string, operator: string, value?: string) {
    await this.filters.getByRole("button", { name: t("search.addCondition") }).click();
    const row = this.filters
      .getByRole("group")
      .filter({ has: this.page.getByRole("combobox", { name: t("search.property") }) })
      .last();
    await row
      .getByRole("combobox", { name: t("search.property") })
      .selectOption({ label: property });
    await row
      .getByRole("combobox", { name: t("search.operator") })
      .selectOption({ label: operator });
    if (value !== undefined) {
      const input = row.getByRole("textbox", { name: t("search.value") });
      if ((await input.count()) > 0) await input.fill(value);
      else
        await row.getByRole("combobox", { name: t("search.value") }).selectOption({ label: value });
    }
  }

  period(label: string): Locator {
    return this.filters.getByRole("button", { name: label, exact: true });
  }
}
