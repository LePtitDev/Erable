/**
 * Frozen IDs of the reference project « Chroniques d'Aube » (docs/features.md §2.1), as written by
 * tests/Erable.Testing/Reference/Chroniques.cs. Tests use them to check that references survive renames and moves.
 */
export const chroniques = {
  name: "Chroniques d'Aube",
  ilsa: "3F2A9C14E07B5D21",
  orsen: "7A1C4E90B3F2D615",
  dragon: "C47D2B9E0F6A1388",
  hautbois: "5B19C0E2A4D7F311",
  colGeleLieu: "8C3E1F7A90B2D446",
  ordreDuGivre: "9E04B6A1C8D2F570",
  colGeleQuete: "2D8F5A7C1B9E0463",
} as const;

/** Default test clock: 13 October 2026, 10:00 Europe/Paris (docs/features.md §1.4). */
export const referenceDate = new Date("2026-10-13T08:00:00.000Z");
