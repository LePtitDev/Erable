import fc from "fast-check";
import type { Nodes } from "mdast";
import { describe, expect, it } from "vitest";
import { analyzeMarkdown } from "./analyze";
import remarkGfm from "remark-gfm";
import remarkParse from "remark-parse";
import { unified } from "unified";
import { parseMarkdown } from "./remarkErable";

/** The same pipeline without the Erable extension (baseline for cost comparisons). */
const createMarkdownParserWithout = () =>
  unified().use(remarkParse).use(remarkGfm, { singleTilde: false });

// docs/testing.md §4.1 (Parseur de références), twin of MarkdownPropertyTests.cs.

const hex = "0123456789ABCDEF";
const ids = fc
  .array(fc.constantFrom(...Array.from(hex)), { minLength: 16, maxLength: 16 })
  .map((chars) => chars.join(""))
  .filter((id) => id !== "0000000000000000");
// Single spaces only: four leading spaces would open an indented code block, where references are not recognized.
const prose = fc
  .array(
    fc.constantFrom(...Array.from("abcdefghijklmnopqrstuvwxyzéèàçABCXYZ0123456789 ,.;:'?\n")),
    {
      maxLength: 40,
    },
  )
  .map((chars) => chars.join("").replace(/ {2,}/g, " "));
const labels = fc
  .array(fc.constantFrom(...Array.from("abcdefghij XYZéà|#")), { minLength: 1, maxLength: 12 })
  .map((chars) => chars.join(""))
  .filter((label) => label.trim().length > 0);

describe("markdown properties", () => {
  it("finds every generated reference at its position", () => {
    const piece = fc.record({
      id: ids,
      label: fc.option(labels, { nil: null }),
      embed: fc.boolean(),
      before: prose,
    });
    fc.assert(
      fc.property(fc.array(piece, { minLength: 1, maxLength: 8 }), prose, (pieces, tail) => {
        let markdown = "";
        const expected = pieces.map(({ id, label, embed, before }) => {
          markdown += `${before} `;
          const token = `${embed ? "!" : ""}[[${id}${label === null ? "" : `|${label}`}]]`;
          const start = markdown.length;
          markdown += `${token} `;
          return { id, label: label?.trim() ?? null, embed, start, end: start + token.length };
        });
        markdown += tail;

        const actual = analyzeMarkdown(markdown).references.map(
          ({ id, label, embed, start, end }) => ({ id, label, embed, start, end }),
        );
        expect(actual).toEqual(expected);
      }),
    );
  });

  it("never throws on random input", () => {
    const markdownish = fc
      .array(fc.constantFrom(...Array.from("[]!|#{}()<>`*_~\\-=:/ \n\r\taZ03F9é😀")), {
        maxLength: 200,
      })
      .map((chars) => chars.join(""));
    fc.assert(
      fc.property(fc.oneof(markdownish, fc.string({ unit: "binary" })), (input) => {
        const { references } = analyzeMarkdown(input);
        for (const r of references) {
          expect(r.start).toBeGreaterThanOrEqual(0);
          expect(r.end).toBeLessThanOrEqual(input.length);
        }
      }),
    );
  });

  const time = (run: () => unknown) => {
    let best = Infinity;
    for (let i = 0; i < 2; i++) {
      const start = performance.now();
      run();
      best = Math.min(best, performance.now() - start);
    }
    return best;
  };

  it("parses one megabyte in linear time", () => {
    // No list between paragraphs: micromark 4.0.3 itself is quadratic on alternating paragraphs and lists
    // (see the next test and tasks/M1-04 notes); everything else must stay linear.
    const block = `## Titre\n\nTexte [[3F2A9C14E07B5D21|lien]] et ![[7A1C4E90B3F2D615]]{width=320} [[[[ ]] \` x \`\n\n| a | b |\n|---|---|\n| [[5B19C0E2A4D7F311]] | c |\n\n\`\`\`\ncode [[5B19C0E2A4D7F311]]\n\`\`\`\n\n`;
    const small = block.repeat(1_000);
    const large = block.repeat(8_000);
    analyzeMarkdown(small); // warm-up

    const timeSmall = time(() => analyzeMarkdown(small));
    const timeLarge = time(() => analyzeMarkdown(large));

    expect(large.length).toBeGreaterThan(1_000_000);
    expect(analyzeMarkdown(large).references).toHaveLength(3 * 8_000);
    // 8× the input must take far less than 64× the time; allow noise up to 24×.
    expect(timeLarge).toBeLessThan(Math.max(timeSmall, 5) * 24);
  }, 120_000);

  it("adds no cost of its own where micromark is superlinear (paragraphs and lists)", () => {
    const input =
      "Texte [[3F2A9C14E07B5D21|lien]] fin\n\n- [ ] tâche ![[7A1C4E90B3F2D615]]\n\n".repeat(2_000);
    const plain = createMarkdownParserWithout();
    plain.parse(input); // warm-up

    const timePlain = time(() => plain.parse(input));
    const timeErable = time(() => analyzeMarkdown(input));

    expect(timeErable).toBeLessThan(Math.max(timePlain, 5) * 2);
  }, 120_000);

  it("stays linear on a hostile line of unterminated labels", () => {
    const hostile = "[[3F2A9C14E07B5D21|".repeat(20_000);
    const start = performance.now();
    expect(analyzeMarkdown(hostile).references).toEqual([]);
    expect(performance.now() - start).toBeLessThan(10_000);
  }, 30_000);
});

describe("raw HTML (F04.R5)", () => {
  it("produces no html node: tags are plain text", () => {
    const tree = parseMarkdown('<img src=x onerror="window.__xss=1">\n\n<script>alert(1)</script>');
    const types: string[] = [];
    const walk = (node: Nodes) => {
      types.push(node.type);
      if ("children" in node) {
        node.children.forEach((c) => {
          walk(c);
        });
      }
    };
    walk(tree);

    expect(types).not.toContain("html");
    expect(analyzeMarkdown('<img src=x onerror="window.__xss=1">').plainText).toBe(
      '<img src=x onerror="window.__xss=1">',
    );
  });
});
