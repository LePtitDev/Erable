import { describe, expect, it } from "vitest";
import { availableRules } from "../../shared/properties/rules";
import { draftOfRule, keepApplicable, newRuleDraft, ruleOf } from "./ruleDrafts";

const int = { scalar: "int", list: false } as const;
const string = { scalar: "string", list: false } as const;
const enumType = { scalar: "enum", list: false } as const;
const options = [
  { key: "a", label: "A" },
  { key: "b", label: "B" },
];

describe("ruleOf", () => {
  it("reads a number typed with a comma", () => {
    const draft = { ...newRuleDraft("greater_than"), value: "0,5" };

    expect(ruleOf(draft, { scalar: "float", list: false }, [])).toEqual({
      op: "greater_than",
      value: 0.5,
    });
  });

  it("refuses an empty value", () => {
    expect(ruleOf(newRuleDraft("less_or_equal"), int, [])).toBeNull();
  });

  it("refuses a between whose bounds are reversed", () => {
    const draft = { ...newRuleDraft("between"), value: "10", value2: "1" };

    expect(ruleOf(draft, int, [])).toBeNull();
  });

  it("refuses a pattern that is not a safe regular expression", () => {
    expect(ruleOf({ ...newRuleDraft("match"), value: "(" }, string, [])).toBeNull();
    expect(ruleOf({ ...newRuleDraft("match"), value: "^[A-Z]{3}$" }, string, [])).toEqual({
      op: "match",
      value: "^[A-Z]{3}$",
    });
  });

  it("accepts not_empty with no value", () => {
    expect(ruleOf(newRuleDraft("not_empty"), string, [])).toEqual({ op: "not_empty" });
  });

  it("starts an « in » rule with every option allowed", () => {
    expect(ruleOf(newRuleDraft("in", options), enumType, options)).toEqual({
      op: "in",
      value: ["a", "b"],
    } satisfies unknown as never);
  });
});

describe("draftOfRule", () => {
  it("round-trips a rule", () => {
    const rule = { op: "between", value: [1, 60] } as const;

    expect(ruleOf(draftOfRule(rule), int, [])).toEqual(rule);
  });
});

describe("keepApplicable", () => {
  it("drops the rules the new type does not offer (F08.R2)", () => {
    const drafts = [newRuleDraft("greater_than"), newRuleDraft("less_or_equal")];

    expect(keepApplicable(drafts, availableRules({ scalar: "bool", list: false }))).toEqual([]);
    expect(keepApplicable(drafts, availableRules(int))).toHaveLength(2);
  });
});
