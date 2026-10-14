import { describe, expect, it } from "vitest";
import { loadFixtures } from "../../test/fixtures";
import { analyzeMarkdown } from "./analyze";
import { slugify } from "./slug";

// spec/fixtures/markdown, shared with tests/Erable.Core.Tests/Markdown/MarkdownFixtureTests.cs.

interface RefCase {
  readonly md: string;
  readonly refs: readonly unknown[];
}
interface PlainCase {
  readonly md: string;
  readonly plain: string;
}
interface HeadingCase {
  readonly md: string;
  readonly headings: readonly unknown[];
}
interface SlugCase {
  readonly text: string;
  readonly slug: string;
}

const markdown = (file: string) =>
  loadFixtures<{ readonly file: string }>("markdown").filter((c) => c.file === file);

describe("references (refs.json, edge-cases.json)", () => {
  it.each([...markdown("refs.json"), ...markdown("edge-cases.json")] as unknown as RefCase[])(
    "$file › $name",
    (c) => {
      expect(analyzeMarkdown(c.md).references).toEqual(c.refs);
    },
  );
});

describe("plain text (plain-text.json)", () => {
  it.each(markdown("plain-text.json") as unknown as PlainCase[])("$file › $name", (c) => {
    expect(analyzeMarkdown(c.md).plainText).toBe(c.plain);
  });
});

describe("headings (headings.json)", () => {
  it.each(markdown("headings.json") as unknown as HeadingCase[])("$file › $name", (c) => {
    const actual = analyzeMarkdown(c.md).headings.map(({ level, text, anchor }) => ({
      level,
      text,
      anchor,
    }));
    expect(actual).toEqual(c.headings);
  });
});

describe("slugs (slugs.json)", () => {
  it.each(markdown("slugs.json") as unknown as SlugCase[])("$file › $name", (c) => {
    expect(slugify(c.text)).toBe(c.slug);
  });
});
