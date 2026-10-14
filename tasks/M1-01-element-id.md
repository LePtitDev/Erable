# M1-01 — `ElementId` (C# + TS + fixtures)

- **Statut** : review
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

- [x] Propriété CsCheck : `Parse(Format(id)) == id` pour tout `ulong ≠ 0` ; `long ↔ ulong` sans perte.
- [x] Propriété fast-check équivalente côté TS.
- [x] `ids.json` exécuté par xUnit **et** Vitest.
- [x] Un ID sérialisé en nombre JSON est rejeté par l'API (test d'intégration).
- [x] Couverture Core de ce module ≥ 90 %.

## Points d'attention

- Ne jamais exposer `Value` en JSON.

## Notes d'implémentation

- **C#** (`src/Erable.Core/ElementId.cs`) : `readonly record struct ElementId(ulong)`, `None`, `NewRandom()` (`RandomNumberGenerator`, retirage si 0), `Parse`/`TryParse` (16 hexa ASCII exactement, insensible à la casse : pas d'espace, de signe, de préfixe `0x` ni de chiffre Unicode non ASCII), `ToString()` canonique majuscule, `ToDisplayString()`, `FromInt64`/`ToInt64` (`unchecked`). Implémente `IParsable`/`ISpanParsable` : c'est ce que les Minimal API utilisent pour lier les paramètres de route et de requête (pas de binder à écrire). `ElementIdJsonConverter` posé par attribut sur le type : chaîne uniquement (nombre, `null`, chaîne invalide → `JsonException`), y compris comme clé de dictionnaire.
- **OpenAPI** : transformateur de schéma dans `ErableHost` → `type: string`, `pattern: ^[0-9A-F]{16}$`, `format: element-id`. Le document ne change pas encore (aucun endpoint n'expose d'ID) : l'effet sur l'instantané sera visible et figé en M1-05.
- **TS** (`src/web/src/shared/ids/elementId.ts`) : type marqué, `parseElementId()` (→ forme canonique majuscule ou `null`), `requireElementId()` (pour les constantes et paramètres déjà validés), `formatDisplay()`.
- **Client généré** : `scripts/api-generate.mjs` applique un `transform` openapi-typescript : tout schéma `format: element-id` devient le type `ElementId`, importé dans `schema.ts` seulement s'il est utilisé (sinon `noUnusedLocals` échoue).
- **Fixtures** : `spec/fixtures/ids/ids.json` (20 cas, dont signe, bit de poids fort, > 2^53, chiffres pleine chasse et arabes) ; l'exemple `spec/fixtures/example` et ses deux lanceurs sont supprimés, comme prévu par M0-09.
- **Rejet d'un ID numérique** : vérifié sur les options JSON réellement configurées par l'hôte (`ElementIdJsonTests`) ; le test bout en bout via un endpoint arrive avec les premiers endpoints (M1-05).
- **Couverture** : ajout de `Microsoft.Testing.Extensions.CodeCoverage` 18.12.0 à `Erable.Core.Tests` (`dotnet test --project tests/Erable.Core.Tests --coverage --coverage-output-format cobertura`). `ElementId` : 95 % des lignes, `ElementIdJsonConverter` : 100 %. Le seuil bloquant en CI n'est pas encore branché (voir Propositions).
- Dépendance ajoutée : `fast-check` 4.10.2 (dev, MIT).

**Vérification** : `dotnet test --solution Erable.sln` · `dotnet format --verify-no-changes` · `pnpm -C src/web test`, `typecheck`, `lint`, `format:check`, `api:check`.

## Propositions

- Brancher le seuil de couverture de Core (90 %, §T-12) dans le pipeline PR, maintenant que l'extension de couverture est installée.
