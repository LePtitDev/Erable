import { describe, expect, it } from "vitest";
import { createFormatters } from "./format";

const now = new Date("2026-10-13T08:00:00Z");
const clock = () => now;

/** Intl uses (narrow) no-break spaces; compare with plain spaces. */
const plain = (text: string) => text.replace(/\s/g, " ");

describe("createFormatters", () => {
  it("formats relative times in the interface language", () => {
    const twoHoursAgo = new Date(now.getTime() - 2 * 3600 * 1000);
    expect(plain(createFormatters("fr", clock).relative(twoHoursAgo))).toBe("il y a 2 h");
    expect(createFormatters("en", clock).relative(twoHoursAgo)).toBe("2 hr. ago");
  });

  it("uses words for yesterday", () => {
    const yesterday = new Date(now.getTime() - 24 * 3600 * 1000);
    expect(createFormatters("fr", clock).relative(yesterday)).toBe("hier");
  });

  it("formats long dates", () => {
    const date = new Date(Date.UTC(2026, 2, 12, 12));
    expect(createFormatters("fr", clock).date(date)).toBe("12 mars 2026");
    expect(createFormatters("de", clock).date(date)).toBe("12. März 2026");
  });

  it("formats numbers with the language separators", () => {
    expect(plain(createFormatters("fr", clock).number(1280.5))).toBe("1 280,5");
    expect(createFormatters("en", clock).number(1280)).toBe("1,280");
  });

  it("formats the pseudo-locale like French", () => {
    expect(createFormatters("xx", clock).number(840.5)).toBe("840,5");
  });
});
