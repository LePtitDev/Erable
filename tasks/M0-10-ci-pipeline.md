# M0-10 — Pipeline CI PR (3 OS)

- **Statut** : review
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

- [x] Le workflow est valide (`actionlint` ou équivalent) et documenté. (Validé localement par schéma ; `actionlint` tourne en CI.)
- [ ] L'utilisateur l'a exécuté sur une branche de test et il est vert (l'agent ne pousse rien : il fournit les instructions). **En attente de l'utilisateur.**

## Points d'attention

- L'agent ne peut pas pousser ni créer de branche : préparer les fichiers, expliquer à l'utilisateur comment déclencher le workflow, puis analyser les logs qu'il rapporte.
- Épingler les actions tierces par SHA.

## Notes d'implémentation

- Workflow **`.github/workflows/pr.yml`**, documenté dans `.github/workflows/README.md`. Déclencheurs : `push`, `pull_request`, `workflow_dispatch` ; `concurrency` par branche (le push et la PR d'une même branche ne tournent pas deux fois) ; `permissions: contents: read`.
- Quatre jobs parallèles :
  1. **actionlint** (`raven-actions/actionlint`) ;
  2. **checks** (Linux, Debug) : `dotnet format --verify-no-changes`, build, tests .NET avec rapports TRX, `typecheck`, `lint`, `format:check`, Vitest, `api:check`, build SPA + `bundle-size` (échoue au-delà de 500 Ko gzip), `e2e:trace` (rapport ; passera en `--enforce M0` après M0-11) ;
  3. **unit** (matrice Windows / Linux / macOS, **Release**) : build et tests .NET — dont le test qui prouve l'absence de `/_test` en Release (point relevé en M0-03) — et Vitest ;
  4. **e2e** (Linux) : `playwright install --with-deps chromium webkit`, tests `@pr` ; rapport et traces Playwright en artefact en cas d'échec.
- Caches : paquets NuGet (`actions/cache`, clé = `Directory.Packages.props` + `*.csproj` + `global.json`), store pnpm (`setup-node` `cache: pnpm`). Pas de fichiers de verrouillage NuGet : à envisager si la reproductibilité des restaurations devient un sujet.
- **Actions épinglées par SHA** (dernières versions vérifiées le 14/10/2026) : checkout v7.0.1, setup-dotnet v6.0.0 (`global-json-file`), setup-node v7.1.0, pnpm/action-setup v6.1.0 (lit `packageManager`), cache v6.1.0, upload-artifact v7.0.2, raven-actions/actionlint v2.2.0. Node 22 (comme le poste de dev).
- `e2e/support/start-backend.mjs` lance désormais Vite par son point d'entrée (`node node_modules/vite/bin/vite.js build`) : indépendant de l'installation de pnpm (Corepack en local, `pnpm/action-setup` en CI).
- **Validation locale** : `@action-validator/cli` 0.6.0 (validation par schéma, lancé hors du dépôt) → valide ; `dotnet format --verify-no-changes` → propre ; toutes les étapes du job *checks* rejouées localement sous Windows et vertes. `actionlint` (binaire Go) n'a pas été lancé localement : c'est le premier job de la CI.
- **Non exécuté sur GitHub** : l'utilisateur a choisi de ne pas pousser la branche. Critère « exécuté sur une branche de test et vert » **en attente**. Pour le déclencher : `git push -u origin m0` (le workflow tourne sur tout push), ou onglet *Actions* → *PR* → *Run workflow*. Me transmettre les journaux en cas d'échec.
- Durée estimée (non mesurée) : *checks* ≈ 6–8 min, *unit* ≈ 4–7 min (macOS le plus lent), *e2e* ≈ 5–7 min (dont ~2 min d'installation des navigateurs) ; en parallèle, sous la cible de 12 min. À confirmer au premier passage.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
