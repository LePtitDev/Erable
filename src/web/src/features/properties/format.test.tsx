import { renderHook } from "@testing-library/react";
import type { ReactNode } from "react";
import { describe, expect, it } from "vitest";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { I18nextProvider } from "react-i18next";
import fr from "../../shared/i18n/locales/fr.json";
import type { ValidationRule } from "../../shared/properties/rules";
import { requireElementId } from "../../shared/ids/elementId";
import { testI18n } from "../../test/i18n";
import { ids } from "../../test/projectMocks";
import { definitions } from "../../test/structureMocks";
import { toDefinition } from "./definitions";
import { rawRule, usePropertyFormat } from "./format";

const operators = [
  "greater_than",
  "greater_or_equal",
  "less_than",
  "less_or_equal",
  "between",
  "not_equal",
  "min_length",
  "max_length",
  "match",
  "not_empty",
  "in",
  "after",
  "before",
  "kind_page",
  "kind_category",
  "in_category",
  "min_count",
  "max_count",
];

function renderFormat() {
  const wrapper = ({ children }: { children: ReactNode }) => (
    <I18nextProvider i18n={testI18n}>
      <QueryClientProvider client={new QueryClient()}>{children}</QueryClientProvider>
    </I18nextProvider>
  );
  return renderHook(() => usePropertyFormat(ids.project), { wrapper }).result.current;
}

describe("property formatting", () => {
  it("has a short form and a message for every operator", () => {
    for (const op of operators) {
      expect(fr.property.rule).toHaveProperty(op);
      expect(fr.property.violation).toHaveProperty(op);
    }
  });

  it("formats the rules and violations of Niveau (E2E-05-12)", () => {
    const format = renderFormat();
    const niveau = toDefinition(definitions.niveau);

    expect(niveau.rules.map((r) => format.rule(niveau, r))).toEqual(["≥ 1", "≤ 60"]);
    expect(format.violation(niveau, { rule: { op: "less_or_equal", value: 60 } })).toBe(
      "Doit être inférieur ou égal à 60",
    );
    expect(format.violation(niveau, { rule: { op: "greater_than", value: 0 }, index: 1 })).toBe(
      "Élément 2 : Doit être supérieur à 0",
    );
  });

  it("formats values by type", () => {
    const format = renderFormat();

    expect(format.value(toDefinition(definitions.hostile), false)).toBe("Non");
    expect(format.value(toDefinition(definitions.role), "garde")).toBe("Garde");
    expect(format.value(toDefinition(definitions.niveau), 1250.5)).toBe(
      new Intl.NumberFormat("fr").format(1250.5),
    );
    expect(format.value(toDefinition(definitions.niveau), undefined)).toBe("—");
    expect(
      format.value(
        { ...toDefinition(definitions.niveau), type: { scalar: "int", list: true } },
        [1, 2],
      ),
    ).toBe("1, 2");
  });

  it("formats an enum subset with the labels", () => {
    const format = renderFormat();
    const role = toDefinition(definitions.role);

    expect(format.rule(role, { op: "in", value: ["garde", "marchande"] })).toBe(
      "parmi : Garde, Marchande",
    );
  });

  it.each<[ValidationRule, string]>([
    [{ op: "less_or_equal", value: 60 }, "less_or_equal 60"],
    [{ op: "between", value: [1, 60] }, "between 1 60"],
    [{ op: "not_empty" }, "not_empty"],
    [{ op: "in", value: ["a", "b"] }, "in a, b"],
    [
      { op: "in_category", value: requireElementId("2000000000000001") },
      "in_category 2000000000000001",
    ],
  ])("writes %j as the data says (E2E-08-04)", (rule, text) => {
    expect(rawRule(rule)).toBe(text);
  });
});
