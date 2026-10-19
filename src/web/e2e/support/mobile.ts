import { expect, type Page } from "@playwright/test";

/** F18.R3: the document is never wider than the screen; wide blocks scroll inside their own frame. */
export async function expectNoHorizontalScroll(page: Page, screen: string): Promise<void> {
  const overflow = await page.evaluate(() => {
    const width = document.documentElement.clientWidth;
    const culprits = [...document.querySelectorAll<HTMLElement>("body *")]
      .filter((element) => {
        const box = element.getBoundingClientRect();
        if (box.width === 0 || box.right <= width + 1) return false;
        // Inside a frame that scrolls on its own (table, code, diff), or clipped by an ancestor: not the page's.
        for (let parent = element.parentElement; parent !== null; parent = parent.parentElement) {
          const style = getComputedStyle(parent);
          if (style.overflowX !== "visible" && parent.getBoundingClientRect().right <= width + 1) {
            return false;
          }
        }
        return true;
      })
      .slice(0, 5)
      .map((element) => `${element.tagName.toLowerCase()}.${element.className}`);
    return { scrollWidth: document.documentElement.scrollWidth, width, culprits };
  });
  expect(overflow.culprits, `${screen}: elements wider than the screen`).toEqual([]);
  expect(overflow.scrollWidth, `${screen}: document width`).toBeLessThanOrEqual(overflow.width);
}

/**
 * F18.R4: every visible button, link of a navigation, text field and tab offers a 44 × 44 px touch target. The
 * target is the box of the element, or of its label when the element is a checkbox or radio wrapped in one. Links
 * inside running text are left out (WCAG 2.5.8 exception for inline targets).
 */
export async function expectTouchTargets(page: Page, screen: string, minimum = 44): Promise<void> {
  const small = await page.evaluate((min) => {
    const selector = [
      "button",
      "[role=button]",
      "[role=tab]",
      "[role=menuitem]",
      "[role=treeitem] > *",
      "nav a",
      "input:not([type=hidden])",
      "select",
      "textarea",
    ].join(",");
    const visible = (element: Element) => {
      const box = element.getBoundingClientRect();
      const style = getComputedStyle(element);
      return box.width > 0 && box.height > 0 && style.visibility !== "hidden";
    };
    return [...document.querySelectorAll(selector)]
      .filter((element) => visible(element) && element.closest("[aria-hidden=true]") === null)
      .map((element) => {
        const target =
          element instanceof HTMLInputElement &&
          (element.type === "checkbox" || element.type === "radio")
            ? (element.closest("label") ?? element)
            : element;
        const box = target.getBoundingClientRect();
        return {
          name:
            element.getAttribute("aria-label") ??
            (element.textContent.trim().slice(0, 30) || element.tagName),
          width: Math.round(box.width),
          height: Math.round(box.height),
        };
      })
      .filter((target) => target.width < min || target.height < min);
  }, minimum);
  expect(small, `${screen}: touch targets under ${String(minimum)} px`).toEqual([]);
}
