import { describe, expect, it } from "vitest";
import { interfaceLanguages, referenceLanguage, type InterfaceLanguage } from "./languages";
import de from "./locales/de.json";
import en from "./locales/en.json";
import es from "./locales/es.json";
import fr from "./locales/fr.json";
import type { Resources } from "./pseudo";

const locales: Record<InterfaceLanguage, Resources> = { fr, en, de, es };
const pluralSuffix = /_(zero|one|two|few|many|other)$/;

function flatten(resources: Resources, prefix = ""): Map<string, string> {
  const result = new Map<string, string>();
  for (const [key, value] of Object.entries(resources)) {
    const path = prefix + key;
    if (typeof value === "string") result.set(path, value);
    else for (const [k, v] of flatten(value, `${path}.`)) result.set(k, v);
  }
  return result;
}

/** Per base key (plural suffix removed): interpolation parameters and plural forms. */
function describeLocale(resources: Resources) {
  const keys = new Map<string, { params: Set<string>; forms: Set<string> }>();
  for (const [key, text] of flatten(resources)) {
    const base = key.replace(pluralSuffix, "");
    const entry = keys.get(base) ?? { params: new Set<string>(), forms: new Set<string>() };
    for (const match of text.matchAll(/\{\{\s*([\w.]+)/g)) entry.params.add(match[1] ?? "");
    const form = pluralSuffix.exec(key)?.[1];
    if (form !== undefined) entry.forms.add(form);
    keys.set(base, entry);
  }
  return keys;
}

const reference = describeLocale(locales[referenceLanguage]);

describe.each(interfaceLanguages.filter((l) => l !== referenceLanguage))(
  "%s translations",
  (language) => {
    const target = describeLocale(locales[language]);

    it(`has every ${referenceLanguage} key`, () => {
      expect([...reference.keys()].filter((k) => !target.has(k))).toEqual([]);
    });

    it(`has no key missing from ${referenceLanguage}`, () => {
      expect([...target.keys()].filter((k) => !reference.has(k))).toEqual([]);
    });

    it("uses the same interpolation parameters", () => {
      const mismatches = [...reference].flatMap(([key, { params }]) => {
        const other = target.get(key)?.params;
        return other && [...params].sort().join() !== [...other].sort().join() ? [key] : [];
      });
      expect(mismatches).toEqual([]);
    });
  },
);

describe.each(interfaceLanguages)("%s plurals", (language) => {
  it("provides every plural category of the language", () => {
    const categories = new Intl.PluralRules(language).resolvedOptions().pluralCategories;
    const missing = [...describeLocale(locales[language])].flatMap(([key, { forms }]) =>
      forms.size === 0 ? [] : categories.filter((c) => !forms.has(c)).map((c) => `${key}_${c}`),
    );
    expect(missing).toEqual([]);
  });
});
