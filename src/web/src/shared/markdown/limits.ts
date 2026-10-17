/**
 * Safety limits on the Markdown of a page (docs/markdown-syntax.md §6, M5-04): twin of `MarkdownLimits.cs`, driven by the
 * same `spec/fixtures/limits`. The parsers take quadratic time, or overflow their stack, on a few dozen kilobytes of
 * degenerate input; one linear pre-scan refuses those shapes before any parser runs. Real pages never come near.
 */
export const markdownLimits = {
  /** UTF-16 code units: storage, search index and revisions, not parsing time. */
  maxLength: 1_048_576,
  maxLineLength: 10_000,
  /** Of `[ ] ( ) >`, in a row. */
  maxRepeatedCharacters: 256,
  /** Quote and list markers at the start of one line. */
  maxNesting: 32,
  /** Leading spaces and tabs of a line. */
  maxIndentation: 256,
  /** Consecutive lines without a blank line. */
  maxParagraphLines: 2_000,
} as const;

export type MarkdownLimitRule =
  "size" | "lineLength" | "repeatedCharacter" | "nesting" | "paragraphLines";

export interface MarkdownLimitViolation {
  readonly rule: MarkdownLimitRule;
  /** 1-based line of the first offence; 0 for the size of the whole page. */
  readonly line: number;
}

/** The first broken rule in document order, or null when the page is within the limits. */
export function checkMarkdownLimits(markdown: string): MarkdownLimitViolation | null {
  if (markdown.length > markdownLimits.maxLength) return { rule: "size", line: 0 };

  let lineNumber = 0;
  let consecutiveLines = 0;
  let start = 0;
  for (;;) {
    const newline = markdown.indexOf("\n", start);
    const end = newline < 0 ? markdown.length : newline;
    const lineEnd = end > start && markdown.charCodeAt(end - 1) === 13 ? end - 1 : end;
    lineNumber++;

    const result = checkLine(markdown, start, lineEnd);
    if (result.rule !== null) return { rule: result.rule, line: lineNumber };

    consecutiveLines = result.blank ? 0 : consecutiveLines + 1;
    if (consecutiveLines > markdownLimits.maxParagraphLines) {
      return { rule: "paragraphLines", line: lineNumber };
    }
    if (newline < 0) return null;
    start = newline + 1;
  }
}

const isBlankChar = (c: number) => c === 32 || c === 9;
const isDigit = (c: number) => c >= 48 && c <= 57;
// [ ] ( ) >
const isRepeatGuarded = (c: number) => c === 91 || c === 93 || c === 40 || c === 41 || c === 62;

function checkLine(
  text: string,
  start: number,
  end: number,
): { readonly rule: MarkdownLimitRule | null; readonly blank: boolean } {
  if (end - start > markdownLimits.maxLineLength) return { rule: "lineLength", blank: false };

  let blank = true;
  let run = 0;
  let previous = -1;
  for (let i = start; i < end; i++) {
    const c = text.charCodeAt(i);
    if (!isBlankChar(c)) blank = false;
    run = c === previous ? run + 1 : 1;
    previous = c;
    if (run > markdownLimits.maxRepeatedCharacters && isRepeatGuarded(c)) {
      return { rule: "repeatedCharacter", blank: false };
    }
  }
  if (!blank && !nestingWithinLimits(text, start, end)) return { rule: "nesting", blank: false };
  return { rule: null, blank };
}

function skipBlanks(text: string, from: number, end: number): number {
  let i = from;
  while (i < end && isBlankChar(text.charCodeAt(i))) i++;
  return i;
}

/** Width of the quote or list marker at `i`, 0 when the text is not one. */
function markerWidth(text: string, i: number, end: number): number {
  const c = text.charCodeAt(i);
  if (c === 62) return 1; // >
  if (c === 45 || c === 42 || c === 43) return i + 1 < end && text.charCodeAt(i + 1) === 32 ? 2 : 0;
  let digits = 0;
  while (i + digits < end && isDigit(text.charCodeAt(i + digits))) digits++;
  const after = i + digits;
  const delimiter = text.charCodeAt(after);
  return digits >= 1 &&
    digits <= 9 &&
    after + 1 < end &&
    (delimiter === 46 || delimiter === 41) &&
    text.charCodeAt(after + 1) === 32
    ? digits + 2
    : 0;
}

function nestingWithinLimits(text: string, start: number, end: number): boolean {
  let i = skipBlanks(text, start, end);
  if (i - start > markdownLimits.maxIndentation) return false;
  let markers = 0;
  while (i < end) {
    const width = markerWidth(text, i, end);
    if (width === 0) return true;
    markers++;
    if (markers > markdownLimits.maxNesting) return false;
    i = skipBlanks(text, i + width, end);
  }
  return true;
}

/** The i18n key of the message for a broken rule: the same codes as the errors of the backend (`errors.<code>`). */
export function limitErrorKey(rule: MarkdownLimitRule) {
  switch (rule) {
    case "size":
      return "errors.page.content.tooLarge";
    case "lineLength":
      return "errors.page.content.lineTooLong";
    case "repeatedCharacter":
      return "errors.page.content.repeatedCharacter";
    case "nesting":
      return "errors.page.content.tooNested";
    case "paragraphLines":
      return "errors.page.content.paragraphTooLong";
  }
}
