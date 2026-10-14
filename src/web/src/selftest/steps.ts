import type { i18n as I18n } from "i18next";
import { api, unwrap } from "../shared/api/client";

/**
 * Scripted desktop smoke journey (docs/testing.md §10, E2E-16-01). Each milestone appends steps here:
 * M1 adds "create a project, a page, a reference, an image, search".
 */
export interface SelfTestContext {
  readonly i18n: I18n;
  readonly document: Document;
  /** SPA clock when the home screen became visible (E2E-16-01: < 3 s). */
  readyAtMs?: number;
}

export interface SelfTestStepDefinition {
  readonly name: string;
  readonly run: (context: SelfTestContext) => Promise<string | undefined>;
}

const timeoutMs = 15_000;

/** Resolves once `predicate` holds, re-checked on every DOM change. */
export function waitFor(document: Document, predicate: () => boolean, what: string): Promise<void> {
  return new Promise((resolve, reject) => {
    if (predicate()) {
      resolve();
      return;
    }
    const observer = new MutationObserver(() => {
      if (predicate()) {
        observer.disconnect();
        clearTimeout(timer);
        resolve();
      }
    });
    const timer = setTimeout(() => {
      observer.disconnect();
      reject(new Error(`Timed out waiting for ${what}`));
    }, timeoutMs);
    observer.observe(document.body, { childList: true, subtree: true, characterData: true });
  });
}

export const selfTestSteps: readonly SelfTestStepDefinition[] = [
  {
    name: "api-authenticated",
    run: async () => {
      const info = await unwrap(api.GET("/api/info"));
      return `${info.appVersion} · ${info.mode}`;
    },
  },
  {
    name: "home-visible",
    run: async (context) => {
      const { document, i18n } = context;
      await waitFor(
        document,
        () =>
          document.body.textContent.includes(i18n.t("home.subtitle")) &&
          document.body.textContent.includes(i18n.t("app.mode.desktop")),
        "the home screen",
      );
      context.readyAtMs = performance.now();
      return `ready at ${Math.round(context.readyAtMs)} ms`;
    },
  },
];
