/**
 * Interface languages (F15.R1). They are independent of the *content* language, which belongs to the
 * project (`erable.json`, decision Q1) and is set with a `lang` attribute on the content container.
 */
export const interfaceLanguages = ["fr", "en", "de", "es"] as const;
export type InterfaceLanguage = (typeof interfaceLanguages)[number];

/** Pseudo-locale for layout and missing-translation checks (docs/testing.md §8.2), development and end-to-end builds only. */
export const pseudoLanguage = "xx";

export const referenceLanguage: InterfaceLanguage = "fr";

/** Language names are shown in their own language, so they are data, not translations. */
export const nativeNames: Record<InterfaceLanguage, string> = {
  fr: "Français",
  en: "English",
  de: "Deutsch",
  es: "Español",
};

export function isInterfaceLanguage(value: string): value is InterfaceLanguage {
  return (interfaceLanguages as readonly string[]).includes(value);
}

/** First supported language among the candidates (BCP 47 tags such as `en-GB`), else the reference language. */
export function pickLanguage(candidates: readonly (string | undefined)[]): InterfaceLanguage {
  for (const candidate of candidates) {
    const base = candidate?.toLowerCase().split("-")[0];
    if (base !== undefined && isInterfaceLanguage(base)) {
      return base;
    }
  }
  return referenceLanguage;
}
