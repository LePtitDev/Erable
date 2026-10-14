import { expect, test } from "../fixtures";
import { expectNoSeriousViolations } from "../support/a11y";

// Screen-level axe-core checks added by each M1 screen task; the full matrix (themes × languages) is E2E-17-04 (M5).
test.describe("a11y · axe-core per screen", () => {
  for (const theme of ["light", "dark"] as const) {
    test(`a11y · Home screen (${theme}) has no serious violation @pr`, async ({
      home,
      page,
      freshProfile,
    }) => {
      expect(freshProfile.slot).not.toBe("");
      await page.emulateMedia({ colorScheme: theme });
      await home.goto();
      await expect(home.greeting).toBeVisible();

      await expectNoSeriousViolations(page);
    });
  }
});
