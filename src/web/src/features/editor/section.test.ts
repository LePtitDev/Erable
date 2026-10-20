import { describe, expect, it } from "vitest";
import { sectionAt } from "./section";

const page = [
  "Ilsa tient le comptoir.", // 1
  "", // 2
  "## Présentation", // 3
  "", // 4
  "Méfiante envers les soldats.", // 5
  "", // 6
  "## Histoire {#histoire}", // 7
  "", // 8
  "Née à Hautbois.", // 9
  "", // 10
  "### Les années de cartographe", // 11
  "", // 12
  "Ses relevés.", // 13
].join("\n");

describe("sectionAt", () => {
  it("is null above the first heading", () => {
    expect(sectionAt(page, 1)).toBeNull();
  });

  it("is the heading itself when the cursor is on it", () => {
    expect(sectionAt(page, 3)).toBe("Présentation");
  });

  it("is the closest heading above the cursor, not an enclosing one", () => {
    expect(sectionAt(page, 5)).toBe("Présentation");
    expect(sectionAt(page, 13)).toBe("Les années de cartographe");
  });

  it("drops the explicit anchor of a title", () => {
    expect(sectionAt(page, 9)).toBe("Histoire");
  });

  it("ignores headings in code fences", () => {
    const text = ["## Code", "```", "## Not a title", "```", "after"].join("\n");

    expect(sectionAt(text, 5)).toBe("Code");
  });

  it("ignores a hash that is not a heading", () => {
    expect(sectionAt("#hashtag\n\ntext", 3)).toBeNull();
    expect(sectionAt("    ## indented code\n", 1)).toBeNull();
  });

  it("accepts closing hashes and a line past the end", () => {
    expect(sectionAt("## Titre ##\n", 99)).toBe("Titre");
  });
});
