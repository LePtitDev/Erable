import { parseRule, type RuleOp, type ValidationRule } from "../../shared/properties/rules";
import type { EnumOption, PropertyType } from "../../shared/properties/types";

/**
 * A rule being edited: the fields hold what the user typed (text), so a half-typed rule can exist in the form. It
 * becomes a `ValidationRule` only when it parses (`ruleOf`), exactly like the backend reads it.
 */
export interface RuleDraft {
  /** Identity of the line in the form (not stored). */
  readonly uid: number;
  readonly op: RuleOp;
  /** Number, text, date, kind or category ID, depending on the operator. */
  readonly value: string;
  /** Upper bound of `between`. */
  readonly value2: string;
  /** Option keys of `in`. */
  readonly keys: readonly string[];
}

let nextUid = 1;

export function newRuleDraft(op: RuleOp, options: readonly EnumOption[] = []): RuleDraft {
  return {
    uid: nextUid++,
    op,
    value: op === "kind" ? "page" : "",
    value2: "",
    keys: op === "in" ? options.map((o) => o.key) : [],
  };
}

export function draftOfRule(rule: ValidationRule): RuleDraft {
  const base = { uid: nextUid++, op: rule.op, value: "", value2: "", keys: [] as string[] };
  switch (rule.op) {
    case "not_empty":
      return base;
    case "between":
      return { ...base, value: String(rule.value[0]), value2: String(rule.value[1]) };
    case "in":
      return { ...base, keys: [...rule.value] };
    default:
      return { ...base, value: String(rule.value) };
  }
}

function numberOf(text: string): number | null {
  const trimmed = text.trim().replace(",", ".");
  if (trimmed === "") return null;
  const n = Number(trimmed);
  return Number.isFinite(n) ? n : null;
}

/** The rule a draft stands for, or null while it is incomplete or refused (invalid pattern, bound order…). */
export function ruleOf(
  draft: RuleDraft,
  type: PropertyType,
  options: readonly EnumOption[],
): ValidationRule | null {
  const json = (value?: unknown) => ({ op: draft.op, ...(value === undefined ? {} : { value }) });
  switch (draft.op) {
    case "not_empty":
      return parseRule(json(), type, options);
    case "between": {
      const min = numberOf(draft.value);
      const max = numberOf(draft.value2);
      return min === null || max === null ? null : parseRule(json([min, max]), type, options);
    }
    case "in":
      return parseRule(json(draft.keys), type, options);
    case "match":
    case "after":
    case "before":
    case "kind":
    case "in_category":
      return parseRule(json(draft.value), type, options);
    default: {
      const n = numberOf(draft.value);
      return n === null ? null : parseRule(json(n), type, options);
    }
  }
}

/** Drafts that stay valid when the type changes: the others are dropped (F08.R2). */
export function keepApplicable(
  drafts: readonly RuleDraft[],
  available: readonly RuleOp[],
): RuleDraft[] {
  return drafts.filter((d) => available.includes(d.op));
}
