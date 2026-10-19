/**
 * Keys derived from a display name (enum options named in the SPA). Twin of `PropertyKey.FromName` and
 * `PropertyKey.Unique` in `src/Erable.Core/Properties/PropertyType.cs`, checked against `spec/fixtures/rules/keys.json`.
 */

export const maxKeyLength = 64;

/** « Points de vie » → `points_de_vie`, « Âge » → `age`, « 12 travaux » → `p_12_travaux`. */
export function keyFromName(name: string): string {
  let key = "";
  // UTF-16 code units, like the C# twin.
  const decomposed = name.normalize("NFD");
  for (let i = 0; i < decomposed.length; i++) {
    const c = decomposed.charAt(i);
    if (/\p{Mn}/u.test(c)) continue;
    const lower = c.toLowerCase();
    if (/^[a-z0-9]$/.test(lower)) key += lower;
    else if (key.length > 0 && !key.endsWith("_")) key += "_";
  }
  key = key.replace(/^_+|_+$/g, "");
  if (key.length > maxKeyLength) key = key.slice(0, maxKeyLength).replace(/_+$/, "");
  return key.length === 0 || !/^[a-z]/.test(key) ? `p_${key}` : key;
}

/** `key`, or `key_2`, `key_3`… if it is taken (truncated to stay within the maximum length). */
export function uniqueKey(key: string, taken: Iterable<string>): string {
  const used = new Set(taken);
  if (!used.has(key)) return key;
  for (let i = 2; ; i++) {
    const suffix = `_${String(i)}`;
    const candidate =
      (key.length + suffix.length > maxKeyLength
        ? key.slice(0, maxKeyLength - suffix.length)
        : key) + suffix;
    if (!used.has(candidate)) return candidate;
  }
}
