import {
  test as base,
  type APIRequestContext,
  type BrowserContext,
  type Page,
} from "@playwright/test";
import { HomePage } from "../pages/HomePage";
import { referenceDate } from "./chroniques";

/** A fresh copy of a reference project, private to the running test (POST /_test/reset). */
export interface ReferenceProject {
  readonly id: string;
  readonly name: string;
  readonly folder: string;
  /** The slot of the test: its profile and its project copy (second browser contexts join it). */
  readonly slot: string;
  /** « Autre projet », of which only « externe » is a member (E2E-14-04); set with the `otherProject` option. */
  readonly otherProjectId: string | null;
  /** URL of an element of this project. */
  url(elementId: string, anchor?: string): string;
}

interface ResetResult {
  projectId: string | null;
  name: string | null;
  folder: string | null;
  otherProjectId: string | null;
}

/** Who the test is signed in as, and what else `/_test/reset` prepares. */
export interface ResetOptions {
  /** A user of spec/projects/users.json; empty to stay anonymous (the login screen). Default: the administrator. */
  user?: string;
  /** Also create « Autre projet », of which « externe » is the administrator. */
  otherProject?: boolean;
}

/** Slot = one test: its own project copy and its own profile, so tests run in parallel against one backend. */
function slotOf(testId: string, browser: string): string {
  return `${browser}-${testId}`.replace(/[^A-Za-z0-9_-]/g, "").slice(0, 64);
}

export async function resetProject(
  request: APIRequestContext,
  slot: string,
  fixture = "chroniques",
  options: ResetOptions = {},
): Promise<ResetResult> {
  const query = new URLSearchParams({ slot, fixture });
  if (options.user !== undefined) query.set("user", options.user);
  if (options.otherProject === true) query.set("other", "true");
  const response = await request.post(`/_test/reset?${query.toString()}`);
  if (!response.ok()) {
    throw new Error(`/_test/reset failed: ${String(response.status())} ${await response.text()}`);
  }
  return (await response.json()) as ResetResult;
}

interface Options {
  /** Signed in as this user of the reference project (default `admin`); empty: anonymous. */
  user: string;
  /** Create the second project of « externe » too. */
  otherProject: boolean;
  /**
   * Freeze the browser clock at the reference date (default). Performance tests turn it off: the fake clock also
   * replaces `performance`, whose marks then record nothing.
   */
  fakeClock: boolean;
}

interface Fixtures {
  home: HomePage;
  /** « Chroniques d'Aube », freshly copied; the browser clock is set to the reference date. */
  reference: ReferenceProject;
  /** No project, but a fresh profile (empty recent list, default preferences); gives the slot used. */
  freshProfile: { readonly slot: string };
  /** Another browser context of the same test, signed in as this user (@multi scenarios); closed with the test. */
  openAs: (user: string) => Promise<Page>;
}

export const test = base.extend<Fixtures & Options>({
  fakeClock: [true, { option: true }],
  user: ["admin", { option: true }],
  otherProject: [false, { option: true }],
  page: async ({ page, fakeClock }, use) => {
    // Dates shown as « il y a 2 h » are computed in the browser: freeze its clock like the backend one.
    if (fakeClock) await page.clock.install({ time: referenceDate });
    await page.request.post("/_test/clock", { data: { now: null } });
    await use(page);
  },
  home: async ({ page }, use) => {
    await use(new HomePage(page));
  },
  reference: async ({ page, browserName, user, otherProject }, use, testInfo) => {
    const slot = slotOf(testInfo.testId, browserName);
    const result = await resetProject(page.request, slot, "chroniques", { user, otherProject });
    if (result.projectId === null || result.name === null || result.folder === null) {
      throw new Error("/_test/reset did not open a project.");
    }
    const id = result.projectId;
    await use({
      id,
      name: result.name,
      folder: result.folder,
      slot,
      otherProjectId: result.otherProjectId,
      url: (elementId, anchor) => `/p/${id}/e/${elementId}${anchor ? `#${anchor}` : ""}`,
    });
    // `erable doctor` after each test: whatever the scenario did, the project must stay sound (docs/testing.md §6).
    const doctor = await page.request.get(`/_test/doctor?projectId=${id}`);
    if (doctor.status() === 404) return; // the test closed its copy on purpose
    const report = (await doctor.json()) as { healthy: boolean; findings: unknown[] };
    if (!report.healthy) {
      throw new Error(`erable doctor found anomalies: ${JSON.stringify(report.findings)}`);
    }
  },
  freshProfile: async ({ page, browserName, user }, use, testInfo) => {
    const slot = slotOf(testInfo.testId, browserName);
    await resetProject(page.request, slot, "none", { user });
    await use({ slot });
  },
  openAs: async ({ browser, baseURL, fakeClock, reference }, use) => {
    const contexts: BrowserContext[] = [];
    await use(async (user) => {
      const context = await browser.newContext({
        ...(baseURL === undefined ? {} : { baseURL }),
        locale: "fr-FR",
        timezoneId: "Europe/Paris",
      });
      contexts.push(context);
      const login = await context.request.post(
        `/_test/login-as/${encodeURIComponent(user)}?slot=${encodeURIComponent(reference.slot)}`,
      );
      if (!login.ok()) throw new Error(`/_test/login-as failed: ${String(login.status())}`);
      const other = await context.newPage();
      if (fakeClock) await other.clock.install({ time: referenceDate });
      return other;
    });
    for (const context of contexts) await context.close();
  },
});

export { expect } from "@playwright/test";
