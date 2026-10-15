import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The category screen (F07, mockup « Catégorie — définition »). */
export class CategoryViewPage {
  readonly article: Locator;
  readonly title: Locator;
  readonly lineage: Locator;
  readonly properties: Locator;
  readonly pages: Locator;
  readonly includeSubCategories: Locator;

  constructor(readonly page: Page) {
    this.article = page.getByRole("article");
    this.title = this.article.getByRole("heading", { level: 1 });
    this.lineage = this.article.getByRole("navigation", { name: t("category.lineage") });
    this.properties = this.article.getByRole("table");
    this.pages = this.article.getByRole("list", { name: t("category.pages") });
    this.includeSubCategories = this.article.getByRole("checkbox", {
      name: t("category.includeSubCategories"),
    });
  }

  /** The row of a property in the table. */
  property(name: string): Locator {
    return this.properties.getByRole("row", { name: new RegExp(`^${name}\\b`) });
  }

  subCategory(name: string): Locator {
    return this.article.getByRole("link", { name, exact: true });
  }

  async edit() {
    await this.article.getByRole("button", { name: t("page.edit") }).click();
  }

  get description(): Locator {
    return this.article.getByRole("textbox", { name: t("category.description") });
  }

  get parent(): Locator {
    return this.article.getByRole("combobox", { name: t("category.parent") });
  }

  valueOf(property: string): Locator {
    return this.article.getByLabel(t("category.valueOf", { name: property }));
  }

  async save() {
    await this.article.getByRole("button", { name: t("editor.save") }).click();
    await this.article.getByRole("button", { name: t("page.edit") }).waitFor();
  }
}
