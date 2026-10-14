# M0-09 — Infrastructure de test

- **Statut** : todo
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

- [ ] `dotnet test` et `pnpm -C src/web test` exécutent un même fichier de fixture d'exemple.
- [ ] `pnpm -C src/web e2e` lance un test Playwright contre le vrai backend en Chromium et WebKit.
- [ ] Le script de traçabilité liste les scénarios M0 manquants.

## Points d'attention

- P-10 : n'installer que l'outillage prévu pour M0 en §T-16 ; Stryker, NBomber, captures visuelles arrivent plus tard.
- Aucun `waitForTimeout` : règle ESLint dédiée sur `e2e/`.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
