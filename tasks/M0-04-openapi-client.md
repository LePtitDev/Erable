# M0-04 — OpenAPI, client TS généré, contrôles de dérive

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : M0-03
- **Références** : §A-3.1, §T-3, §T-7 (contrat)

## Objectif

Garantir que front et back ne divergent jamais silencieusement.

## Périmètre

- Génération du document OpenAPI (ASP.NET Core natif), exporté dans un fichier versionné (ex. `src/web/src/api/openapi.json`) par une commande dédiée.
- `openapi-typescript` + `openapi-fetch` : types générés et un wrapper unique qui gère l'URL de base, le jeton (desktop) et les erreurs `ProblemDetails`.
- Test **Verify** : instantané du document OpenAPI ; tout changement apparaît en diff.
- Script de vérification pour la CI : régénère et échoue si le fichier versionné diffère.
- Préparer l'exposition de `ElementId` en `string` avec `pattern: ^[0-9A-F]{16}$` (implémenté en M1-01).

## Critères d'acceptation

- [x] La SPA appelle `/api/info` via le client généré.
- [x] Modifier un DTO sans régénérer fait échouer le script de vérification.
- [x] Commandes ajoutées à `CLAUDE.md` §9.

## Points d'attention

- Vérifier la qualité de l'OpenAPI natif .NET 10 (records, enums, nullables). S'il est insuffisant, **proposer** une alternative (Swashbuckle, NSwag) avant de l'adopter.

## Notes d'implémentation

- **OpenAPI natif .NET 10 retenu** (`Microsoft.AspNetCore.OpenApi` 10.0.13, OpenAPI 3.1) : records, `required`, enums et nullables corrects après deux réglages :
  - `JsonNumberHandling.Strict` : la valeur web par défaut (lecture des nombres depuis des chaînes) produisait `type: [integer, string]` ;
  - transformateur de schéma ajoutant `type: string` aux enums (sérialisés en chaîne camelCase).
  - `servers` vidé (le port aléatoire rendrait le document instable). Document servi sur `/api/openapi/v1.json` (sous `/api`, donc couvert plus tard par le jeton desktop).
- Export : sous-commande cachée `erable openapi --output <fichier>` (démarre l'hôte sur `127.0.0.1:0`, lit le document, l'écrit avec des fins de ligne LF).
- Côté SPA : `src/api/openapi.json` + `src/api/schema.ts` **générés** (openapi-typescript 7.13, `alphabetize`, formatés par Prettier ; exclus d'ESLint), wrapper unique `src/shared/api/client.ts` (openapi-fetch 0.17) : URL de base = origine courante, `ApiError` porteur du `ProblemDetails`, `unwrap()`. Le jeton desktop sera ajouté par M0-07. `fetch` est résolu à l'appel (sinon MSW ne peut pas l'intercepter).
- La page « hello » appelle `/api/info` via TanStack Query (`apiInfoQuery`).
- **Point à challenger — licence de Verify.** *Constat* : Verify 33.x fait échouer le build (`SponsorCheck SC021`) tant qu'une propriété de licence n'est pas posée : parrainage payant, ou exemption déclarée (open source sans revenu, revenu < 10 k$…) à renouveler tous les 12 mois. *Impact* : engagement juridique que l'agent ne peut pas prendre au nom de l'utilisateur. *Option retenue (prudente, ~30 lignes)* : instantané maison — `OpenApiTests` compare le document vivant (`JsonNode.DeepEquals`) au fichier versionné `src/web/src/api/openapi.json`, écrit `openapi.received.json` (ignoré par Git) et échoue avec la commande à lancer. Avantage : un seul fichier sert d'instantané **et** de source du client, au lieu de deux. *Alternative* : réintroduire Verify si l'utilisateur pose `Verify_SponsorshipExemption` ou parraine. Écart à reporter dans `docs/testing.md` §7 (« bibliothèque Verify ») après décision.
- **Contrôle de dérive** prouvé : ajouter un champ à `ApiInfo` sans régénérer fait échouer `pnpm api:check` (les deux fichiers signalés) **et** `OpenApiTests`.
- MSW 3.0 + **openapi-msw 2.1** installés dès maintenant (handlers typés par `paths`), `onUnhandledFrame: "error"` ; utilitaire `renderWithProviders`. MSW 3 a renommé `onUnhandledRequest` en `onUnhandledFrame`.
- openapi-typescript déclare `typescript ^5` en pair : autorisé pour TS 6 via `pnpm.peerDependencyRules` (fonctionne).
- **`ElementId` (préparation pour M1-01)** : le générateur ne sait pas décrire un type porté par un `JsonConverter` maison (il produirait un schéma vide). M1-01 ajoutera un transformateur de schéma : si `context.JsonTypeInfo.Type == typeof(ElementId)` → `type: string`, `pattern: ^[0-9A-F]{16}$`, et le test de contrat le figera.

**Vérification** : `dotnet test --solution Erable.sln` (12 tests) · `pnpm -C src/web api:check`, `typecheck`, `lint`, `test` (3), `format:check`, `build`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
