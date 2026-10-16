import fr from "../../src/shared/i18n/locales/fr.json" with { type: "json" };

type Resources = { readonly [key: string]: string | Resources };

const resources: Resources = fr;

/**
 * French labels, looked up by i18n key (docs/features.md §1.2): page objects never hard-code UI strings,
 * so a wording change in fr.json does not break the tests.
 */
export function t(key: string, values: Record<string, string | number> = {}): string {
  const text = key
    .split(".")
    .reduce<string | Resources | undefined>(
      (node, part) => (typeof node === "object" ? node[part] : undefined),
      resources,
    );
  if (typeof text !== "string") {
    throw new Error(`Unknown i18n key: ${key}`);
  }
  return text.replace(/\{\{\s*(\w+)[^}]*\}\}/g, (_, name: string) => String(values[name] ?? ""));
}

/** A plural key (`key_one` for 1, `key_other` otherwise), like i18next resolves them in French for small counts. */
export function tp(
  key: string,
  count: number,
  values: Record<string, string | number> = {},
): string {
  return t(`${key}_${count === 1 ? "one" : "other"}`, { count, ...values });
}
