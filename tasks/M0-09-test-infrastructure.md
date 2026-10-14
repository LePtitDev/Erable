# M0-09 — Infrastructure de test

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : M0-01, M0-02, M0-03
- **Références** : §T-2, §T-5, §T-9, §T-16 (M0), features §1, P-10, P-12

## Objectif

Rendre l'écriture d'un test de chaque niveau triviale dès le début, pour que les tâches suivantes n'aient qu'à ajouter des cas.

## Périmètre

- **Spécification partagée** : dossier `spec/fixtures/` ; un lanceur xUnit (`[Theory]` alimenté par les JSON) et un lanceur Vitest qui lisent tous les fichiers d'un dossier. Ajouter un cas = ajouter une ligne JSON.
- **Stockage** : utilitaire de dossier temporaire par test, nettoyé automatiquement.
- **API** : fixture `WebApplicationFactory` avec projet temporaire.
- **Playwright** (`src/web/e2e/`) : configuration Chromium + WebKit (Firefox réservé au nightly), démarrage automatique du backend en mode test, traces conservées en cas d'échec, structure `fixtures/`, `pages/`, `specs/`.
- Convention de titre : `E2E-nn-nn · <English title> @tags` (P-12).
- Script de **traçabilité** : chaque ID `E2E-*` de `docs/features.md` correspond à un test Playwright et inversement. Tant que les jalons ne sont pas atteints, il rapporte les scénarios manquants par jalon sans échouer pour les jalons futurs.
- Un exemple de test de chaque niveau.
- Mettre à jour `CLAUDE.md` §9.

## Critères d'acceptation

- [x] `dotnet test` et `pnpm -C src/web test` exécutent un même fichier de fixture d'exemple.
- [x] `pnpm -C src/web e2e` lance un test Playwright contre le vrai backend en Chromium et WebKit.
- [x] Le script de traçabilité liste les scénarios M0 manquants.

## Points d'attention

- P-10 : n'installer que l'outillage prévu pour M0 en §T-16 ; Stryker, NBomber, captures visuelles arrivent plus tard.
- Aucun `waitForTimeout` : règle ESLint dédiée sur `e2e/`.

## Notes d'implémentation

- **Spécification partagée** : format `spec/fixtures/<dossier>/*.json` = `{ "description", "cases": [{ "name", … }] }`.
  - C# : bibliothèque **`tests/Erable.Testing`** (référencée par les trois projets de test) : `SharedFixtures.Load("dossier")` → `TheoryData<FixtureCase>` ; chaque cas est un test distinct (`FixtureCase` est sérialisable xUnit, affiché `fichier › nom`), `fixture.As<T>()` désérialise.
  - TS : `src/test/fixtures.ts`, `loadFixtures<T>("dossier")` via `import.meta.glob` (aucune dépendance Node dans le code de l'app).
  - Exemple commun `spec/fixtures/example/arithmetic.json`, exécuté par `SharedFixtureExampleTests` (xUnit) **et** `fixtures.example.test.ts` (Vitest). À supprimer quand `ids.json` existera (M1-01).
- **Stockage** : `TempDirectory.Create()` (dossier unique, suppression récursive avec quelques nouvelles tentatives pour les verrous Windows/WAL), testé dans `Erable.Storage.Tests`.
- **API** : `TestBackend` (M0-03/M0-07) reçoit désormais un dossier de données temporaire en plus de la racine web ; `RepositoryPaths` déplacé dans `Erable.Testing`.
- **Playwright 1.64** (`src/web/e2e/`) : `fixtures/` (page objects injectés), `pages/` (`HelloPage`), `specs/`, `support/` (`start-backend.mjs`, `i18n.ts`). Projets **Chromium + WebKit** ; `retries: 0` (un test instable est mis en quarantaine, jamais relancé en silence) ; trace, vidéo et capture conservées en cas d'échec ; locale `fr-FR`, fuseau `Europe/Paris`. Le `webServer` build la SPA puis lance `erable serve` (Debug, `ERABLE_TEST_MODE=1`) sur un dossier de données neuf : le backend sert la SPA buildée, comme en production.
- Les page objects lisent les libellés **par clé i18n** dans `fr.json` (`t("preferences.language")`), conformément à features §1.2.
- Exemple e2e : deux tests de fumée d'infrastructure **sans ID de scénario** (`smoke · …`) : la page « hello » affiche la version du backend, `/_test/ping` répond en mode test. Verts en Chromium et WebKit (≈ 6 s, serveur compris).
- **Règle ESLint** sur `e2e/**` : tout appel `waitForTimeout` est une erreur (vérifié).
- **Traçabilité** `scripts/trace-e2e.mjs` (`pnpm e2e:trace`) : lit les en-têtes de scénario de `docs/features.md` (ignore les scénarios barrés), cherche `test("E2E-nn-nn…` dans `e2e/`, rapporte les manquants par jalon. Échoue si un test porte un ID inconnu ; avec `--enforce M<n>`, échoue aussi s'il manque un scénario des jalons ≤ n. Totaux cohérents avec features §4.1 (117 ; M0 = 2, M1 = 49, M2 = 34…). Aujourd'hui : **M0 manquants = E2E-16-01 et E2E-16-02** (implémentés en M0-11).
- P-12 tranché : features §1.1 et §1.3 passent aux titres anglais précédés de l'ID. P-10 appliqué : rien d'autre que l'outillage M0 (pas de Stryker, NBomber, captures, axe-core).
- **Bug corrigé (test écrit d'abord)** : en environnement `Development`, le backend plantait au démarrage (`DirectoryNotFoundException …\src\Erable.Server\wwwroot`) : le pipeline Razor « static web assets », activé par le contenu SPA lié en M0-03, pointait vers un dossier inexistant. `StaticWebAssetsEnabled=false` dans `Erable.Server.csproj` (la SPA est servie depuis `wwwroot/` par les fichiers statiques classiques). Test de régression `Start_DevelopmentEnvironment_Succeeds`.

**Vérification** : `dotnet test --solution Erable.sln` · `pnpm -C src/web test`, `lint`, `typecheck`, `format:check` · `pnpm -C src/web e2e` (4 tests, Chromium + WebKit) · `pnpm -C src/web e2e:trace`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
