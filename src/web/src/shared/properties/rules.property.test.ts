import fc from "fast-check";
import { describe, expect, it } from "vitest";
import { evaluateRules } from "./rules";
import { parsePropertyType, parseValue, type PropertyType } from "./types";

// docs/testing.md §4.1 (Règles de validation), twin of RulePropertyTests.cs.

const float: PropertyType = { scalar: "float", list: false };
const quarter = fc.integer({ min: -1000, max: 1000 }).map((i) => i / 4);

describe("rule properties", () => {
  it("between(a, b) ⇔ greater_or_equal(a) ∧ less_or_equal(b)", () => {
    fc.assert(
      fc.property(quarter, quarter, quarter, (a, b, x) => {
        const [min, max] = a <= b ? [a, b] : [b, a];
        const between =
          evaluateRules(float, [{ op: "between", value: [min, max] }], x).length === 0;
        const both =
          evaluateRules(
            float,
            [
              { op: "greater_or_equal", value: min },
              { op: "less_or_equal", value: max },
            ],
            x,
          ).length === 0;
        expect(between).toBe(both);
      }),
    );
  });

  it("a valid value survives a JSON round trip", () => {
    const samples = fc.oneof(
      fc.boolean().map((v) => ["bool", v] as const),
      fc.maxSafeInteger().map((v) => ["int", v] as const),
      fc
        .double({ noNaN: true, noDefaultInfinity: true })
        .map((v) => ["float", Object.is(v, -0) ? 0 : v] as const),
      fc.string({ maxLength: 50 }).map((v) => ["text", v.normalize("NFC")] as const),
      fc
        .array(fc.integer({ min: -100, max: 100 }), { maxLength: 5 })
        .map((v) => ["list<int>", v] as const),
    );
    fc.assert(
      fc.property(samples, ([typeName, value]) => {
        const type = parsePropertyType(typeName);
        expect(type).not.toBeNull();
        if (type === null) {
          return;
        }
        const parsed = parseValue(JSON.parse(JSON.stringify(value)), type);
        expect(parsed).toEqual({ ok: true, value });
      }),
    );
  });

  it("a value within the rules stays valid after a JSON round trip", () => {
    const int: PropertyType = { scalar: "int", list: false };
    fc.assert(
      fc.property(fc.integer({ min: 1, max: 60 }), (i) => {
        const parsed = parseValue(JSON.parse(JSON.stringify(i)), int);
        expect(parsed.ok).toBe(true);
        if (parsed.ok) {
          expect(
            evaluateRules(
              int,
              [
                { op: "greater_than", value: 0 },
                { op: "less_or_equal", value: 60 },
              ],
              parsed.value,
            ),
          ).toEqual([]);
        }
      }),
    );
  });
});
