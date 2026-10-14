# M0-02 — SPA React/TS : outillage

- **Statut** : todo
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

- [ ] `dev`, `build`, `test`, `lint`, `typecheck` fonctionnent via `pnpm -C src/web …`.
- [ ] Un `any` explicite fait échouer le lint.
- [ ] Taille gzip du bundle affichée au build (référence pour le budget < 500 Ko, §T-11).
- [ ] `CLAUDE.md` §9 à jour.

## Points d'attention

- Le desktop chargera la SPA depuis le backend local : `base` d'assets relative ou configurable.
- Aucun accès direct à une API de l'OS (§A-1).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
