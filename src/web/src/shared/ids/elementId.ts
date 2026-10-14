/**
 * Universal element identifier (docs/architecture.md §4): 64 bits, always carried as a 16-character
 * hexadecimal string. A JavaScript `number` would lose precision above 2^53, so the branded type can only be
 * obtained through {@link parseElementId}: an ID in a `number` cannot be written.
 */
export type ElementId = string & { readonly __brand: "ElementId" };

const pattern = /^[0-9A-Fa-f]{16}$/;
const zero = "0000000000000000";

/**
 * Reads exactly 16 hexadecimal characters (case-insensitive) and returns the canonical upper-case form,
 * or `null` for anything else (spaces, other lengths, prefixes, the reserved value 0).
 */
export function parseElementId(value: string): ElementId | null {
  if (!pattern.test(value)) {
    return null;
  }
  const canonical = value.toUpperCase();
  return canonical === zero ? null : (canonical as ElementId);
}

/** Like {@link parseElementId}, for values that must be valid (route parameters already validated, constants). */
export function requireElementId(value: string): ElementId {
  const id = parseElementId(value);
  if (id === null) {
    throw new Error(`Not a valid element ID: "${value}"`);
  }
  return id;
}

/** Display form, groups of four: `3F2A 9C14 E07B 5D21` (F03.R1). */
export function formatDisplay(id: ElementId): string {
  return id.replace(/(.{4})(?=.)/g, "$1 ");
}
