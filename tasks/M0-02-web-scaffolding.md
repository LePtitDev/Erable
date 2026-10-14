# M0-02 — SPA React/TS : outillage

- **Statut** : review
- **Jalon** : M0
- **Dépend de** : —
- **Références** : §A-3.1, §T-3, §T-8, P-01, P-11

## Objectif

Créer la SPA `src/web` avec un outillage strict, prête à recevoir les écrans.

## Périmètre

- pnpm (version fixée par `packageManager`, via Corepack — P-11), Vite, React, TypeScript.
- `tsconfig` : `strict`, `noUncheckedIndexedAccess`, `exactOptionalPropertyTypes`.
- ESLint (flat config) `typescript-eslint/strict-type-checked`, règles React Hooks, `no-explicit-any` en erreur hors code généré ; formateur (Prettier ou équivalent).
- Vitest + Testing Library + jsdom ; un test de composant exemple.
- CSS Modules ; code organisé par fonctionnalité (`src/features/tree`, `src/features/editor`…) plus `src/shared/`.
- Dépendances de base (versions vérifiées) : TanStack Router, TanStack Query, Zustand, Radix UI. Pas encore CodeMirror ni unified.
- Proxy Vite vers le backend en dev.
- Page « hello » minimale.
- Trancher P-01 (`src/web/`, e2e dans `src/web/e2e/`) puis, après accord, corriger `docs/features.md` §1.3.

## Hors périmètre

- Tokens et thème (M0-05), i18n (M0-06), client API (M0-04), Playwright (M0-09).

## Critères d'acceptation

- [x] `dev`, `build`, `test`, `lint`, `typecheck` fonctionnent via `pnpm -C src/web …`.
- [x] Un `any` explicite fait échouer le lint.
- [x] Taille gzip du bundle affichée au build (référence pour le budget < 500 Ko, §T-11).
- [x] `CLAUDE.md` §9 à jour.

## Points d'attention

- Le desktop chargera la SPA depuis le backend local : `base` d'assets relative ou configurable.
- Aucun accès direct à une API de l'OS (§A-1).

## Notes d'implémentation

- **Versions vérifiées sur npm (14/10/2026)** : React 19.3, Vite 8.3 (Rolldown), Vitest 5.0, ESLint 10.12, typescript-eslint 8.71, TanStack Router 1.170 / Query 5.104, Zustand 5.0, Radix UI 1.8 (paquet unifié `radix-ui`). Versions exactes épinglées.
- **Écarts de version (contraintes de compatibilité)** :
  - **TypeScript 6.0.3** et non 7.0 (portage natif) : typescript-eslint 8.71 exige `typescript <6.1`.
  - **jsdom 28.1** et non 30 : jsdom 30 exige Node ≥ 22.22, la machine de dev a Node 22.18. `engines.node` fixé à `>=22.12.0`.
  - **pnpm 10.34.6** (P-11) et non 12 : pnpm 12 est livré en binaire natif que le Corepack 0.33 de Node 22 ne sait pas lancer.
- P-01 tranché : SPA dans `src/web/`, e2e dans `src/web/e2e/` ; `docs/features.md` §1.3 corrigé, note retirée de `CLAUDE.md` §7.
- Routage TanStack Router **par code** (pas le plugin de génération de routes) : moins d'outillage tant qu'il n'y a qu'une route ; à reconsidérer en M1-09.
- `base` Vite = `/` (configurable par `ERABLE_BASE`) : une base relative `./` casserait les assets sur les routes profondes (`/p/:id/e/:id`), et le backend sert la SPA à la racine dans tous les modes.
- Proxy de dev : `/api`, `/_test`, `/hubs` (WebSocket) → `ERABLE_BACKEND_URL` (défaut `http://localhost:5080`, profil `launchSettings`).
- Taille du bundle : Vite affiche les tailles gzip ; `scripts/bundle-size.mjs` additionne en plus le JS **initial** (référencé par `index.html`) et échoue au-delà de `--max-kb` (`pnpm bundle-size`, pour la CI). Actuellement ≈ 98 Ko gzip.
- ESLint : `strictTypeChecked`, `no-explicit-any` en erreur (vérifié : un `any` fait échouer `lint`), `consistent-type-imports`, React Hooks, React Refresh. Prettier pour le formatage (`format:check`).
- `src/shared/` sera créé avec son premier contenu (M0-04/M0-05) : pas de dossier vide.

**Vérification** : `pnpm -C src/web typecheck`, `lint`, `test` (1/1), `build`, `format:check` ; `dev` démarré et sondé sur :5173.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
