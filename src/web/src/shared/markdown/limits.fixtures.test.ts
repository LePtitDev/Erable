import { describe, expect, it } from "vitest";
import { loadFixtures } from "../../test/fixtures";
import { analyzeMarkdown } from "./analyze";
import { checkMarkdownLimits, markdownLimits } from "./limits";

// spec/fixtures/limits, shared with tests/Erable.Core.Tests/Markdown/MarkdownLimitsTests.cs.

interface LimitCase {
  readonly md: string;
  readonly unit: string;
  readonly count: number;
  readonly rule: string | null;
  readonly line: number;
}

describe("markdown limits (limits.json)", () => {
  it.each(loadFixtures<LimitCase>("limits"))("$file › $name", (c) => {
    const violation = checkMarkdownLimits(c.md + c.unit.repeat(c.count));

    expect(violation?.rule ?? null).toBe(c.rule);
    expect(violation?.line ?? 0).toBe(c.line);
  });

  // The point of the limits: whatever they let through, the analysis stays fast and does not throw.
  it.each(["[a](", "> ", "[", "1. ", "]"])("analyzes the largest accepted %j quickly", (token) => {
    let markdown = token.repeat(Math.floor(markdownLimits.maxLineLength / token.length));
    if (checkMarkdownLimits(markdown) !== null) markdown = token.repeat(markdownLimits.maxNesting);
    expect(checkMarkdownLimits(markdown)).toBeNull();

    const start = performance.now();
    analyzeMarkdown(markdown);

    expect(performance.now() - start).toBeLessThan(2000);
  });
});
