import { describe, expect, it } from "vitest";
import { loadFixtures } from "../../test/fixtures";
import { requireElementId, type ElementId } from "../ids/elementId";
import { evaluateRules, parseRule, parseRules, type RuleContext } from "./rules";
import { keyFromName, uniqueKey } from "./keys";
import { parseSafeRegex, safeRegexTest } from "./safeRegex";
import { parsePropertyType, parseValue, type EnumOption, type PropertyType } from "./types";

// spec/fixtures/rules, shared with tests/Erable.Core.Tests/Properties/RuleFixtureTests.cs.

interface EvaluationCase {
  readonly type: string;
  readonly options?: readonly EnumOption[];
  readonly rules: unknown;
  readonly value: unknown;
  readonly context?: {
    readonly kinds?: Readonly<Record<string, "page" | "category">>;
    readonly inCategory?: readonly (readonly [string, string])[];
  };
  readonly violations: readonly { readonly op: string; readonly index?: number }[];
}

interface DefinitionCase {
  readonly type: string;
  readonly options?: readonly EnumOption[];
  readonly rule: unknown;
  readonly valid: boolean;
  readonly canonical?: unknown;
}

interface ValueCase {
  readonly type: string;
  readonly options?: readonly EnumOption[];
  readonly json: unknown;
  readonly valid: boolean;
  readonly canonical?: unknown;
}

interface RegexCase {
  readonly pattern: string;
  readonly valid: boolean;
  readonly javascript?: string;
  readonly matches?: readonly (readonly [string, boolean])[];
}

interface KeyCase {
  readonly label: string;
  readonly taken: readonly string[];
  readonly key: string;
}

const rules = <T>(file: string) =>
  loadFixtures<T & { readonly file: string }>("rules").filter((c) => c.file === file);

function typeOf(text: string): PropertyType {
  const type = parsePropertyType(text);
  if (type === null) {
    throw new Error(`Unknown type ${text}`);
  }
  return type;
}

function contextOf(c: EvaluationCase["context"]): RuleContext | undefined {
  if (c === undefined) {
    return undefined;
  }
  const memberships = new Set(
    (c.inCategory ?? []).map(([page, category]) => `${page}/${category}`),
  );
  return {
    kindOf: (id: ElementId) => c.kinds?.[id] ?? null,
    isInCategory: (page: ElementId, category: ElementId) => memberships.has(`${page}/${category}`),
  };
}

describe("evaluation (evaluation.json)", () => {
  it.each(rules<EvaluationCase>("evaluation.json"))("$file › $name", (c) => {
    const type = typeOf(c.type);
    const parsedRules = parseRules(c.rules, type, c.options);
    const value = parseValue(c.value, type, c.options);
    expect(parsedRules).not.toBeNull();
    expect(value.ok).toBe(true);
    if (parsedRules === null || !value.ok) {
      return;
    }

    const actual = evaluateRules(type, parsedRules, value.value, contextOf(c.context)).map((v) =>
      v.index === undefined ? { op: v.rule.op } : { op: v.rule.op, index: v.index },
    );

    expect(actual).toEqual(c.violations);
  });
});

describe("rule definitions (definitions.json)", () => {
  it.each(rules<DefinitionCase>("definitions.json"))("$file › $name", (c) => {
    const rule = parseRule(c.rule, typeOf(c.type), c.options);

    expect(rule !== null).toBe(c.valid);
    if (rule !== null) {
      expect(rule).toEqual(c.canonical);
    }
  });
});

describe("values (values.json)", () => {
  it.each(rules<ValueCase>("values.json"))("$file › $name", (c) => {
    const value = parseValue(c.json, typeOf(c.type), c.options);

    expect(value.ok).toBe(c.valid);
    if (value.ok) {
      expect(value.value).toEqual(c.canonical);
    }
  });
});

describe("keys (keys.json)", () => {
  it.each(rules<KeyCase>("keys.json"))("$file › $name", (c) => {
    expect(uniqueKey(keyFromName(c.label), c.taken)).toBe(c.key);
  });
});

describe("safe regular expressions (regex.json)", () => {
  it.each(rules<RegexCase>("regex.json"))("$file › $name", (c) => {
    const parsed = parseSafeRegex(c.pattern);

    expect(parsed.ok).toBe(c.valid);
    if (parsed.ok) {
      expect(parsed.javaScript).toBe(c.javascript);
      for (const [value, found] of c.matches ?? []) {
        expect(safeRegexTest(c.pattern, value), JSON.stringify(value)).toBe(found);
      }
    }
  });

  it("never matches with an invalid pattern", () => {
    expect(safeRegexTest("(?=a)", "a")).toBe(false);
  });

  it("refuses an ID-less in_category rule", () => {
    expect(
      parseRule({ op: "in_category", value: requireElementId("2000000000000001") }, typeOf("ref")),
    ).toEqual({
      op: "in_category",
      value: "2000000000000001",
    });
  });
});
