import { parseElementId, type ElementId } from "../ids/elementId";

/**
 * Property types and values (docs/architecture.md §5.4), twin of Erable.Core.Properties (PropertyType.cs,
 * PropertyValue.cs). Values keep their untagged JSON form: the definition gives the type.
 */

export const scalarTypes = [
  "bool",
  "int",
  "float",
  "string",
  "text",
  "enum",
  "date",
  "ref",
] as const;

export type ScalarType = (typeof scalarTypes)[number];

export interface PropertyType {
  readonly scalar: ScalarType;
  readonly list: boolean;
}

/** `int`, `list<string>`… or `null` for anything else. */
export function parsePropertyType(text: string): PropertyType | null {
  const value = text.trim();
  const list = value.startsWith("list<") && value.endsWith(">");
  const scalar = list ? value.slice(5, -1).trim() : value;
  return isScalarType(scalar) ? { scalar, list } : null;
}

export function formatPropertyType(type: PropertyType): string {
  return type.list ? `list<${type.scalar}>` : type.scalar;
}

function isScalarType(value: string): value is ScalarType {
  return (scalarTypes as readonly string[]).includes(value);
}

/** One option of an enum: a stable key (stored) and a renamable label (shown). */
export interface EnumOption {
  readonly key: string;
  readonly label: string;
}

export type ScalarValue = boolean | number | string;

/** A typed value: `true`, `72`, `1.5`, `"texte"`, `"garde"` (enum key), `"2026-10-13"`, an ID, or a list. */
export type PropertyValue = ScalarValue | readonly ScalarValue[];

/** 2^53 − 1: the largest integer a JavaScript number holds exactly (same bound as the backend). */
export const maxSafeInteger = Number.MAX_SAFE_INTEGER;
export const maxStringLength = 1000;
export const maxTextLength = 100_000;
export const maxListCount = 1000;

export type ParseResult =
  { readonly ok: true; readonly value: PropertyValue } | { readonly ok: false };

const invalid: ParseResult = { ok: false };

/**
 * Reads a JSON value of `type`, exactly like `PropertyValues.FromJson`: malformed input is refused (unlike rule
 * violations, which only warn). Strings are NFC-normalized, IDs canonical.
 */
export function parseValue(
  json: unknown,
  type: PropertyType,
  options?: readonly EnumOption[],
): ParseResult {
  if (!type.list) {
    return parseScalar(json, type.scalar, options);
  }
  if (!Array.isArray(json) || json.length > maxListCount) {
    return invalid;
  }
  const items: ScalarValue[] = [];
  for (const item of json as unknown[]) {
    const parsed = parseScalar(item, type.scalar, options);
    if (!parsed.ok) {
      return invalid;
    }
    items.push(parsed.value as ScalarValue);
  }
  return { ok: true, value: items };
}

/** A calendar date `yyyy-MM-dd`, year 1 to 9999. */
export function isIsoDate(text: string): boolean {
  const match = /^(\d{4})-(\d{2})-(\d{2})$/.exec(text);
  if (match === null) {
    return false;
  }
  const [year, month, day] = [Number(match[1]), Number(match[2]), Number(match[3])];
  if (year < 1 || month < 1 || month > 12 || day < 1) {
    return false;
  }
  const leap = (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
  const days = [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1] ?? 0;
  return day <= days;
}

function parseScalar(
  json: unknown,
  scalar: ScalarType,
  options: readonly EnumOption[] | undefined,
): ParseResult {
  switch (scalar) {
    case "bool":
      return typeof json === "boolean" ? { ok: true, value: json } : invalid;
    case "int":
      return typeof json === "number" && Number.isInteger(json) && Math.abs(json) <= maxSafeInteger
        ? { ok: true, value: json }
        : invalid;
    case "float":
      return typeof json === "number" && Number.isFinite(json)
        ? { ok: true, value: json }
        : invalid;
    case "string":
    case "text": {
      if (typeof json !== "string") {
        return invalid;
      }
      const text = json.normalize("NFC");
      if (scalar === "string" && (text.length > maxStringLength || hasControl(text))) {
        return invalid;
      }
      if (scalar === "text" && text.length > maxTextLength) {
        return invalid;
      }
      return { ok: true, value: text };
    }
    case "enum":
      return typeof json === "string" &&
        (options === undefined || options.some((o) => o.key === json))
        ? { ok: true, value: json }
        : invalid;
    case "date":
      return typeof json === "string" && isIsoDate(json) ? { ok: true, value: json } : invalid;
    case "ref": {
      const id = typeof json === "string" ? parseElementId(json) : null;
      return id === null ? invalid : { ok: true, value: id };
    }
  }
}

/** Control characters other than tab (C# `char.IsControl`: U+0000–U+001F and U+007F–U+009F). */
function hasControl(text: string): boolean {
  for (let i = 0; i < text.length; i++) {
    const code = text.charCodeAt(i);
    if ((code < 0x20 && code !== 0x09) || (code >= 0x7f && code <= 0x9f)) {
      return true;
    }
  }
  return false;
}

/** Narrows a stored value of a `ref` property. */
export function refValue(value: PropertyValue | undefined): ElementId | null {
  return typeof value === "string" ? parseElementId(value) : null;
}
