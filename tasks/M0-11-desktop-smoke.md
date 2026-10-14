# M0-11 — Artefacts desktop et test de fumée

- **Statut** : todo
- **Jalon** : M0
- **Dépend de** : M0-07, M0-10
- **Références** : §A-10 (Packaging), §A-12 (M0), §T-10, E2E-16-01, E2E-16-02, P-06

## Objectif

Produire un binaire desktop autonome par OS et vérifier automatiquement qu'il démarre et fonctionne.

## Périmètre

- `dotnet publish` autonome par RID (`win-x64`, `linux-x64`, `osx-arm64`, `osx-x64`) avec la SPA embarquée.
- Trancher P-06 : en M0, archives autonomes ; installeur Velopack en M5-03.
- **Windows** : lancement avec `WEBVIEW2_ADDITIONAL_BROWSER_ARGUMENTS=--remote-debugging-port=…`, Playwright `connectOverCDP`.
- **macOS / Linux** : option `--self-test` : l'app ouvre un projet temporaire, la SPA déroule un scénario scripté, rapporte le résultat au backend, le processus sort avec 0 ou 1 (Linux sous `xvfb`).
- Job CI qui exécute ces fumées sur les artefacts publiés.
- Vérification de fin : plus aucun processus Erable après fermeture.

## Critères d'acceptation

- [ ] `E2E-16-01` (version M0 : démarrage < 3 s et affichage de l'accueil « hello ») vert sur les 3 OS.
- [ ] `E2E-16-02` vert sur le binaire packagé.
- [ ] `--self-test` est absent ou inopérant hors des builds de test, ou ne permet rien de plus qu'un démarrage normal (à justifier).

## Points d'attention

- Le scénario de fumée complet d'E2E-16-01 (projet, page, référence, image, recherche) s'enrichira au fil des jalons : prévoir un scénario extensible.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
