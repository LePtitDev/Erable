# M0-03 — Hôte serveur, `/api/info`, service de la SPA

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : M0-01, M0-02
- **Références** : §A-2, §A-8.1, §A-9, §T-7, features §1.4

## Objectif

Rendre `Erable.Server` démarrable en exécutable autonome comme en bibliothèque hébergée par le desktop, avec les conventions d'API posées dès le départ.

## Périmètre

- Point d'entrée réutilisable (ex. `ErableHost.Build(options)`) utilisé par `Program` (mode serveur) **et** par `Erable.Desktop` (in-process). Options : mode (`Desktop` / `Server`), adresse, port (0 = aléatoire), dossier de données, jeton.
- `GET /api/info` : version de l'application, version d'API (`v1`), `formatVersion` supportée, mode.
- Groupe `/api/v1` ; erreurs en `ProblemDetails` ; JSON camelCase, enums en chaîne.
- Service des fichiers statiques de la SPA buildée, repli sur `index.html` pour les routes client.
- Journalisation structurée ; `TimeProvider` injecté partout.
- Mode test : `ERABLE_TEST_MODE=1` active un groupe `/_test` (vide pour l'instant), **absent des builds Release** (compilation conditionnelle ou assembly séparée).
- Ligne de commande : squelette de `erable serve <dossier> [--port]`.

## Hors périmètre

- Authentification (M4-01), sécurité loopback (M0-07), endpoints métier (M1-05).

## Critères d'acceptation

- [ ] Test `WebApplicationFactory` : `/api/info` répond 200 avec le contrat attendu.
- [ ] Route inconnue sous `/api` → 404 `ProblemDetails` ; route client → `index.html`.
- [ ] Test : en Release, `/_test/*` n'existe pas même avec `ERABLE_TEST_MODE=1`.

## Points d'attention

- Le backend doit pouvoir être hébergé plusieurs fois dans un même processus (tests parallèles) : aucun état statique.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
