import i18next, { type i18n as I18n } from "i18next";
import { initReactI18next } from "react-i18next";
import {
  interfaceLanguages,
  isInterfaceLanguage,
  pickLanguage,
  pseudoLanguage,
  referenceLanguage,
} from "./languages";
import fr from "./locales/fr.json";
import { pseudoLocalizeResources } from "./pseudo";

declare module "i18next" {
  interface CustomTypeOptions {
    // Keys and interpolation parameters are type-checked against the reference language.
    resources: { translation: typeof fr };
    returnNull: false;
  }
}

/** Non-reference languages are loaded on demand: one chunk per language. */
const loaders: Record<string, () => Promise<{ default: object }>> = {
  en: () => import("./locales/en.json"),
  de: () => import("./locales/de.json"),
  es: () => import("./locales/es.json"),
};

export async function loadLanguage(instance: I18n, language: string): Promise<void> {
  if (instance.hasResourceBundle(language, "translation")) {
    return;
  }
  const resources =
    language === pseudoLanguage
      ? pseudoLocalizeResources(fr)
      : (await loaders[language]?.())?.default;
  if (resources !== undefined) {
    instance.addResourceBundle(language, "translation", resources);
  }
}

/** Changes the interface language without reloading the page. */
export async function changeLanguage(language: string, instance: I18n = i18next): Promise<void> {
  await loadLanguage(instance, language);
  await instance.changeLanguage(language);
}

/**
 * The pseudo-locale exists in development and in the build the end-to-end tests use (`VITE_PSEUDO_LOCALE=1`, set by
 * e2e/support/start-backend.mjs); never in a release build.
 */
const pseudoEnabled = import.meta.env.DEV || import.meta.env["VITE_PSEUDO_LOCALE"] === "1";

/** Where the visual tests ask for the pseudo-locale without saving it as a preference (the backend refuses it). */
const forcedLocaleKey = "erable:locale";

function forcedLocale(): string | null {
  try {
    return window.sessionStorage.getItem(forcedLocaleKey);
  } catch {
    return null;
  }
}

export function availableLanguages(): string[] {
  return pseudoEnabled ? [...interfaceLanguages, pseudoLanguage] : [...interfaceLanguages];
}

/**
 * Initial language: `<html lang>` (written by the backend from the user's preference, decision Q7),
 * then the browser languages, then French.
 */
export function initialLanguage(): string {
  const fromDocument = document.documentElement.lang;
  if (pseudoEnabled && (fromDocument === pseudoLanguage || forcedLocale() === pseudoLanguage)) {
    return pseudoLanguage;
  }
  return isInterfaceLanguage(fromDocument)
    ? fromDocument
    : pickLanguage([...navigator.languages, navigator.language]);
}

export async function createI18n(language: string): Promise<I18n> {
  const instance = i18next.createInstance();
  await instance.use(initReactI18next).init({
    lng: referenceLanguage,
    fallbackLng: referenceLanguage,
    supportedLngs: availableLanguages(),
    resources: { [referenceLanguage]: { translation: fr } },
    partialBundledLanguages: true,
    interpolation: { escapeValue: false }, // React already escapes
    returnNull: false,
  });
  instance.on("languageChanged", (lng) => {
    document.documentElement.lang = lng === pseudoLanguage ? referenceLanguage : lng;
  });
  await changeLanguage(language, instance);
  return instance;
}
