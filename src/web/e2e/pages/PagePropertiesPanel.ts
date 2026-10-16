import type { Locator, Page } from "@playwright/test";
import { t } from "../support/i18n";

/** Properties and categories of a page: the side panel in reading (F04.R4), the tabs of the editor (F05.R6, F07.R3). */
export class PagePropertiesPanel {
  /** Pills of the categories, in the header. */
  readonly categories: Locator;
  readonly outOfRulesBadge: Locator;
  /** The list of properties: the side panel in reading, the « Propriétés » tab in edit mode. */
  readonly list: Locator;

  constructor(readonly page: Page) {
    this.categories = page.getByRole("list", { name: t("page.categoriesLabel") });
    this.outOfRulesBadge = page
      .getByRole("article")
      .getByText(t("page.outOfRules"), { exact: true });
    this.list = page.getByRole("list", { name: t("page.propertiesTitle") });
  }

  /** A line of the reading panel, by property name. */
  row(name: string): Locator {
    return this.list
      .getByRole("listitem")
      .filter({ has: this.page.getByRole("link", { name, exact: true }) });
  }

  /** A field of the editor, by property name. */
  field(name: string): Locator {
    return this.page.getByRole("group", { name, exact: true }).first();
  }

  async openTab(tab: "properties" | "categories" | "links" | "files") {
    await this.page.getByRole("tab", { name: t(`page.tabs.${tab}`) }).click();
  }

  async addCategory(name: string) {
    await this.openTab("categories");
    await this.page
      .getByRole("combobox", { name: t("page.addCategory") })
      .selectOption({ label: name });
    await this.page.getByRole("button", { name: t("page.addCategoryButton") }).click();
  }

  get validationStatus(): Locator {
    return this.page.getByRole("contentinfo").getByRole("status");
  }

  get addPropertyButton(): Locator {
    return this.page.getByRole("button", { name: t("page.addProperty") });
  }

  /** « Rétablir la valeur héritée » of one field. */
  restoreButton(field: Locator): Locator {
    return field.getByRole("button", { name: t("page.restoreInherited") });
  }

  async save() {
    await this.page.getByRole("button", { name: t("editor.save") }).click();
    await this.page.getByRole("button", { name: t("page.edit") }).waitFor();
  }
}
