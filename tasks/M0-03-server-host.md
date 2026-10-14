# M0-03 — Hôte serveur, `/api/info`, service de la SPA

- **Statut** : review
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

- [x] Test `WebApplicationFactory` : `/api/info` répond 200 avec le contrat attendu.
- [x] Route inconnue sous `/api` → 404 `ProblemDetails` ; route client → `index.html`.
- [x] Test : en Release, `/_test/*` n'existe pas même avec `ERABLE_TEST_MODE=1`.

## Points d'attention

- Le backend doit pouvoir être hébergé plusieurs fois dans un même processus (tests parallèles) : aucun état statique.

## Notes d'implémentation

- **Point d'entrée** : `ErableHost.Build(ErableHostOptions, Action<WebApplicationBuilder>?)` → `WebApplication` non démarrée ; `ErableHost.GetListeningUri(app)` donne le port effectif (port 0). Aucun état statique : deux hôtes coexistent dans un processus (testé).
- `ErableHostOptions` : `Mode`, `Address`, `Port`, `DataDirectory`, `WebRootPath`, `SessionToken` (utilisé en M0-07), `TestMode`, `EnvironmentName`, `TimeProvider` (enregistré en singleton ; les tests le remplacent).
- `GET /api/info` → `{ appVersion, apiVersion: "v1", formatVersion, mode }`. `ProjectFormat.CurrentVersion = 1` vit dans Core. Enums sérialisés en chaîne **camelCase** (`"desktop"`).
- Groupe `/api/v1` réservé (vide). Erreurs : `AddProblemDetails` + `UseExceptionHandler` + `UseStatusCodePages`. Repli `404 ProblemDetails` pour `/api/**`, `/_test/**`, `/hubs/**` ; repli SPA `index.html` pour tout le reste.
- Cache HTTP : `/assets/*` (noms hachés par Vite) `immutable`, le reste `no-cache` (important pour les mises à jour desktop).
- **`/_test` absent en Release** : constante `ERABLE_TEST_ENDPOINTS` définie hors configuration `Release`, code sous `#if`. Testé dans les deux configurations : `dotnet test -c Release` vérifie que `/_test/ping` répond 404 même avec `TestMode = true` (le test dédié est ignoré en Debug). **La CI doit exécuter les tests serveur en Release** (M0-10).
- **Écart : `TestServer` piloté par `ErableHost.Build` au lieu de `WebApplicationFactory<Program>`.** `WebApplicationFactory` intercepte `Program` et ne passe pas d'options typées ; utiliser le même point d'entrée que le desktop teste le vrai assemblage (`WebApplicationFactory` n'est qu'une enveloppe de `TestServer`). Utilitaire `TestBackend` dans `tests/Erable.Server.Tests/Infrastructure/`.
- CLI : **System.CommandLine 2.0.13** (GA, maintenu par Microsoft) pour `serve` aujourd'hui et `doctor` en M1-16 (aide, validation, sous-commandes). `erable serve <dossier> [--port 5080] [--address 127.0.0.1]`. **Choix prudent : écoute par défaut sur la boucle locale** tant qu'il n'y a pas de comptes (M4) ; `--address 0.0.0.0` pour le LAN.
- La SPA buildée (`src/web/dist`, hors `.map`) est copiée dans `wwwroot/` du serveur au build et à la publication si elle existe.
- Journalisation : fournisseurs par défaut d'ASP.NET Core (modèles de messages structurés ; formateur JSON activable par configuration `Logging:Console:FormatterName=json`).

**Vérification** : `dotnet build Erable.sln` (0 avertissement) · `dotnet test --solution Erable.sln` et `-c Release` · `erable serve` lancé et sondé (`/api/info` 200, `/api/nope` 404 problem+json, `/_test/ping` 204 avec `ERABLE_TEST_MODE=1`).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
