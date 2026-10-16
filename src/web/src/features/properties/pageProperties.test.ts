import { describe, expect, it } from "vitest";
import { ilsaStructure, structureIds } from "../../test/structureMocks";
import {
  draftOfStructure,
  editedProperty,
  hasViolations,
  sameStructure,
  toPageProperty,
} from "./pageProperties";

const ilsa = () => {
  const { categories, properties } = ilsaStructure();
  const rows = properties.map(toPageProperty);
  return { categories, rows, draft: draftOfStructure(categories, rows) };
};

describe("toPageProperty", () => {
  it("reads the value, the origin, the violations and the fallback", () => {
    const { rows } = ilsa();
    const niveau = rows.find((r) => r.definition.name === "Niveau");

    expect(niveau?.own).toBe(72);
    expect(niveau?.violations).toEqual([{ rule: { op: "less_or_equal", value: 60 } }]);
    expect(niveau?.fallback?.value).toBe(10);
    expect(niveau?.fallback?.sources.map((s) => s.name)).toEqual(["PNJ"]);
    const hostile = rows.find((r) => r.definition.name === "Hostile");
    expect(hostile?.origin).toBe("inherited");
    expect(hostile?.fallback).toBeNull();
  });

  it("flags the page as out of rules when a value breaks a rule", () => {
    expect(hasViolations(ilsa().rows)).toBe(true);
  });
});

describe("draftOfStructure", () => {
  it("keeps the categories and only the values the page defines", () => {
    const { draft } = ilsa();

    expect(draft.categories).toEqual([structureIds.pnj]);
    expect([...draft.values]).toEqual([[structureIds.niveau, 72]]);
  });
});

describe("sameStructure", () => {
  it("compares categories and values", () => {
    const { draft } = ilsa();

    expect(sameStructure(draft, { ...draft, values: new Map(draft.values) })).toBe(true);
    expect(sameStructure(draft, { ...draft, values: new Map([[structureIds.niveau, 55]]) })).toBe(
      false,
    );
    expect(sameStructure(draft, { ...draft, categories: [] })).toBe(false);
  });
});

describe("editedProperty", () => {
  it("validates a value typed in the draft on the spot (E2E-05-12)", () => {
    const { rows, draft } = ilsa();
    const niveau = rows.find((r) => r.definition.name === "Niveau");
    if (niveau === undefined) throw new Error("Niveau is missing.");

    const fixed = editedProperty(niveau, {
      ...draft,
      values: new Map([[niveau.definition.id, 55]]),
    });
    const broken = editedProperty(niveau, {
      ...draft,
      values: new Map([[niveau.definition.id, 61]]),
    });

    expect(editedProperty(niveau, draft).shownViolations).toHaveLength(1);
    expect(fixed.shownViolations).toEqual([]);
    expect(broken.shownViolations).toEqual([{ rule: { op: "less_or_equal", value: 60 } }]);
  });

  it("keeps what the backend computed for a value that is not in the draft", () => {
    const { rows, draft } = ilsa();
    const hostile = rows.find((r) => r.definition.name === "Hostile");
    if (hostile === undefined) throw new Error("Hostile is missing.");

    expect(editedProperty(hostile, draft).draftOwn).toBeUndefined();
    expect(editedProperty(hostile, draft).shownViolations).toEqual([]);
  });
});
