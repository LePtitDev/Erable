import { useQuery } from "@tanstack/react-query";
import { useCallback, useMemo } from "react";
import { useTranslation } from "react-i18next";
import { treeQuery } from "../../shared/api/tree";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import type { RuleViolation, ValidationRule } from "../../shared/properties/rules";
import type { PropertyValue, ScalarValue } from "../../shared/properties/types";
import type { Definition } from "./definitions";
import type { Candidate } from "./pageProperties";

/** Name of an element by ID, from the explorer tree already in cache (refs, `in_category`). */
export function useElementNames(projectId: ElementId): (id: ElementId) => string {
  const tree = useQuery(treeQuery(projectId));
  const names = useMemo(
    () => new Map((tree.data?.nodes ?? []).map((n) => [n.id, n.name] as const)),
    [tree.data],
  );
  return useCallback((id: ElementId) => names.get(id) ?? id, [names]);
}

function localDate(iso: string): Date {
  const [year, month, day] = iso.split("-").map(Number);
  return new Date(year ?? 1, (month ?? 1) - 1, day ?? 1);
}

/**
 * How values, rules and violations read on screen, in the interface language: « Oui », « 72 », « Marchande »,
 * « ≤ 60 », « Doit être inférieur ou égal à 60 ».
 */
export function usePropertyFormat(projectId: ElementId) {
  const { t } = useTranslation();
  const format = useFormatters();
  const nameOf = useElementNames(projectId);

  return useMemo(() => {
    const scalar = (definition: Definition, value: ScalarValue): string => {
      switch (definition.type.scalar) {
        case "bool":
          return value === true ? t("property.yes") : t("property.no");
        case "int":
        case "float":
          return typeof value === "number" ? format.number(value) : String(value);
        case "enum":
          return definition.options.find((o) => o.key === value)?.label ?? String(value);
        case "date":
          return typeof value === "string" ? format.date(localDate(value)) : String(value);
        case "ref":
          return typeof value === "string" ? nameOf(value as ElementId) : String(value);
        case "string":
        case "text":
          return String(value);
      }
    };

    const value = (definition: Definition, v: PropertyValue | undefined): string => {
      if (v === undefined) return t("property.empty");
      if (Array.isArray(v)) {
        const items = v as readonly ScalarValue[];
        return items.length === 0
          ? t("property.emptyList")
          : items.map((i) => scalar(definition, i)).join(", ");
      }
      return scalar(definition, v as ScalarValue);
    };

    /** Parameters of the messages of a rule, formatted for its definition. */
    const params = (definition: Definition, rule: ValidationRule): Record<string, string> => {
      switch (rule.op) {
        case "greater_than":
        case "greater_or_equal":
        case "less_than":
        case "less_or_equal":
        case "not_equal":
        case "min_length":
        case "max_length":
        case "min_count":
        case "max_count":
          return { value: format.number(rule.value) };
        case "between":
          return { min: format.number(rule.value[0]), max: format.number(rule.value[1]) };
        case "match":
          return { value: rule.value };
        case "not_empty":
          return {};
        case "in":
          return {
            value: rule.value
              .map((k) => definition.options.find((o) => o.key === k)?.label ?? k)
              .join(", "),
          };
        case "after":
        case "before":
          return { value: format.date(localDate(rule.value)) };
        case "kind":
          return {};
        case "in_category":
          return { value: nameOf(rule.value) };
      }
    };

    // Keys built from the operator: every operator has its translation (checked by format.test.ts).
    const dynamic = t as unknown as (key: string, options: Record<string, string>) => string;
    const key = (rule: ValidationRule) =>
      rule.op === "kind" ? (`kind_${rule.value}` as const) : rule.op;

    return {
      value,
      /** Short form of a rule: « ≥ 1 », « parmi : Garde, Marchande ». */
      rule: (definition: Definition, rule: ValidationRule) =>
        dynamic(`property.rule.${key(rule)}`, params(definition, rule)),
      /** Message of a violation: « Doit être inférieur ou égal à 60 », with the item for a list. */
      violation: (definition: Definition, violation: RuleViolation) => {
        const message = dynamic(
          `property.violation.${key(violation.rule)}`,
          params(definition, violation.rule),
        );
        return violation.index === undefined
          ? message
          : t("property.violation.item", { index: violation.index + 1, message });
      },
    };
  }, [t, format, nameOf]);
}

/** The rule as written in the data: « less_or_equal 60 » (E2E-08-04). */
export function rawRule(rule: ValidationRule): string {
  switch (rule.op) {
    case "not_empty":
      return rule.op;
    case "between":
      return `${rule.op} ${String(rule.value[0])} ${String(rule.value[1])}`;
    case "in":
      return `${rule.op} ${rule.value.join(", ")}`;
    default:
      return `${rule.op} ${String(rule.value)}`;
  }
}

/** « En conflit : PNJ (Non) / Montagnard (Oui) » (F07.R3). */
export function useConflictText(projectId: ElementId) {
  const { t } = useTranslation();
  const format = usePropertyFormat(projectId);
  return (definition: Definition, candidates: readonly Candidate[]) =>
    t("page.conflict", {
      candidates: candidates
        .map((c) => `${c.categoryName} (${format.value(definition, c.value)})`)
        .join(" / "),
    });
}
