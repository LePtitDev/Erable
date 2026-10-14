# M5-02 — Accessibilité, captures visuelles, pseudo-locale

- **Statut** : todo
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

- [ ] `E2E-17-04` vert.
- [ ] Références de captures versionnées et procédure de mise à jour documentée.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
