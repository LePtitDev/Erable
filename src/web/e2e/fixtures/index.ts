import { test as base } from "@playwright/test";
import { HelloPage } from "../pages/HelloPage";

/** Page objects injected as fixtures: `test("…", async ({ hello }) => …)`. */
export const test = base.extend<{ hello: HelloPage }>({
  hello: async ({ page }, use) => {
    await use(new HelloPage(page));
  },
});

export { expect } from "@playwright/test";
