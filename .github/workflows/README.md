# CI

| Workflow | Déclencheur | Contenu |
|---|---|---|
| `pr.yml` | chaque push, chaque pull request, manuel (`workflow_dispatch`) | voir ci-dessous |

`pr.yml` (cible < 12 min, docs/testing.md §13), quatre jobs en parallèle :

1. **Workflow lint** — `actionlint` sur `.github/workflows/`.
2. **Checks** (Linux, Debug) — `dotnet format --verify-no-changes`, build (analyseurs, avertissements = erreurs), tests .NET (Core, Storage, API) ; `typecheck`, `lint` (ESLint + stylelint), `format:check`, Vitest, contrat API (`api:check`), build de la SPA + budget du bundle (500 Ko gzip), traçabilité E2E.
3. **Unit** (Windows, Linux, macOS, Release) — build et tests .NET en Release (prouve l'absence de `/_test`), Vitest.
4. **E2E** (Linux) — Playwright Chromium + WebKit, tests `@pr`, contre le vrai backend.
5. **Desktop** (win-x64, linux-x64, osx-arm64, osx-x64) — publication autonome + archive (artefact `erable-desktop-<rid>`), puis fumée sur le binaire publié : Playwright via CDP + `--self-test` sous Windows, `--self-test` sous Linux (`xvfb`) et macOS arm64 ; vérifie qu'aucun processus ne subsiste. `osx-x64` est publié sans fumée.

Artefacts : rapports TRX de chaque job ; rapport et traces Playwright en cas d'échec (14 jours).
Les actions tierces sont épinglées par SHA (tag en commentaire).

Déclencher sur une branche de test : `git push -u origin <branche>`, ou onglet *Actions* → *PR* → *Run workflow*.
