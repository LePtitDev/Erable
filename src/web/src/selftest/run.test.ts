import { describe, expect, it } from "vitest";
import { testI18n } from "../test/i18n";
import { http, server } from "../test/msw";
import { noLaunchOptions } from "../shared/api/session";
import { runSelfTest } from "./run";

describe("runSelfTest", () => {
  it("reports every step, stops at the first failure and posts the result", async () => {
    let body: unknown;
    server.use(
      http.post("/api/self-test/report", async ({ request, response }) => {
        body = await request.json();
        return response(204).empty();
      }),
    );

    await runSelfTest(testI18n, noLaunchOptions, [
      { name: "first", run: () => Promise.resolve("fine") },
      { name: "second", run: () => Promise.reject(new Error("broken")) },
      { name: "never", run: () => Promise.resolve(undefined) },
    ]);

    expect(body).toMatchObject({
      steps: [
        { name: "first", ok: true, detail: "fine" },
        { name: "second", ok: false, detail: "broken" },
      ],
    });
  });
});
