import fc from "fast-check";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import {
  conditionOps,
  emptySearch,
  isComplete,
  isFiltered,
  modifiedWindows,
  operatorsFor,
  parseSearchParams,
  resetFilters,
  searchKinds,
  searchSorts,
  toApiQuery,
  toggleCategory,
  toggleKind,
  toSearchParams,
  type SearchState,
} from "./searchState";

const pnj = requireElementId("0D7EA95162C3B804");
const niveau = requireElementId("44B107E9C2A63F58");

const genState = fc.record<SearchState>({
  q: fc.string({ maxLength: 30 }),
  kinds: fc.subarray([...searchKinds]).map((k) => k),
  categories: fc.array(fc.constantFrom(pnj, niveau), { maxLength: 2 }).map((c) => [...new Set(c)]),
  subCategories: fc.boolean(),
  conditions: fc.array(
    fc.record({
      property: fc.constantFrom(pnj, niveau),
      op: fc.constantFrom(...conditionOps),
      value: fc.string({ maxLength: 12 }),
    }),
    { maxLength: 3 },
  ),
  modified: fc.option(fc.constantFrom(...modifiedWindows), { nil: null }),
  outOfRules: fc.boolean(),
  sort: fc.constantFrom(...searchSorts),
});

describe("search state in the URL", () => {
  it("reads an empty URL as the empty search", () => {
    expect(parseSearchParams({})).toEqual(emptySearch);
    expect(toSearchParams(emptySearch)).toEqual({});
  });

  it("writes only what differs from the empty search", () => {
    const state: SearchState = {
      ...emptySearch,
      q: "col gele",
      kinds: ["page"],
      categories: [pnj],
      subCategories: true,
      conditions: [{ property: niveau, op: "gt", value: "50" }],
      modified: "week",
      outOfRules: true,
      sort: "name",
    };

    expect(toSearchParams(state)).toEqual({
      q: "col gele",
      kind: ["page"],
      cat: [pnj],
      sub: true,
      where: [`${niveau}:gt:50`],
      mod: "week",
      oor: true,
      sort: "name",
    });
  });

  it("round-trips any state (E2E-10-08)", () => {
    fc.assert(
      fc.property(genState, (state) => {
        // Sub-categories only make sense with a category; a state is normalised by the write.
        const expected = {
          ...state,
          subCategories: state.subCategories && state.categories.length > 0,
        };
        const url = JSON.parse(JSON.stringify(toSearchParams(state))) as Record<string, unknown>;

        expect(parseSearchParams(url)).toEqual(expected);
      }),
    );
  });

  it("keeps colons inside the value of a condition", () => {
    const state = parseSearchParams({ where: [`${niveau}:eq:a:b::c`] });

    expect(state.conditions).toEqual([{ property: niveau, op: "eq", value: "a:b::c" }]);
  });

  it("accepts a lone value where a list is expected, and a number for the text", () => {
    const state = parseSearchParams({ q: 72, kind: "page", cat: pnj });

    expect(state).toMatchObject({ q: "72", kinds: ["page"], categories: [pnj] });
  });

  it("drops what it does not understand", () => {
    const state = parseSearchParams({
      kind: ["page", "root", "nonsense"],
      cat: ["xyz", pnj],
      where: ["nonsense", `${niveau}:between:1`, `zzz:eq:1`, `${niveau}:set`],
      mod: "year",
      sort: "oldest",
      sub: "maybe",
      oor: 3,
    });

    expect(state).toEqual({
      ...emptySearch,
      kinds: ["page"],
      categories: [pnj],
      conditions: [{ property: niveau, op: "set", value: "" }],
    });
  });
});

describe("search query", () => {
  it("sends the text, the filters and the order with the names of the API", () => {
    const state: SearchState = {
      ...emptySearch,
      q: "col gele",
      kinds: ["page", "category"],
      categories: [pnj],
      subCategories: true,
      conditions: [
        { property: niveau, op: "gt", value: "50" },
        { property: pnj, op: "unset", value: "" },
      ],
      modified: "day",
      outOfRules: true,
      sort: "modified",
    };

    expect(toApiQuery(state, "abc", 25)).toEqual({
      q: "col gele",
      kind: ["page", "category"],
      category: [pnj],
      subCategories: true,
      where: [`${niveau}:gt:50`, `${pnj}:unset`],
      modified: "day",
      outOfRules: true,
      sort: "modified",
      cursor: "abc",
      limit: 25,
    });
  });

  it("leaves out blank text and conditions still missing their operand", () => {
    const state: SearchState = {
      ...emptySearch,
      q: "   ",
      conditions: [{ property: niveau, op: "gt", value: " " }],
    };

    expect(toApiQuery(state)).toEqual({ sort: "relevance" });
    expect(isComplete({ property: niveau, op: "gt", value: " " })).toBe(false);
    expect(isComplete({ property: niveau, op: "set", value: "" })).toBe(true);
  });

  it("offers the operators the backend accepts for each type", () => {
    expect(operatorsFor("int")).toEqual(["eq", "ne", "lt", "le", "gt", "ge", "set", "unset"]);
    expect(operatorsFor("string")).toEqual(["eq", "ne", "contains", "set", "unset"]);
    expect(operatorsFor("bool")).toEqual(["eq", "ne", "set", "unset"]);
    expect(operatorsFor("date")).toContain("ge");
    expect(operatorsFor("enum")).not.toContain("contains");
  });
});

describe("filters", () => {
  it("unticking a type with none chosen keeps all the others (E2E-10-03)", () => {
    const state = toggleKind(emptySearch, "attachment");

    expect(state.kinds).toEqual(["page", "folder", "category", "propertyDefinition", "linkType"]);
    expect(toggleKind(state, "attachment").kinds).toEqual([]);
  });

  it("unticking every type shows every type again", () => {
    const only = toggleKind({ ...emptySearch, kinds: ["page"] }, "page");

    expect(only.kinds).toEqual([]);
  });

  it("selects and unselects categories, and forgets sub-categories without any", () => {
    const one = toggleCategory({ ...emptySearch, subCategories: false }, pnj);
    const withSub = { ...one, subCategories: true };

    expect(one.categories).toEqual([pnj]);
    expect(toggleCategory(withSub, pnj)).toMatchObject({ categories: [], subCategories: false });
  });

  it("resets the filters and keeps the text and the order (E2E-10-10)", () => {
    const state: SearchState = {
      ...emptySearch,
      q: "col gele",
      kinds: ["page"],
      outOfRules: true,
      modified: "day",
      sort: "name",
    };

    expect(isFiltered(state)).toBe(true);
    expect(resetFilters(state)).toEqual({ ...emptySearch, q: "col gele", sort: "name" });
    expect(isFiltered(resetFilters(state))).toBe(false);
  });
});
