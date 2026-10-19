# M6-01 — Fondations mobiles et projet de test `mobile`

- **Statut** : todo
- **Jalon** : M6
- **Dépend de** : M5-02
- **Références** : §A-3.1, décision Q11, F18.R3, F18.R4, F18.R8, §T-9.1

## Objectif

Poser ce dont toutes les tâches mobiles ont besoin : un seuil de mise en page unique, des tailles tactiles, une page qui tient dans l'écran des téléphones (barres du navigateur, encoches), le manifeste d'application web, et un projet Playwright qui émule un téléphone.

## Périmètre

- `shared/ui/media.ts` : seuil `narrowMaxWidth = 720`, requêtes `narrowQuery` et `coarseQuery`, hook `useMediaQuery` (et `useIsNarrowScreen`, `useIsCoarsePointer`) ; jsdom : faux par défaut (mise en page large).
- Tokens : `--size-touch` (44 px) appliqué aux contrôles sous `@media (pointer: coarse)` ; `100dvh` au lieu de `100vh` ; marges `env(safe-area-inset-*)` ; `viewport-fit=cover`.
- Coupure des mots : les titres se coupent entre les mots (fini « Vare / nne »).
- Manifeste `manifest.webmanifest` (nom, `start_url: "/"`, `display: standalone`, couleurs du thème), icônes 192 / 512 / maskable dérivées du logo, `<meta name="theme-color">` clair et sombre, `apple-touch-icon`. Servi par le backend avec le bon type MIME, couvert par la CSP.
- Playwright : projet `mobile` (Pixel 7, Chromium) qui exécute seulement `@mobile` ; `@mobile` exclu des autres projets ; `e2e:pr` inclut les `@mobile @pr`. Aides `expectNoHorizontalScroll(page)` et `expectTouchTargets(page)`.

## Hors périmètre

- Service worker, mode hors ligne (F18.R8).

## Critères d'acceptation

- [ ] `E2E-18-11` vert.
- [ ] Vitest : `useMediaQuery` suit les changements de la requête.
- [ ] Test serveur : `/manifest.webmanifest` servi en `application/manifest+json`.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

_À remplir par l'agent._
