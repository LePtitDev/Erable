# M0-10 — Pipeline CI PR (3 OS)

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : M0-09
- **Références** : §T-13, §A-10 (CI matricielle)

## Objectif

Chaque PR est vérifiée automatiquement sur Windows, Linux et macOS.

## Périmètre

- GitHub Actions, workflow `pr.yml` :
  - lint + typage C# et TS ;
  - build + tests unitaires C# et TS sur `windows-latest`, `ubuntu-latest`, `macos-latest` ;
  - tests de stockage et d'API ;
  - vérification du client OpenAPI et de l'instantané (M0-04) ;
  - e2e Playwright Chromium + WebKit (Linux) ;
  - taille du bundle.
- Caches NuGet et pnpm ; artefacts : traces Playwright, rapports de tests.
- Durée cible < 12 min ; signaler si elle est dépassée.

## Hors périmètre

- Nightly et release (M5-03, M5-04).

## Critères d'acceptation

- [ ] Le workflow est valide (`actionlint` ou équivalent) et documenté.
- [ ] L'utilisateur l'a exécuté sur une branche de test et il est vert (l'agent ne pousse rien : il fournit les instructions).

## Points d'attention

- L'agent ne peut pas pousser ni créer de branche : préparer les fichiers, expliquer à l'utilisateur comment déclencher le workflow, puis analyser les logs qu'il rapporte.
- Épingler les actions tierces par SHA.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
