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

  test("a11y · Project shell on a folder has no serious violation @pr", async ({
    page,
    reference,
  }) => {
    await page.goto(reference.url("0000000000000001"));
    await expect(page.getByRole("heading", { level: 1, name: "Pages" })).toBeVisible();

    await expectNoSeriousViolations(page);
  });
});
