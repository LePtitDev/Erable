import { describe, expect, it } from "vitest";
import { applyTheme, readInitialTheme } from "./theme";

describe("theme", () => {
  it("reads the preference injected by the backend", () => {
    const root = document.createElement("html");
    root.dataset["theme"] = "dark";
    expect(readInitialTheme(root)).toBe("dark");
  });

  it("falls back to system for a missing or unknown value", () => {
    const root = document.createElement("html");
    expect(readInitialTheme(root)).toBe("system");
    root.dataset["theme"] = "sepia";
    expect(readInitialTheme(root)).toBe("system");
  });

  it("applies a preference as the data-theme attribute", () => {
    const root = document.createElement("html");
    applyTheme("light", root);
    expect(root.getAttribute("data-theme")).toBe("light");
  });

  it("turns on the theme-color of a forced theme, and back to the system ones", () => {
    const light = document.createElement("meta");
    light.name = "theme-color";
    light.media = "(prefers-color-scheme: light)";
    const dark = document.createElement("meta");
    dark.name = "theme-color";
    dark.media = "(prefers-color-scheme: dark)";
    document.head.append(light, dark);
    try {
      applyTheme("dark", document.createElement("html"));
      expect([light.media, dark.media]).toEqual(["not all", "all"]);
      applyTheme("system", document.createElement("html"));
      expect([light.media, dark.media]).toEqual([
        "(prefers-color-scheme: light)",
        "(prefers-color-scheme: dark)",
      ]);
    } finally {
      light.remove();
      dark.remove();
    }
  });
});
