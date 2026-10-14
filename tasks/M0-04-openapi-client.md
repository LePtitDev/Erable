# M0-04 — OpenAPI, client TS généré, contrôles de dérive

- **Statut** : todo
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

- [ ] La SPA appelle `/api/info` via le client généré.
- [ ] Modifier un DTO sans régénérer fait échouer le script de vérification.
- [ ] Commandes ajoutées à `CLAUDE.md` §9.

## Points d'attention

- Vérifier la qualité de l'OpenAPI natif .NET 10 (records, enums, nullables). S'il est insuffisant, **proposer** une alternative (Swashbuckle, NSwag) avant de l'adopter.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
