import type { PropertyDefinitionItem } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import { parseRules, type ValidationRule } from "../../shared/properties/rules";
import {
  parsePropertyType,
  parseValue,
  type EnumOption,
  type PropertyType,
  type PropertyValue,
} from "../../shared/properties/types";

/** A property definition read from the API, with its free JSON parsed (type, default, rules). */
export interface Definition {
  readonly id: ElementId;
  readonly name: string;
  readonly key: string;
  readonly description: string;
  readonly type: PropertyType;
  readonly default: PropertyValue | undefined;
  readonly rules: readonly ValidationRule[];
  readonly options: readonly EnumOption[];
}

const fallbackType: PropertyType = { scalar: "string", list: false };

/** Reads a definition; whatever the backend sent that does not parse is dropped (the backend is the authority). */
export function toDefinition(item: PropertyDefinitionItem): Definition {
  const type = parsePropertyType(item.type) ?? fallbackType;
  const parsedDefault = item.default === null ? null : parseValue(item.default, type, item.options);
  return {
    id: item.id,
    name: item.name,
    key: item.key,
    description: item.description,
    type,
    default: parsedDefault?.ok === true ? parsedDefault.value : undefined,
    rules: parseRules(item.rules, type, item.options) ?? [],
    options: item.options,
  };
}

/** A value of the API (free JSON) read with its definition; undefined when absent or malformed. */
export function readValue(definition: Definition, json: unknown): PropertyValue | undefined {
  if (json === null || json === undefined) return undefined;
  const parsed = parseValue(json, definition.type, definition.options);
  return parsed.ok ? parsed.value : undefined;
}

/** The type as written in the documents: `int`, `list<string>`. */
export function typeName(definition: Definition): string {
  return definition.type.list ? `list<${definition.type.scalar}>` : definition.type.scalar;
}

/**
 * A first value for a property just given to a page or a category: its default, else the neutral value of its type
 * (a reference has none: the user must pick a page).
 */
export function initialValue(definition: Definition): PropertyValue | undefined {
  if (definition.default !== undefined) return definition.default;
  if (definition.type.list) return [];
  switch (definition.type.scalar) {
    case "bool":
      return false;
    case "int":
    case "float":
      return 0;
    case "string":
    case "text":
      return "";
    case "enum":
      return definition.options[0]?.key;
    case "date":
      return new Date().toISOString().slice(0, 10);
    case "ref":
      return undefined;
  }
}
