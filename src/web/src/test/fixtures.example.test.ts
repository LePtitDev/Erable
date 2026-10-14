import { describe, expect, it } from "vitest";
import { loadFixtures } from "./fixtures";

// Example of the shared specification runner: the same file is executed by
// tests/Erable.Core.Tests/SharedFixtureExampleTests.cs. Delete with spec/fixtures/example (M1-01).
interface SumCase {
  readonly a: number;
  readonly b: number;
  readonly sum: number;
}

describe("shared fixtures example", () => {
  it.each(loadFixtures<SumCase>("example"))("$file › $name", ({ a, b, sum }) => {
    expect(a + b).toBe(sum);
  });
});
