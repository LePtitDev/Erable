import { expect, type Locator, type Page } from "@playwright/test";
import { t } from "../support/i18n";

/** The explorer (F02): lines found by their accessible name, ARIA state read from the DOM. */
export class TreePanel {
  readonly tree: Locator;

  constructor(readonly page: Page) {
    this.tree = page.getByRole("tree", { name: t("explorer.title") });
  }

  /** A line by name; the first one in tree order when several share it (« Personnages » in Pages and Propriétés). */
  item(name: string, which: "first" | "last" = "first"): Locator {
    const lines = this.tree.getByRole("treeitem", { name, exact: true });
    return which === "first" ? lines.first() : lines.last();
  }

  root(name: string): Locator {
    return this.tree.getByRole("treeitem", { name: new RegExp(`^${name}\\s*\\d*$`) });
  }

  /** Expands a line if it is collapsed (click on its caret through the keyboard alternative). */
  async expand(name: string) {
    const item = this.item(name);
    if ((await item.getAttribute("aria-expanded")) === "false") {
      await item.click();
      await this.tree.press("ArrowRight");
      await expect(item).toHaveAttribute("aria-expanded", "true");
    }
  }

  async collapse(name: string) {
    const item = this.item(name);
    if ((await item.getAttribute("aria-expanded")) === "true") {
      await item.click();
      await this.tree.press("ArrowLeft");
      await expect(item).toHaveAttribute("aria-expanded", "false");
    }
  }

  /** Expands each name of the path in turn: « Pages, Personnages, PNJ ». */
  async reveal(...path: string[]) {
    for (const name of path) await this.expand(name);
  }

  async newFolder(name: string) {
    await this.page.getByRole("button", { name: t("explorer.newFolder") }).click();
    await this.tree.getByRole("textbox", { name: t("explorer.newName.folder") }).fill(name);
    await this.page.keyboard.press("Enter");
  }

  async newPage(name: string) {
    await this.page.getByRole("button", { name: t("explorer.newPage") }).click();
    await this.tree.getByRole("textbox", { name: t("explorer.newName.page") }).fill(name);
    await this.page.keyboard.press("Enter");
  }

  /** Creates a category from the context menu of a line (the Catégories root or one of its folders). */
  async newCategory(name: string, under = "Catégories") {
    const target = under === "Catégories" ? this.root(under) : this.item(under);
    await target.click({ button: "right" });
    await this.page.getByRole("menuitem", { name: t("explorer.newCategory") }).click();
    await this.tree.getByRole("textbox", { name: t("explorer.newName.category") }).fill(name);
    await this.page.keyboard.press("Enter");
  }

  /** Creates a property from the context menu of a line (the Propriétés root or one of its folders). */
  async newProperty(name: string, under = "Propriétés") {
    const target = under === "Propriétés" ? this.root(under) : this.item(under);
    await target.click({ button: "right" });
    await this.page.getByRole("menuitem", { name: t("explorer.newProperty") }).click();
    await this.tree.getByRole("textbox", { name: t("explorer.newName.property") }).fill(name);
    await this.page.keyboard.press("Enter");
  }

  /** Creates a link type from the context menu of the Liens root. */
  async newLinkType(name: string) {
    await this.root("Liens").click({ button: "right" });
    await this.page.getByRole("menuitem", { name: t("explorer.newLinkType") }).click();
    await this.tree.getByRole("textbox", { name: t("explorer.newName.linkType") }).fill(name);
    await this.page.keyboard.press("Enter");
  }

  async rename(name: string, newName: string) {
    await this.item(name).click();
    await this.tree.press("F2");
    await this.tree.getByRole("textbox", { name: t("explorer.renameLabel") }).fill(newName);
    await this.page.keyboard.press("Enter");
  }

  /** Puts the line in the trash with the Delete key; the confirmation dialog stays to answer. */
  async startDelete(name: string) {
    await this.item(name).click();
    await this.tree.press("Delete");
  }

  /** Children of a line, by name, in order (the lines right below it with a deeper level). */
  async childrenOf(name: string): Promise<string[]> {
    return this.tree.evaluate((tree, parentName) => {
      const items = [...tree.querySelectorAll<HTMLElement>('[role="treeitem"]')];
      const at = items.findIndex(
        (i) => i.querySelector("span:not([aria-hidden])")?.textContent === parentName,
      );
      if (at < 0) return [];
      const level = Number(items[at]?.getAttribute("aria-level"));
      const result: string[] = [];
      for (const item of items.slice(at + 1)) {
        const l = Number(item.getAttribute("aria-level"));
        if (l <= level) break;
        if (l === level + 1)
          result.push(item.querySelector("span:not([aria-hidden])")?.textContent ?? "");
      }
      return result;
    }, name);
  }
}
