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
});
