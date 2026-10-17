# M5-02 — Accessibilité, captures visuelles, pseudo-locale

- **Statut** : in-progress
- **Jalon** : M5
- **Dépend de** : M3-03
- **Références** : §T-9.3, §T-8.2, F17 (E2E-17-04)

## Objectif

Garantir l'accessibilité et l'absence de régression visuelle sur tous les écrans du mockup.

## Périmètre

- `@axe-core/playwright` sur chaque écran, clair et sombre : zéro violation « serious » / « critical ».
- Captures `toHaveScreenshot` : écrans × clair/sombre × `fr`/`en`/`xx`, dans un conteneur Docker fixe (polices embarquées, animations coupées, horloge figée).
- Correction des débordements révélés par la pseudo-locale.
- Revue du parcours 100 % clavier.

## Critères d'acceptation

- [x] `E2E-17-04` vert.
- [ ] Références de captures versionnées (à générer) ; [x] procédure de mise à jour documentée.

## Notes d'implémentation

- **E2E-17-04** : `f17-visual.spec.ts` parcourt les 6 écrans du mockup (`e2e/support/screens.ts`) × clair/sombre avec axe-core (WCAG 2 A/AA, zéro « serious » / « critical »). Tags `@a11y @nightly`.
- **Captures** : projet Playwright `visual` (Chromium 1280×800, animations coupées, `reducedMotion`, polices embarquées attendues via `document.fonts.ready`, horloge navigateur et serveur figées par les fixtures), 6 écrans × clair/sombre × `fr`/`en`/`xx` = 36 références `e2e/screenshots/<écran>-<thème>-<langue>.png`. Conteneur fixe `e2e/visual/Dockerfile` (image Playwright 1.64 + SDK .NET) lancé par `pnpm e2e:visual:docker [-- --update]` ; procédure dans `e2e/screenshots/README.md` et `docs/testing.md` §9.3.
- **Pseudo-locale** : n'existait qu'en `DEV` ; elle est maintenant aussi dans le build de test (`VITE_PSEUDO_LOCALE=1`, posé par `start-backend.mjs`), choisie par les tests via un drapeau `sessionStorage` (le backend refuse `xx` comme préférence). Jamais dans un build de release.
- **Dette / non fait** : les références PNG ne sont pas encore générées (Docker non lancé ici, et les tests ne sont lancés qu'en fin de M5) ; la correction des débordements révélés par `xx` reste à faire après la première génération et la relecture des captures `*-xx.png`. Le conteneur n'a pas été exécuté : à valider une première fois. La revue du parcours 100 % clavier s'appuie sur E2E-17-01/02/03 existants ; le raccourci d'ouverture de palette et la configuration des raccourcis (M5-01) sont atteignables au clavier (boutons natifs, capture sur le bouton focalisé, Échap annule).
- Message de commit : `test(web): add axe-core matrix and visual regression suite with pseudo-locale (M5-02)`

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
