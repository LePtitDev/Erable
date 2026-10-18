import { describe, expect, it } from "vitest";
import { AA_NORMAL_TEXT, contrastRatio } from "./contrast";
import tokensCss from "./tokens.css?raw";

type Tokens = Record<string, string>;

function block(selector: string): Tokens {
  const start = tokensCss.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`Block ${selector} not found`);
  const body = tokensCss.slice(start, tokensCss.indexOf("}", start));
  return Object.fromEntries(
    [...body.matchAll(/(--[\w-]+):\s*([^;]+);/g)].map((m) => [m[1] ?? "", (m[2] ?? "").trim()]),
  );
}

const light = block(":root");
const dark = block(':root[data-theme="dark"]');
const system = block('  :root[data-theme="system"]');

const textColors = ["--color-text", "--color-text-muted", "--color-text-faint"];
const surfaces = [
  "--color-bg",
  "--color-surface",
  "--color-sunken",
  "--color-hover",
  "--color-editor-bg",
];
const pairs: [string, string][] = [
  ...textColors.flatMap((fg) => surfaces.map((bg): [string, string] => [fg, bg])),
  ["--color-accent", "--color-surface"],
  ["--color-accent-text", "--color-accent-soft"],
  ["--color-on-accent", "--color-accent"],
  ["--color-danger", "--color-surface"],
  ["--color-danger", "--color-danger-soft"],
  ["--color-warning", "--color-surface"],
  ["--color-warning", "--color-warning-soft"],
  ["--color-text", "--color-highlight"],
  ...["page", "category", "property", "link", "attachment"].map((kind): [string, string] => [
    `--color-kind-${kind}`,
    `--color-kind-${kind}-soft`,
  ]),
];

describe("design tokens", () => {
  it("defines the same dark values for the forced and the system dark theme", () => {
    expect(system).toEqual(dark);
  });

  it("overrides every light color in the dark theme", () => {
    const lightColors = Object.keys(light).filter((k) => k.startsWith("--color-"));
    expect(lightColors.filter((k) => !(k in dark))).toEqual([]);
  });

  describe.each([
    ["light", light],
    ["dark", { ...light, ...dark }],
  ])("%s theme", (_, theme: Tokens) => {
    it.each(pairs)("%s on %s meets WCAG AA for normal text", (fg, bg) => {
      const ratio = contrastRatio(theme[fg] ?? "", theme[bg] ?? "");
      expect(ratio).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
    });
  });
});
