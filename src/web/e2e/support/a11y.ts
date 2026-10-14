import AxeBuilder from "@axe-core/playwright";
import { expect, type Page } from "@playwright/test";

/** No « serious » or « critical » axe-core violation on the current screen (docs/testing.md §9.3). */
export async function expectNoSeriousViolations(page: Page) {
  const results = await new AxeBuilder({ page })
    .withTags(["wcag2a", "wcag2aa", "wcag21aa"])
    .analyze();
  const serious = results.violations
    .filter((v) => v.impact === "serious" || v.impact === "critical")
    .map((v) => `${v.id}: ${v.nodes.map((n) => n.target.join(" ")).join(", ")}`);
  expect(serious).toEqual([]);
}
