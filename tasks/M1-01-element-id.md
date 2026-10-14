# M1-01 — `ElementId` (C# + TS + fixtures)

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M0-09
- **Références** : §A-4, §T-3, §T-4.1, §T-5, F03.R1–R2

## Objectif

Implémenter l'identifiant universel de manière à rendre impossible l'erreur de précision JavaScript.

## Périmètre

**C# (`Erable.Core`)**
- `readonly record struct ElementId(ulong Value)` ; `0` réservé (`ElementId.None`).
- `NewRandom()` via `RandomNumberGenerator` (jamais 0).
- `Parse` / `TryParse` (16 hexa, insensible à la casse), `ToString()` canonique majuscule, `ToDisplayString()` en groupes de 4.
- Conversion `long ↔ ulong` sans perte pour SQLite (`unchecked`).
- `JsonConverter` (chaîne uniquement ; un nombre JSON est rejeté) ; binder de route ; schéma OpenAPI `string` + `pattern`.

**TypeScript**
- `type ElementId = string & { readonly __brand: "ElementId" }` ; `parseElementId()` (retourne `ElementId | null`) ; `formatDisplay()`.
- Le client généré utilise ce type pour tous les champs d'ID (post-traitement des types générés ou format OpenAPI dédié).

**Spécification partagée**
- `spec/fixtures/ids.json` : canoniques, minuscules, espaces, 15/17 caractères, non-hexa, zéro, valeurs extrêmes (`FFFF…`, `8000…`).

## Critères d'acceptation

- [ ] Propriété CsCheck : `Parse(Format(id)) == id` pour tout `ulong ≠ 0` ; `long ↔ ulong` sans perte.
- [ ] Propriété fast-check équivalente côté TS.
- [ ] `ids.json` exécuté par xUnit **et** Vitest.
- [ ] Un ID sérialisé en nombre JSON est rejeté par l'API (test d'intégration).
- [ ] Couverture Core de ce module ≥ 90 %.

## Points d'attention

- Ne jamais exposer `Value` en JSON.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
