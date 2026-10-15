import { parseElementId, type ElementId } from "../ids/elementId";
import { parseSafeRegex, safeRegexTest } from "./safeRegex";
import {
  isIsoDate,
  type EnumOption,
  type PropertyType,
  type PropertyValue,
  type ScalarValue,
} from "./types";

/**
 * Validation rules as data (docs/architecture.md §5.4), twin of Erable.Core/Properties/ValidationRule.cs and
 * RuleEvaluator.cs: the backend is the authority, this copy gives instant feedback in the editor (F05.R6). Both are
 * checked by spec/fixtures/rules. A rule object is its own canonical JSON form.
 */

export type NumberOp =
  "greater_than" | "greater_or_equal" | "less_than" | "less_or_equal" | "not_equal";
export type LengthOp = "min_length" | "max_length" | "min_count" | "max_count";

export type ValidationRule =
  | { readonly op: NumberOp; readonly value: number }
  | { readonly op: "between"; readonly value: readonly [number, number] }
  | { readonly op: LengthOp; readonly value: number }
  | { readonly op: "match"; readonly value: string }
  | { readonly op: "not_empty" }
  | { readonly op: "in"; readonly value: readonly string[] }
  | { readonly op: "after" | "before"; readonly value: string }
  | { readonly op: "kind"; readonly value: "page" | "category" }
  | { readonly op: "in_category"; readonly value: ElementId };

export type RuleOp = ValidationRule["op"];

/** A rule the value does not satisfy; `index` for an item rule of a list. */
export interface RuleViolation {
  readonly rule: ValidationRule;
  readonly index?: number;
}

/** What the `kind` and `in_category` rules need to know about the project. */
export interface RuleContext {
  kindOf(id: ElementId): "page" | "category" | "other" | null;
  isInCategory(pageId: ElementId, categoryId: ElementId): boolean;
}

const numberOps: readonly RuleOp[] = [
  "greater_than",
  "greater_or_equal",
  "less_than",
  "less_or_equal",
  "between",
  "not_equal",
];

/** Operators available for a type, in the order the interface proposes them (none for `bool`, F08.R2). */
export function availableRules(type: PropertyType): readonly RuleOp[] {
  const item: readonly RuleOp[] = {
    bool: [],
    int: numberOps,
    float: numberOps,
    string: ["min_length", "max_length", "match", "not_empty"],
    text: ["max_length"],
    enum: ["in"],
    date: ["after", "before"],
    ref: ["kind", "in_category"],
  }[type.scalar] as readonly RuleOp[];
  return type.list ? ["min_count", "max_count", ...item] : item;
}

export function isCountRule(rule: ValidationRule): boolean {
  return rule.op === "min_count" || rule.op === "max_count";
}

function finiteNumber(value: unknown): number | null {
  return typeof value === "number" && Number.isFinite(value) ? value : null;
}

/** Reads a rule for a property of `type`, exactly like `ValidationRules.FromJson`; `null` when refused. */
export function parseRule(
  json: unknown,
  type: PropertyType,
  options?: readonly EnumOption[],
): ValidationRule | null {
  if (typeof json !== "object" || json === null || Array.isArray(json)) {
    return null;
  }
  const { op, value } = json as { op?: unknown; value?: unknown };
  if (typeof op !== "string" || !(availableRules(type) as readonly string[]).includes(op)) {
    return null;
  }
  switch (op as RuleOp) {
    case "greater_than":
    case "greater_or_equal":
    case "less_than":
    case "less_or_equal":
    case "not_equal": {
      const n = finiteNumber(value);
      return n === null ? null : { op: op as NumberOp, value: n };
    }
    case "between": {
      if (!Array.isArray(value) || value.length !== 2) {
        return null;
      }
      const min = finiteNumber(value[0]);
      const max = finiteNumber(value[1]);
      return min === null || max === null || min > max
        ? null
        : { op: "between", value: [min, max] };
    }
    case "min_length":
    case "max_length":
    case "min_count":
    case "max_count": {
      const n = finiteNumber(value);
      return n === null || !Number.isInteger(n) || n < 0 || n > 2147483647
        ? null
        : { op: op as LengthOp, value: n };
    }
    case "match":
      return typeof value === "string" && parseSafeRegex(value).ok ? { op: "match", value } : null;
    case "not_empty":
      return { op: "not_empty" };
    case "in": {
      if (!Array.isArray(value) || value.some((k) => typeof k !== "string")) {
        return null;
      }
      const keys = [...new Set(value as string[])];
      return options !== undefined && keys.some((k) => !options.some((o) => o.key === k))
        ? null
        : { op: "in", value: keys };
    }
    case "after":
    case "before":
      return typeof value === "string" && isIsoDate(value)
        ? { op: op as "after" | "before", value }
        : null;
    case "kind":
      return value === "page" || value === "category" ? { op: "kind", value } : null;
    case "in_category": {
      const id = typeof value === "string" ? parseElementId(value) : null;
      return id === null ? null : { op: "in_category", value: id };
    }
  }
}

/** Reads a list of rules; `null` when one of them is refused. */
export function parseRules(
  json: unknown,
  type: PropertyType,
  options?: readonly EnumOption[],
): ValidationRule[] | null {
  if (!Array.isArray(json)) {
    return null;
  }
  const rules: ValidationRule[] = [];
  for (const item of json as unknown[]) {
    const rule = parseRule(item, type, options);
    if (rule === null) {
      return null;
    }
    rules.push(rule);
  }
  return rules;
}

/** White space for `not_empty`: the ECMAScript WhiteSpace and LineTerminator sets, same list as the backend. */
const whiteSpace = new Set(
  [
    0x09, 0x0a, 0x0b, 0x0c, 0x0d, 0x20, 0xa0, 0x1680, 0x2000, 0x2001, 0x2002, 0x2003, 0x2004,
    0x2005, 0x2006, 0x2007, 0x2008, 0x2009, 0x200a, 0x2028, 0x2029, 0x202f, 0x205f, 0x3000, 0xfeff,
  ].map((c) => String.fromCharCode(c)),
);

/** Length in Unicode code points (a lone surrogate counts as one). */
export function codePointLength(text: string): number {
  return Array.from(text).length;
}

const emptyContext: RuleContext = { kindOf: () => null, isInCategory: () => false };

/**
 * Evaluates the rules of a property of `type` on a value. Violations come in rule order; for an item rule of a list,
 * in item order. Rules never block anything: they only warn (§5.4).
 */
export function evaluateRules(
  type: PropertyType,
  rules: readonly ValidationRule[],
  value: PropertyValue,
  context: RuleContext = emptyContext,
): RuleViolation[] {
  const violations: RuleViolation[] = [];
  for (const rule of rules) {
    if (Array.isArray(value)) {
      const items = value as readonly ScalarValue[];
      if (isCountRule(rule)) {
        if (!satisfiesCount(rule, items.length)) {
          violations.push({ rule });
        }
      } else {
        items.forEach((item, index) => {
          if (!satisfies(rule, type, item, context)) {
            violations.push({ rule, index });
          }
        });
      }
    } else if (!isCountRule(rule) && !satisfies(rule, type, value as ScalarValue, context)) {
      violations.push({ rule });
    }
  }
  return violations;
}

function satisfiesCount(rule: ValidationRule, count: number): boolean {
  if (rule.op === "min_count") {
    return count >= rule.value;
  }
  if (rule.op === "max_count") {
    return count <= rule.value;
  }
  return true;
}

/** A rule that does not apply to the kind of value (stale rule after a type change) is satisfied. */
function satisfies(
  rule: ValidationRule,
  type: PropertyType,
  value: ScalarValue,
  context: RuleContext,
): boolean {
  const scalar = type.scalar;
  const number =
    (scalar === "int" || scalar === "float") && typeof value === "number" ? value : null;
  const text =
    (scalar === "string" || scalar === "text") && typeof value === "string" ? value : null;
  switch (rule.op) {
    case "greater_than":
      return number === null || number > rule.value;
    case "greater_or_equal":
      return number === null || number >= rule.value;
    case "less_than":
      return number === null || number < rule.value;
    case "less_or_equal":
      return number === null || number <= rule.value;
    case "not_equal":
      return number === null || number !== rule.value;
    case "between":
      return number === null || (number >= rule.value[0] && number <= rule.value[1]);
    case "min_length":
      return text === null || codePointLength(text) >= rule.value;
    case "max_length":
      return text === null || codePointLength(text) <= rule.value;
    case "match":
      return text === null || safeRegexTest(rule.value, text);
    case "not_empty":
      return text === null || Array.from(text).some((c) => !whiteSpace.has(c));
    case "in":
      return scalar !== "enum" || typeof value !== "string" || rule.value.includes(value);
    case "after":
      // ISO dates compare as strings.
      return scalar !== "date" || typeof value !== "string" || value > rule.value;
    case "before":
      return scalar !== "date" || typeof value !== "string" || value < rule.value;
    case "kind":
    case "in_category": {
      const id = scalar === "ref" && typeof value === "string" ? parseElementId(value) : null;
      if (id === null) {
        return true;
      }
      return rule.op === "kind"
        ? context.kindOf(id) === rule.value
        : context.isInCategory(id, rule.value);
    }
    case "min_count":
    case "max_count":
      return true;
  }
}
