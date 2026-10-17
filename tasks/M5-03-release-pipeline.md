# M5-03 — Velopack, signature, pipeline release

- **Statut** : in-progress
- **Jalon** : M5
- **Dépend de** : M0-11
- **Références** : §A-10 (Packaging), §A-11.4 (coûts), §T-10, §T-13 (Release), P-06

## Objectif

Une version 1.0 installable et qui se met à jour seule sur les trois OS.

## Périmètre

- Velopack : installeurs Windows, macOS, AppImage Linux ; canal de mise à jour ; vérification de mise à jour au démarrage (désactivable).
- Workflow `release.yml` sur tag `v*` : build, packaging, fumée desktop sur les **installeurs**, migration de tous les projets `legacy/`, image Docker + scan, publication.
- Signature : préparer l'intégration (Azure Trusted Signing, notarisation Apple) derrière des secrets ; **ne rien acheter ni créer de compte** — lister à l'utilisateur ce qu'il doit fournir (§A-11.4).
- Notes de version générées.

## Critères d'acceptation

- [ ] Une release de test (tag créé par l'utilisateur) produit des installeurs fonctionnels sur les 3 OS.
- [ ] Mise à jour d'une version N vers N+1 vérifiée.

## Notes d'implémentation

- **Velopack 1.2.161** (`Directory.Packages.props`) dans `Erable.Desktop` ; `vpk` 1.2.161 en outil local (`.config/dotnet-tools.json`, `dotnet tool restore`). `VelopackApp.Build().Run()` est la première instruction de `Main`.
- **Mise à jour** (`AppUpdater`) : au démarrage, hors tests et `--self-test`, uniquement si l'application est installée ; téléchargement en arrière-plan, installation à la fermeture suivante (`WaitExitThenApplyUpdates`), jamais en session. Désactivable par `ERABLE_DISABLE_UPDATES=1` ou `desktop.json` (`checkForUpdates: false`) dans le profil. `ERABLE_UPDATE_SOURCE` (dossier ou URL) remplace GitHub pour vérifier N → N+1 sans publier.
- **Scripts** : `build/pack-desktop.mjs <rid> --version x.y.z` (publish + `vpk pack`, signature pilotée par variables d'environnement) ; `build/release-notes.mjs <tag>` (notes depuis les Conventional Commits).
- **Workflow `release.yml`** : `legacy` → `notes` → `package` ×4 (installeur, fumée sur l'installé) + `image` (scan Trivy, push GHCR) → `publish` (brouillon → publié). Aucune action tierce nouvelle (pas de SHA inventé) : `gh` et `docker` en ligne de commande.
- **Signature** : rien acheté ni créé. `docs/release.md` liste ce que l'utilisateur doit fournir (Azure Trusted Signing, Apple Developer, secrets). Le câblage de l'authentification Azure et de l'import du trousseau Apple n'est pas écrit (comptes inexistants).
- **Non vérifié** : seul le build de `Erable.Desktop` a été exécuté ; ni `vpk pack`, ni le workflow, ni la mise à jour N → N+1 n'ont tourné. Les critères « release de test sur 3 OS » et « mise à jour N → N+1 » restent donc ouverts : ils demandent un tag créé par l'utilisateur et la procédure de `docs/release.md`. Fumée macOS sur l'installé : bundle de l'archive portable (le `.pkg` s'installe avec `sudo`, non testé) ; osx-x64 sans fumée.
- Message de commit : `feat(desktop): add Velopack packaging, auto-update and release pipeline (M5-03)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
