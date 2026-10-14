/**
 * Pseudo-localization (`xx`): accented and ~40 % longer, wrapped in brackets. Makes untranslated strings
 * and truncated layouts obvious. Interpolations (`{{name}}`), nesting (`$t(key)`) and tags are preserved.
 */
const accents: Record<string, string> = {
  a: "á",
  e: "é",
  i: "í",
  o: "ö",
  u: "ü",
  y: "ý",
  c: "ç",
  n: "ñ",
  A: "Å",
  E: "É",
  I: "Î",
  O: "Ø",
  U: "Ü",
  Y: "Ý",
  C: "Ç",
  N: "Ñ",
};

const protectedPattern = /(\{\{[^}]*\}\}|\$t\([^)]*\)|<[^>]*>)/g;

export function pseudoLocalize(text: string): string {
  const parts = text.split(protectedPattern);
  const visible = parts.filter((_, i) => i % 2 === 0).join("");
  const padding = "~".repeat(Math.ceil(visible.length * 0.4));
  const body = parts
    .map((part, i) => (i % 2 === 1 ? part : part.replace(/[a-zA-Z]/g, (c) => accents[c] ?? c)))
    .join("");
  return `[${body}${padding}]`;
}

export type Resources = { readonly [key: string]: string | Resources };

export function pseudoLocalizeResources(resources: Resources): Resources {
  return Object.fromEntries(
    Object.entries(resources).map(([key, value]) => [
      key,
      typeof value === "string" ? pseudoLocalize(value) : pseudoLocalizeResources(value),
    ]),
  );
}
