import type { PropertyValue } from "../../shared/properties/types";
import { initialValue, type Definition } from "../properties/definitions";
import type { SearchCondition } from "./searchState";

/** The definition of one item: a condition on a list holds when one item satisfies it, and `text` is typed on a line. */
export function itemDefinition(definition: Definition): Definition {
  const scalar = definition.type.scalar === "text" ? "string" : definition.type.scalar;
  return { ...definition, type: { scalar, list: false } };
}

/** The operand written in the URL for a value of the editor (and back). */
export function operandOf(value: PropertyValue | undefined): string {
  return value === undefined || Array.isArray(value) ? "" : String(value);
}

export function operandValue(definition: Definition, operand: string): PropertyValue | undefined {
  const scalar = itemDefinition(definition).type.scalar;
  switch (scalar) {
    case "bool":
      return operand === "true" ? true : operand === "false" ? false : undefined;
    case "int":
    case "float": {
      const number = Number(operand);
      return operand.trim() === "" || !Number.isFinite(number) ? undefined : number;
    }
    default:
      return operand === "" ? undefined : operand;
  }
}

/** A condition with a value that suits the property, so that choosing a property filters at once (mockup: « Niveau > 10 »). */
export function newCondition(definition: Definition): SearchCondition {
  return {
    property: definition.id,
    op: "eq",
    value: operandOf(initialValue(itemDefinition(definition))),
  };
}
