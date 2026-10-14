import { expect, test } from "../fixtures";

// Infrastructure smoke test (no E2E scenario ID): the built SPA, served by the real backend, reaches the API.
// Feature scenarios are titled "E2E-nn-nn · <English title> @tags" (docs/features.md §1.3).
test("smoke · Hello page shows the backend version @pr", async ({ hello, request }) => {
  const info = (await (await request.get("/api/info")).json()) as { appVersion: string };

  await hello.goto();

  await expect(hello.heading).toHaveText("Erable");
  await expect(hello.version(info.appVersion, "server")).toBeVisible();
});

test("smoke · Test endpoints are available in test mode @pr", async ({ request }) => {
  const response = await request.get("/_test/ping");

  expect(response.status()).toBe(204);
});
