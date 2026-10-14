import { test as base, type APIRequestContext } from "@playwright/test";
import { HomePage } from "../pages/HomePage";
import { referenceDate } from "./chroniques";

/** A fresh copy of a reference project, private to the running test (POST /_test/reset). */
export interface ReferenceProject {
  readonly id: string;
  readonly name: string;
  readonly folder: string;
  /** URL of an element of this project. */
  url(elementId: string, anchor?: string): string;
}

interface ResetResult {
  projectId: string | null;
  name: string | null;
  folder: string | null;
}

/** Slot = one test: its own project copy and its own profile, so tests run in parallel against one backend. */
function slotOf(testId: string, browser: string): string {
  return `${browser}-${testId}`.replace(/[^A-Za-z0-9_-]/g, "").slice(0, 64);
}

export async function resetProject(
  request: APIRequestContext,
  slot: string,
  fixture = "chroniques",
): Promise<ResetResult> {
  const response = await request.post(
    `/_test/reset?slot=${encodeURIComponent(slot)}&fixture=${fixture}`,
  );
  if (!response.ok()) {
    throw new Error(`/_test/reset failed: ${String(response.status())} ${await response.text()}`);
  }
  return (await response.json()) as ResetResult;
}

interface Options {
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
}

export const test = base.extend<Fixtures & Options>({
  fakeClock: [true, { option: true }],
  page: async ({ page, fakeClock }, use) => {
    // Dates shown as « il y a 2 h » are computed in the browser: freeze its clock like the backend one.
    if (fakeClock) await page.clock.install({ time: referenceDate });
    await page.request.post("/_test/clock", { data: { now: null } });
    await use(page);
  },
  home: async ({ page }, use) => {
    await use(new HomePage(page));
  },
  reference: async ({ page, browserName }, use, testInfo) => {
    const result = await resetProject(page.request, slotOf(testInfo.testId, browserName));
    if (result.projectId === null || result.name === null || result.folder === null) {
      throw new Error("/_test/reset did not open a project.");
    }
    const id = result.projectId;
    await use({
      id,
      name: result.name,
      folder: result.folder,
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
  freshProfile: async ({ page, browserName }, use, testInfo) => {
    const slot = slotOf(testInfo.testId, browserName);
    await resetProject(page.request, slot, "none");
    await use({ slot });
  },
});

export { expect } from "@playwright/test";
