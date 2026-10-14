import { parseElementId, type ElementId } from "../ids/elementId";

/**
 * Character-level grammar of `[[ID]]` / `![[ID]]` (docs/markdown-syntax.md §2). Twin of
 * `src/Erable.Core/Markdown/ReferenceSyntax.cs`; both are checked against `spec/fixtures/markdown`.
 */

export const maxWidth = 4096;
/** Longest label before trimming (R11): bounds every scan, so that parsing stays linear. */
export const maxLabelLength = 200;
/** Longest attribute block between the braces (R11). */
export const maxAttributesLength = 64;

export type EmbedAlign = "left" | "center" | "right";

export interface EmbedAttributes {
  readonly width: number | null;
  readonly align: EmbedAlign | null;
}

export interface ReferenceMatch {
  readonly id: ElementId;
  readonly anchor: string | null;
  readonly label: string | null;
  readonly embed: boolean;
  readonly attrs: EmbedAttributes | null;
  /** Number of characters consumed, attributes included. */
  readonly length: number;
}

const idLength = 16;

/**
 * Label separator at `i`: 1 for `|`, 2 for `\|` (mandatory in tables, R12), 0 otherwise. In a GFM table the cells
 * are already split by remark-gfm when this runs, so a bare bar never reaches a reference there.
 */
function separatorLength(text: string, i: number): number {
  if (text[i] === "|") return 1;
  return text[i] === "\\" && text[i + 1] === "|" ? 2 : 0;
}

export function isAnchorChar(c: string | undefined): boolean {
  return c !== undefined && /^[A-Za-z0-9_-]$/.test(c);
}

/** Reads a reference or embed at `start` of `text`; null when there is none. */
export function matchReference(text: string, start = 0): ReferenceMatch | null {
  const end = text.length;
  let i = start;
  let embed = false;
  if (text[i] === "!") {
    embed = true;
    i++;
  }
  if (text[i] !== "[" || text[i + 1] !== "[") {
    return null;
  }
  i += 2;
  const id = i + idLength <= end ? parseElementId(text.slice(i, i + idLength)) : null;
  if (id === null) {
    return null;
  }
  i += idLength;

  let anchor: string | null = null;
  if (text[i] === "#") {
    if (embed) {
      return null;
    }
    const anchorStart = ++i;
    while (isAnchorChar(text[i])) {
      i++;
    }
    if (i === anchorStart) {
      return null;
    }
    anchor = text.slice(anchorStart, i);
  }

  let label: string | null = null;
  const separator = separatorLength(text, i);
  if (separator > 0) {
    i += separator;
    const labelStart = i;
    while (i + 1 < end && !(text[i] === "]" && text[i + 1] === "]")) {
      if (text[i] === "\n" || text[i] === "\r" || i - labelStart >= maxLabelLength) {
        return null;
      }
      i++;
    }
    if (i + 1 >= end) {
      return null;
    }
    const trimmed = text.slice(labelStart, i).replaceAll("\\|", "|").trim();
    label = trimmed.length === 0 ? null : trimmed;
  }

  if (text[i] !== "]" || text[i + 1] !== "]") {
    return null;
  }
  i += 2;

  let attrs: EmbedAttributes | null = null;
  if (embed) {
    const read = readAttributes(text, i);
    if (read !== null) {
      attrs = read.attrs;
      i += read.length;
    }
  }
  return { id, anchor, label, embed, attrs, length: i - start };
}

/** `{width=320 align=right}`: known keys, each at most once, valid values; otherwise null (nothing consumed). */
export function readAttributes(
  text: string,
  start: number,
): { attrs: EmbedAttributes; length: number } | null {
  if (text[start] !== "{") {
    return null;
  }
  // Bounded search: never scan further than the longest valid block (linear parsing, R11).
  const relative = text.slice(start + 1, start + 2 + maxAttributesLength).indexOf("}");
  if (relative < 0) {
    return null;
  }
  const close = start + 1 + relative;
  const body = text.slice(start + 1, close);
  if (body.length === 0 || body.startsWith(" ") || body.endsWith(" ")) {
    return null;
  }
  let width: number | null = null;
  let align: EmbedAlign | null = null;
  for (const pair of body.split(" ").filter((p) => p.length > 0)) {
    const equals = pair.indexOf("=");
    if (equals <= 0) {
      return null;
    }
    const key = pair.slice(0, equals);
    const value = pair.slice(equals + 1);
    if (key === "width" && width === null && /^[1-9][0-9]{0,3}$/.test(value)) {
      width = Number(value);
      if (width > maxWidth) {
        return null;
      }
    } else if (
      key === "align" &&
      align === null &&
      (value === "left" || value === "center" || value === "right")
    ) {
      align = value;
    } else {
      return null;
    }
  }
  return { attrs: { width, align }, length: close - start + 1 };
}
