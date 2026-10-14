import { describe, expect, it } from "vitest";
import { pickLanguage } from "./languages";

describe("pickLanguage", () => {
  it("returns the first supported base language", () => {
    expect(pickLanguage(["ja", "de-AT", "en"])).toBe("de");
  });

  it("falls back to French", () => {
    expect(pickLanguage(["ja", undefined])).toBe("fr");
  });
});
