import fc from "fast-check";
import { describe, expect, it } from "vitest";
import { loadFixtures } from "../../test/fixtures";
import { formatDisplay, parseElementId, requireElementId } from "./elementId";

interface IdCase {
  readonly input: string;
  readonly valid: boolean;
  readonly canonical?: string;
  readonly display?: string;
}

const nonZero64 = fc.bigInt({ min: 1n, max: (1n << 64n) - 1n });

describe("parseElementId (spec/fixtures/ids)", () => {
  it.each(loadFixtures<IdCase>("ids"))("$file › $name", (c) => {
    const id = parseElementId(c.input);

    if (c.valid) {
      expect(id).toBe(c.canonical);
      expect(id === null ? null : formatDisplay(id)).toBe(c.display);
    } else {
      expect(id).toBeNull();
    }
  });
});

describe("parseElementId properties", () => {
  it("round-trips the canonical form of any non-zero 64-bit value", () => {
    fc.assert(
      fc.property(nonZero64, (value) => {
        const canonical = value.toString(16).toUpperCase().padStart(16, "0");
        expect(parseElementId(canonical)).toBe(canonical);
        expect(parseElementId(canonical.toLowerCase())).toBe(canonical);
        // The grouped form is for display only.
        expect(parseElementId(formatDisplay(requireElementId(canonical)))).toBeNull();
      }),
    );
  });

  it("rejects every string whose length is not 16", () => {
    fc.assert(
      fc.property(
        fc.string().filter((s) => s.length !== 16),
        (s) => {
          expect(parseElementId(s)).toBeNull();
        },
      ),
    );
  });
});

describe("requireElementId", () => {
  it("throws on an invalid value", () => {
    expect(() => requireElementId("0000000000000000")).toThrow();
  });
});
