import type { ElementKind } from "../../shared/api/tree";
import { parseElementId, type ElementId } from "../../shared/ids/elementId";
import type { ScalarType } from "../../shared/properties/types";

// The whole state of a search lives in the URL (F10.R4, M3-03): this module reads it from there, writes it back
// and turns it into the query of `GET search`. No React in here.

export type SearchSort = "relevance" | "modified" | "name";
export type ModifiedWindow = "day" | "week" | "month";
export type ConditionOp = "eq" | "ne" | "lt" | "le" | "gt" | "ge" | "contains" | "set" | "unset";

export const searchSorts: readonly SearchSort[] = ["relevance", "modified", "name"];
export const modifiedWindows: readonly ModifiedWindow[] = ["day", "week", "month"];
export const conditionOps: readonly ConditionOp[] = [
  "eq",
  "ne",
  "lt",
  "le",
  "gt",
  "ge",
  "contains",
  "set",
  "unset",
];

/** The kinds a search can return, in the order of the type filter. */
/** A kind a search can return (roots are not searchable). */
export type SearchKind = Exclude<ElementKind, "root">;

export const searchKinds: readonly SearchKind[] = [
  "page",
  "folder",
  "category",
  "propertyDefinition",
  "linkType",
  "attachment",
];

/** One condition on a property; `value` is the operand as it is written in the URL (empty for `set`/`unset`). */
export interface SearchCondition {
  readonly property: ElementId;
  readonly op: ConditionOp;
  readonly value: string;
}

export interface SearchState {
  readonly q: string;
  /** Empty = every kind. */
  readonly kinds: readonly SearchKind[];
  readonly categories: readonly ElementId[];
  readonly subCategories: boolean;
  readonly conditions: readonly SearchCondition[];
  readonly modified: ModifiedWindow | null;
  readonly outOfRules: boolean;
  readonly sort: SearchSort;
}

export const emptySearch: SearchState = {
  q: "",
  kinds: [],
  categories: [],
  subCategories: false,
  conditions: [],
  modified: null,
  outOfRules: false,
  sort: "relevance",
};

/** Search parameters of the URL: short names, defaults left out, so that a shared link stays readable. */
export interface SearchParams {
  q?: string;
  kind?: string[];
  cat?: string[];
  sub?: true;
  where?: string[];
  mod?: ModifiedWindow;
  oor?: true;
  sort?: SearchSort;
}

const hasOperand = (op: ConditionOp) => op !== "set" && op !== "unset";

/** Operators that make sense for a scalar type (the backend refuses the others, `PropertyCondition.OperatorAllowed`). */
export function operatorsFor(scalar: ScalarType): readonly ConditionOp[] {
  const ordered = scalar === "int" || scalar === "float" || scalar === "date";
  return [
    "eq",
    "ne",
    ...(ordered ? (["lt", "le", "gt", "ge"] as const) : []),
    ...(scalar === "string" || scalar === "text" ? (["contains"] as const) : []),
    "set",
    "unset",
  ];
}

export function needsOperand(op: ConditionOp): boolean {
  return hasOperand(op);
}

function strings(value: unknown): string[] {
  const list = Array.isArray(value) ? value : value === undefined ? [] : [value];
  return list
    .filter((x): x is string | number => typeof x === "string" || typeof x === "number")
    .map(String);
}

function isOneOf<T extends string>(list: readonly T[], value: unknown): value is T {
  return typeof value === "string" && (list as readonly string[]).includes(value);
}

function parseCondition(text: string): SearchCondition | null {
  const first = text.indexOf(":");
  const second = text.indexOf(":", first + 1);
  const property = parseElementId(text.slice(0, Math.max(first, 0)));
  const op = text.slice(first + 1, second < 0 ? undefined : second);
  if (property === null || first < 0 || !isOneOf(conditionOps, op)) return null;
  return { property, op, value: second < 0 ? "" : text.slice(second + 1) };
}

/** Reads the (untrusted) search parameters of the URL; whatever is not understood is dropped. */
export function parseSearchParams(raw: Record<string, unknown>): SearchState {
  const q = raw["q"];
  const sort = raw["sort"];
  const modified = raw["mod"];
  return {
    q: typeof q === "string" ? q : typeof q === "number" ? String(q) : "",
    kinds: strings(raw["kind"]).filter((k): k is SearchKind => isOneOf(searchKinds, k)),
    categories: strings(raw["cat"]).flatMap((c) => parseElementId(c) ?? []),
    subCategories: raw["sub"] === true || raw["sub"] === "true",
    conditions: strings(raw["where"]).flatMap((c) => parseCondition(c) ?? []),
    modified: isOneOf(modifiedWindows, modified) ? modified : null,
    outOfRules: raw["oor"] === true || raw["oor"] === "true",
    sort: isOneOf(searchSorts, sort) ? sort : "relevance",
  };
}

/** Parameters to put in the URL for a state: only what differs from the empty search. */
export function toSearchParams(state: SearchState): SearchParams {
  return {
    ...(state.q === "" ? {} : { q: state.q }),
    ...(state.kinds.length === 0 ? {} : { kind: [...state.kinds] }),
    ...(state.categories.length === 0 ? {} : { cat: [...state.categories] }),
    ...(state.subCategories && state.categories.length > 0 ? { sub: true as const } : {}),
    ...(state.conditions.length === 0
      ? {}
      : { where: state.conditions.map((c) => `${c.property}:${c.op}:${c.value}`) }),
    ...(state.modified === null ? {} : { mod: state.modified }),
    ...(state.outOfRules ? { oor: true as const } : {}),
    ...(state.sort === "relevance" ? {} : { sort: state.sort }),
  };
}

/** A condition that can be sent: a comparison needs its operand. */
export function isComplete(condition: SearchCondition): boolean {
  return !hasOperand(condition.op) || condition.value.trim() !== "";
}

/** True when something other than the text narrows the search. */
export function isFiltered(state: SearchState): boolean {
  return (
    state.kinds.length > 0 ||
    state.categories.length > 0 ||
    state.conditions.length > 0 ||
    state.modified !== null ||
    state.outOfRules
  );
}

/** « Réinitialiser » drops every filter and keeps the text (E2E-10-10); the order of the results stays too. */
export function resetFilters(state: SearchState): SearchState {
  return { ...emptySearch, q: state.q, sort: state.sort };
}

/** Query of `GET search` for a state (the names are those of the API). */
export function toApiQuery(state: SearchState, cursor?: string, limit?: number) {
  const where = state.conditions
    .filter(isComplete)
    .map((c) => (hasOperand(c.op) ? `${c.property}:${c.op}:${c.value}` : `${c.property}:${c.op}`));
  return {
    ...(state.q.trim() === "" ? {} : { q: state.q }),
    ...(state.kinds.length === 0 ? {} : { kind: [...state.kinds] }),
    ...(state.categories.length === 0
      ? {}
      : { category: [...state.categories], subCategories: state.subCategories }),
    ...(where.length === 0 ? {} : { where }),
    ...(state.modified === null ? {} : { modified: state.modified }),
    ...(state.outOfRules ? { outOfRules: true } : {}),
    sort: state.sort,
    ...(cursor === undefined ? {} : { cursor }),
    ...(limit === undefined ? {} : { limit }),
  };
}

/** Toggles a type; with no type chosen every type is shown, so unticking one keeps all the others. */
export function toggleKind(
  state: SearchState,
  kind: SearchKind,
  available: readonly SearchKind[] = searchKinds,
): SearchState {
  const current = state.kinds.length === 0 ? available : state.kinds;
  const next = current.includes(kind) ? current.filter((k) => k !== kind) : [...current, kind];
  // Every type, or none: no filter at all.
  const kinds = next.length === 0 || available.every((k) => next.includes(k)) ? [] : next;
  return { ...state, kinds: searchKinds.filter((k) => kinds.includes(k)) };
}

export function toggleCategory(state: SearchState, id: ElementId): SearchState {
  const categories = state.categories.includes(id)
    ? state.categories.filter((c) => c !== id)
    : [...state.categories, id];
  return { ...state, categories, subCategories: categories.length > 0 && state.subCategories };
}
