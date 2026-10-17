import { expect, test } from "../fixtures";
import { expectNoSeriousViolations } from "../support/a11y";
import { screens, settle, useLocale, type Locale } from "../support/screens";

// E2E-17-04 (docs/testing.md §9.3): every screen of the mockup, light and dark, without a serious axe-core violation.
// The screenshots (@visual) run in the fixed container only: `pnpm e2e:visual:docker`.
test.describe("F17 · Accessibility and visual regressions of the screens", () => {
  for (const screen of screens) {
    for (const theme of ["light", "dark"] as const) {
      test(`E2E-17-04 · ${screen.name} (${theme}) has no serious violation @a11y @nightly`, async ({
        page,
        reference,
      }) => {
        await page.emulateMedia({ colorScheme: theme });
        await screen.open(page, reference);

        await expectNoSeriousViolations(page);
      });
    }
  }

  // The pseudo-locale (+40 % of text) shows overflows and untranslated strings that French and English hide.
  for (const screen of screens) {
    for (const theme of ["light", "dark"] as const) {
      for (const locale of ["fr", "en", "xx"] as const satisfies readonly Locale[]) {
        test(`visual · ${screen.name} (${theme}, ${locale}) @visual @nightly`, async ({
          page,
          reference,
        }) => {
          await page.emulateMedia({ colorScheme: theme, reducedMotion: "reduce" });
          await useLocale(page, locale);
          await screen.open(page, reference);
          await settle(page);

          await expect(page).toHaveScreenshot(`${screen.name}-${theme}-${locale}.png`);
        });
      }
    }
  }
});
