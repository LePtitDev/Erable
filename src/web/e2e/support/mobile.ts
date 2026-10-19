import { expect, type Page } from "@playwright/test";

/**
 * F18.R3: the document is never wider than the screen, and nothing scrolls sideways (the work area included) except
 * the wide blocks meant to: those carry `data-scroll-x` (tables, code, diff…). CodeMirror's own scroller counts as
 * one. Hidden overflow is reported too when it cuts content off at the right edge of the screen. Soft assertions: one
 * run lists every screen at fault.
 */
export async function expectNoHorizontalScroll(page: Page, screen: string): Promise<void> {
  const overflow = await page.evaluate(() => {
    const width = document.documentElement.clientWidth;
    const describe = (element: Element) =>
      `${element.tagName.toLowerCase()}.${element.getAttribute("class") ?? ""}`;
    const allowed = (element: Element) => element.closest("[data-scroll-x], .cm-scroller") !== null;
    const scrolling = [...document.querySelectorAll<HTMLElement>("body *")].filter((element) => {
      if (allowed(element)) return false;
      const style = getComputedStyle(element);
      return (
        (style.overflowX === "auto" || style.overflowX === "scroll") &&
        element.scrollWidth > element.clientWidth + 1
      );
    });
    const outside = [...document.querySelectorAll<HTMLElement>("body *")].filter((element) => {
      if (allowed(element)) return false;
      const box = element.getBoundingClientRect();
      return box.width > 0 && box.height > 0 && box.right > width + 1 && box.left < width;
    });
    return {
      scrollWidth: document.documentElement.scrollWidth,
      width,
      scrolling: scrolling.slice(0, 5).map(describe),
      outside: outside.slice(0, 5).map(describe),
    };
  });
  expect.soft(overflow.scrolling, `${screen}: frames scrolling sideways`).toEqual([]);
  expect.soft(overflow.outside, `${screen}: elements past the right edge`).toEqual([]);
  expect
    .soft(overflow.scrollWidth, `${screen}: document width`)
    .toBeLessThanOrEqual(overflow.width);
}

/**
 * F18.R4: every visible button, link of a navigation, tree line, text field and tab offers a 44 × 44 px touch target.
 * The target is the box of the element, or of its label when the field is wrapped in one. Links
 * inside running text are left out (WCAG 2.5.8 exception for inline targets).
 */
export async function expectTouchTargets(page: Page, screen: string, minimum = 44): Promise<void> {
  const small = await page.evaluate((min) => {
    const selector = [
      "button",
      "[role=button]",
      "[role=tab]",
      "[role=menuitem]",
      "[role=treeitem]",
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
        // A field wrapped in its label is reached through the whole label.
        const target = element.matches("input, select, textarea")
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
  expect.soft(small, `${screen}: touch targets under ${String(minimum)} px`).toEqual([]);
}
