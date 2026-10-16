import type { CategorySummary, PagePropertyItem, ValueOrigin } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import { evaluateRules, parseRule, type RuleViolation } from "../../shared/properties/rules";
import type { PropertyValue } from "../../shared/properties/types";
import { readValue, toDefinition, type Definition } from "./definitions";

/** What a property would be without the value the page defines. */
export interface Fallback {
  readonly value: PropertyValue | undefined;
  readonly origin: ValueOrigin;
  readonly sources: readonly CategorySummary[];
  readonly candidates: readonly Candidate[];
}

/** A category providing a value in a conflict (« PNJ (Non) / Montagnard (Oui) »). */
export interface Candidate {
  readonly categoryName: string;
  readonly value: PropertyValue | undefined;
}

/** A property of a page after inheritance, read from the API (F04.R4, F07.R3). */
export interface PageProperty {
  readonly definition: Definition;
  /** The value the page defines itself. */
  readonly own: PropertyValue | undefined;
  /** The effective value (undefined in a conflict). */
  readonly value: PropertyValue | undefined;
  readonly origin: ValueOrigin;
  readonly sources: readonly CategorySummary[];
  readonly candidates: readonly Candidate[];
  /** Violations the backend computed on the effective value. */
  readonly violations: readonly RuleViolation[];
  readonly fallback: Fallback | null;
}

function violationsOf(item: PagePropertyItem, definition: Definition): RuleViolation[] {
  return item.violations.flatMap((v) => {
    const rule = parseRule(v.rule, definition.type, definition.options);
    if (rule === null) return [];
    return [typeof v.index === "number" ? { rule, index: v.index } : { rule }];
  });
}

export function toPageProperty(item: PagePropertyItem): PageProperty {
  const definition = toDefinition(item.property);
  const candidates = (list: PagePropertyItem["candidates"]): Candidate[] =>
    list.map((c) => ({ categoryName: c.categoryName, value: readValue(definition, c.value) }));
  return {
    definition,
    own: readValue(definition, item.ownValue),
    value: readValue(definition, item.value),
    origin: item.origin,
    sources: item.sources,
    candidates: candidates(item.candidates),
    violations: violationsOf(item, definition),
    fallback:
      item.fallback === null
        ? null
        : {
            value: readValue(definition, item.fallback.value),
            origin: item.fallback.origin,
            sources: item.fallback.sources,
            candidates: candidates(item.fallback.candidates),
          },
  };
}

/** What the editor changes on a page: its categories and the values it defines (saved with the content). */
export interface StructureDraft {
  readonly categories: readonly ElementId[];
  /**
   * Own values by property; a property missing here has no value of its own. An entry holding `undefined` is a field
   * the user emptied: it is shown empty (not as the inherited value) and is not saved.
   */
  readonly values: ReadonlyMap<ElementId, PropertyValue | undefined>;
}

export function draftOfStructure(
  categories: readonly CategorySummary[],
  properties: readonly PageProperty[],
): StructureDraft {
  return {
    categories: categories.map((c) => c.id),
    values: new Map(
      properties.flatMap((p) => (p.own === undefined ? [] : [[p.definition.id, p.own] as const])),
    ),
  };
}

/** The values a draft would save: emptied fields hold nothing. */
export function savedValues(draft: StructureDraft): [ElementId, PropertyValue][] {
  return [...draft.values].flatMap(([id, value]) => (value === undefined ? [] : [[id, value]]));
}

export function sameStructure(a: StructureDraft, b: StructureDraft): boolean {
  const left = savedValues(a);
  const right = new Map(savedValues(b));
  return (
    a.categories.length === b.categories.length &&
    a.categories.every((c, i) => c === b.categories[i]) &&
    left.length === right.size &&
    left.every(([id, v]) => JSON.stringify(right.get(id)) === JSON.stringify(v))
  );
}

/** A row of the properties panel while editing: what the draft makes of a property (F05.R6, F08.R3). */
export interface EditedProperty extends PageProperty {
  /** The page defines this property in the draft (possibly emptied). */
  readonly touched: boolean;
  /** The value the page defines in the draft. */
  readonly draftOwn: PropertyValue | undefined;
  /** Violations of the value shown now: computed here for a value typed in the draft, by the backend otherwise. */
  readonly shownViolations: readonly RuleViolation[];
}

/**
 * Overlays the draft on the resolution: a value typed in the draft is validated on the spot by the TS evaluator (the
 * « instant » feedback of E2E-05-12); anything else keeps what the backend computed. References are left to the
 * backend, whose rules need the categories of the target page.
 */
export function editedProperty(property: PageProperty, draft: StructureDraft): EditedProperty {
  const draftOwn = draft.values.get(property.definition.id);
  const touched = draft.values.has(property.definition.id);
  const typed = draftOwn !== undefined && property.definition.type.scalar !== "ref";
  return {
    ...property,
    touched,
    draftOwn,
    shownViolations: typed
      ? evaluateRules(property.definition.type, property.definition.rules, draftOwn)
      : touched && !sameValue(draftOwn, property.own)
        ? []
        : property.violations,
  };
}

function sameValue(a: PropertyValue | undefined, b: PropertyValue | undefined): boolean {
  return JSON.stringify(a) === JSON.stringify(b);
}

/** At least one value of the page breaks the rules of its property (the « hors règles » badge, F08.R3). */
export function hasViolations(properties: readonly PageProperty[]): boolean {
  return properties.some((p) => p.violations.length > 0);
}
