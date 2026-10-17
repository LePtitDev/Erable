import { describe, expect, it } from "vitest";
import { analyzeMarkdown } from "./analyze";

// Fuzzing of the Markdown analysis (docs/testing.md §12, M5-04), twin of MarkdownFuzzTests.cs: random inputs made of the
// tokens that matter to the grammar never throw and cost time linear in their size. A short run belongs to the regular
// suite; the weekly workflow sets VITE_ERABLE_FUZZ_SECONDS=600. A failing input is printed: add it to
// spec/fixtures/markdown/edge-cases.json.

const tokens = [
  "[[",
  "]]",
  "![[",
  "[",
  "]",
  "(",
  ")",
  "![",
  "](",
  "|",
  "#",
  "##",
  "###### ",
  "# ",
  "\n",
  "\n\n",
  "\r\n",
  " ",
  "  ",
  "    ",
  "\t",
  "`",
  "``",
  "```",
  "~~~",
  "*",
  "**",
  "_",
  "__",
  "~~",
  "~",
  ">",
  "> ",
  "- ",
  "* ",
  "1. ",
  "- [ ] ",
  "- [x] ",
  "<",
  "<script>",
  "<!--",
  "&amp;",
  "&#x0;",
  "\\",
  "\\[",
  "---",
  "| a | b |",
  "|---|---|",
  "{#",
  "}",
  "{width=",
  "{",
  "3F2A9C14E07B5D21",
  "0000000000000000",
  "3F2A9C14E07B5D2",
  "ABCDEFGHIJKLMNOP",
  "#section",
  "é",
  "—",
  "日本語",
  "😀",
  "\ud83d",
  "\u0000",
  "‮",
  "http://example.org/",
  "[[3F2A9C14E07B5D21]]",
  "![[3F2A9C14E07B5D21|label]]",
  "[[3F2A9C14E07B5D21#h|a]]",
];

const pathological = [
  "[[",
  "![[",
  "[",
  "]",
  "`",
  "*",
  "_",
  "> ",
  "- ",
  "<",
  "[[3F2A9C14E07B5D21|",
  "[a](",
  "\\",
  "~",
  "|",
  "# ",
  "1. ",
];

const seconds = Number(import.meta.env["VITE_ERABLE_FUZZ_SECONDS"] ?? "1");
const budgetMs = (Number.isFinite(seconds) && seconds > 0 ? seconds : 1) * 1000;

/** Deterministic generator (mulberry32): a failure of the regular suite replays. */
function random(seed: number): () => number {
  let state = seed;
  return () => {
    state = (state + 0x6d2b79f5) | 0;
    let t = Math.imul(state ^ (state >>> 15), 1 | state);
    t = (t + Math.imul(t ^ (t >>> 7), 61 | t)) ^ t;
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

function elapsed(markdown: string): number {
  const start = performance.now();
  analyzeMarkdown(markdown);
  return performance.now() - start;
}

const best = (markdown: string) =>
  Math.min(elapsed(markdown), elapsed(markdown), elapsed(markdown));

describe("markdown fuzzing", () => {
  it(
    "never throws and keeps reference offsets inside the source",
    { timeout: budgetMs + 30_000 },
    () => {
      const next = random(Number(import.meta.env["VITE_ERABLE_FUZZ_SEED"] ?? "42"));
      const deadline = performance.now() + budgetMs;
      let inputs = 0;
      do {
        const length = 1 + Math.floor(next() * 59);
        let markdown = "";
        for (let i = 0; i < length; i++)
          markdown += tokens[Math.floor(next() * tokens.length)] ?? "";
        let found;
        try {
          found = analyzeMarkdown(markdown).references;
        } catch (error) {
          throw new Error(`analyzeMarkdown threw on ${JSON.stringify(markdown)}`, { cause: error });
        }
        for (const reference of found) {
          const coherent =
            reference.start >= 0 &&
            reference.start < reference.end &&
            reference.end <= markdown.length &&
            markdown.slice(reference.start, reference.end).includes("[[");
          expect(
            coherent,
            `reference ${String(reference.start)}..${String(reference.end)} in ${JSON.stringify(markdown)}`,
          ).toBe(true);
        }
        inputs++;
      } while (performance.now() < deadline);
      expect(inputs).toBeGreaterThan(0);
    },
  );

  it("takes time linear in the size on pathological repetitions", { timeout: 120_000 }, () => {
    const small = 5_000;
    const large = 8 * small;
    for (const token of pathological) {
      elapsed(token.repeat(500));
      const fast = best(token.repeat(small));
      const slow = best(token.repeat(large));
      // 8× the size: about 8× the time. Quadratic would be 64×. The floor keeps timer noise out.
      const allowed = Math.max(fast * 8 * 4, 250);
      expect(
        slow,
        `${JSON.stringify(token)} × ${String(large)}: ${slow.toFixed(0)} ms vs ${fast.toFixed(0)} ms`,
      ).toBeLessThan(allowed);
    }
  });
});
