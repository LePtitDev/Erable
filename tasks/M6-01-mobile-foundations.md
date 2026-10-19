# M6-01 — Fondations mobiles et projet de test `mobile`

- **Statut** : review
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

- [x] `E2E-18-11` vert.
- [x] Vitest : `useMediaQuery` suit les changements de la requête.
- [x] Test serveur : `/manifest.webmanifest` servi en `application/manifest+json`.
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

- **Seuil** : `shared/ui/media.ts` (`narrowMaxWidth`, `narrowQuery`, `coarseQuery`, `useMediaQuery`) ; les CSS gardent `@media (width <= 720px)`.
- **Tactile** : sous `@media (pointer: coarse)`, `--size-control-sm|md|lg` valent `--size-touch` (44 px) et `--size-bar` 48 px : tous les contrôles bâtis sur les tokens grandissent d'un coup. Les cibles hors tokens sont traitées en M6-06 (E2E-18-10).
- **Écran** : `100dvh` (coque, accueil, connexion, dialogues), `viewport-fit=cover` et marges `env(safe-area-inset-*)` sur la coque.
- **Titres** : `overflow-wrap: break-word` au lieu de `anywhere` (qui laissait les actions comprimer le titre lettre par lettre).
- **Icônes** : `scripts/generate-icons.mjs` (Playwright) rend la feuille Lucide du logo en PNG pour le manifeste et le lanceur Android ; sorties versionnées. `theme-color` : une balise par schéma système, et `applyTheme` active celle du thème forcé (sans couleur dans le TS).
- **Pas de changement serveur** : le type MIME `.webmanifest` est connu d'ASP.NET Core, `default-src 'self'` couvre le manifeste et le desktop sert les fichiers statiques sans jeton.
- **Playwright** : projet `mobile` (Pixel 7), `@mobile` exclu des autres projets ; `--grep @pr` garde le filtre du projet (vérifié par `--list`).
- **Vérifications** : `HostingTests.Get_WebManifest_IsServedAsManifestJson`, Vitest `media.test.ts` et `theme.test.ts`, `playwright test --project mobile` (E2E-18-11), lint, typecheck, Prettier.
