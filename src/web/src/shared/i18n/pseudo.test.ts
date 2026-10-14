import { describe, expect, it } from "vitest";
import { pseudoLocalize, pseudoLocalizeResources } from "./pseudo";

describe("pseudoLocalize", () => {
  it("accents letters, brackets the text and makes it about 40 % longer", () => {
    const result = pseudoLocalize("Explorer");
    expect(result).toBe("[Éxplörér~~~~]");
  });

  it("preserves interpolations, nesting and tags", () => {
    const result = pseudoLocalize("Hello {{name}}, $t(app.name) <b>now</b>");
    expect(result).toContain("{{name}}");
    expect(result).toContain("$t(app.name)");
    expect(result).toContain("<b>");
    expect(result).toContain("</b>");
  });

  it("transforms nested resources", () => {
    expect(pseudoLocalizeResources({ a: { b: "ok" } })).toEqual({ a: { b: "[ök~]" } });
  });
});
