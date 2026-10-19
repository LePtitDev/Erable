import { expect, type Page } from "@playwright/test";
import { chroniques } from "../fixtures/chroniques";
import type { ReferenceProject } from "../fixtures";
import { t } from "./i18n";

/** The six screens of the mockup (CLAUDE.md §1), each with the way to reach it and a sign that it is displayed. */
export interface Screen {
  readonly name: string;
  open(page: Page, reference: ReferenceProject): Promise<void>;
}

export const screens: readonly Screen[] = [
  {
    name: "home",
    async open(page) {
      await page.goto("/");
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    },
  },
  {
    name: "page-read",
    async open(page, reference) {
      await page.goto(reference.url(chroniques.ilsa));
      // On a phone the table of contents is folded in the page panel (F18).
      await expect(
        page
          .getByRole("navigation", { name: t("page.toc") })
          .or(page.getByRole("button", { name: t("page.sidePanel") })),
      ).toBeVisible();
    },
  },
  {
    name: "page-edit",
    async open(page, reference) {
      await page.goto(`${reference.url(chroniques.ilsa)}?edit=true`);
      await expect(page.getByRole("textbox", { name: t("editor.content") })).toBeVisible();
    },
  },
  {
    name: "category",
    async open(page, reference) {
      await page.goto(reference.url(chroniques.pnj));
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    },
  },
  {
    name: "property",
    async open(page, reference) {
      await page.goto(reference.url(chroniques.niveau));
      await expect(page.getByRole("heading", { level: 1 })).toBeVisible();
    },
  },
  {
    name: "search",
    async open(page, reference) {
      await page.goto(`/p/${reference.id}/search?q=col`);
      await expect(page.getByRole("searchbox", { name: t("search.field") })).toBeVisible();
    },
  },
];

export type Locale = "fr" | "en" | "xx";

/**
 * Sets the interface language before the next navigation: a saved preference for the real languages, a session flag
 * for the pseudo-locale (the backend only keeps real languages; the test build knows `xx`).
 */
export async function useLocale(page: Page, locale: Locale) {
  if (locale === "xx") {
    await page.addInitScript(() => {
      window.sessionStorage.setItem("erable:locale", "xx");
    });
    return;
  }
  const current = (await (await page.request.get("/api/v1/me/preferences")).json()) as object;
  await page.request.put("/api/v1/me/preferences", { data: { ...current, language: locale } });
}

/** Fonts loaded and nothing moving: the page is ready to be photographed. */
export async function settle(page: Page) {
  await page.evaluate(() => document.fonts.ready);
}
