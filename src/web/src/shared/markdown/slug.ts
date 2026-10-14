/**
 * Heading anchors (docs/markdown-syntax.md §3.1). Twin of `src/Erable.Core/Markdown/Slugs.cs`,
 * checked against `spec/fixtures/markdown/slugs.json`.
 */

export const fallbackSlug = "section";

const replacements: Readonly<Record<string, string>> = {
  œ: "oe",
  Œ: "oe",
  æ: "ae",
  Æ: "ae",
  ß: "ss",
  ø: "o",
  Ø: "o",
  đ: "d",
  Đ: "d",
  ł: "l",
  Ł: "l",
};

export function slugify(text: string): string {
  const replaced = Array.from(text, (c) => replacements[c] ?? c).join("");
  const slug = replaced
    .normalize("NFD")
    .replace(/\p{Mn}/gu, "")
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, "-")
    .replace(/^-+|-+$/g, "");
  return slug.length === 0 ? fallbackSlug : slug;
}

/** Makes anchors unique within a page: the second `a` becomes `a-1`, the third `a-2`. */
export function createUniqueAnchors(): (anchor: string) => string {
  const seen = new Map<string, number>();
  return (anchor) => {
    let count = seen.get(anchor);
    if (count === undefined) {
      seen.set(anchor, 1);
      return anchor;
    }
    let candidate: string;
    do {
      candidate = `${anchor}-${count}`;
      count++;
    } while (seen.has(candidate));
    seen.set(anchor, count);
    seen.set(candidate, 1);
    return candidate;
  };
}
