import { expect, test } from "../fixtures";

interface Manifest {
  name: string;
  start_url: string;
  display: string;
  theme_color: string;
  icons: { src: string; sizes: string; type: string; purpose?: string }[];
}

test.describe("F18 · Mobile version", () => {
  test("E2E-18-11 · Installable web application @mobile", async ({ page, freshProfile }) => {
    expect(freshProfile.slot).not.toBe("");
    await page.goto("/");

    const href = await page.locator('link[rel="manifest"]').getAttribute("href");
    expect(href).not.toBeNull();
    const response = await page.request.get(href ?? "");
    expect(response.ok()).toBe(true);
    expect(response.headers()["content-type"]).toContain("application/manifest+json");
    const manifest = (await response.json()) as Manifest;
    expect(manifest.name).toBe("Erable");
    expect(manifest.start_url).toBe("/");
    expect(manifest.display).toBe("standalone");
    expect(manifest.theme_color).toMatch(/^#[0-9a-f]{6}$/i);

    for (const size of ["192x192", "512x512"]) {
      const icon = manifest.icons.find((i) => i.sizes === size && i.type === "image/png");
      expect(icon, `icon ${size}`).toBeDefined();
      const image = await page.request.get(icon?.src ?? "");
      expect(image.ok()).toBe(true);
      expect(image.headers()["content-type"]).toBe("image/png");
    }
    expect(manifest.icons.some((i) => i.purpose === "maskable")).toBe(true);
    await expect(page.locator('meta[name="theme-color"]')).toHaveCount(2);
  });
});
