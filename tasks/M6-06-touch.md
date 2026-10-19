# M6-06 — Tactile : menus sans clic droit, déplacement sans glisser, cibles de 44 px

- **Statut** : todo
- **Jalon** : M6
- **Dépend de** : M6-02
- **Références** : F18.R4, F18.R5, F02

## Objectif

Tout faire au doigt : ce qui passe aujourd'hui par le survol, le clic droit ou le glisser-déposer a un équivalent tactile.

## Périmètre

- Arbre : bouton « … » par nœud (toujours visible avec un pointeur grossier, au survol et au focus sinon) qui ouvre le menu contextuel ; l'appui long l'ouvre aussi.
- Action « Déplacer vers… » (dialogue avec les destinations valides, filtrable), aussi sur le bureau ; même mutation que le glisser-déposer.
- Revue des actions visibles seulement au survol (lignes de l'accueil, listes) ; zones de toucher de 44 px.

## Critères d'acceptation

- [ ] `F18.R4`, `F18.R5`.
- [ ] `E2E-18-07`, `E2E-18-08`, `E2E-18-10` verts.
- [ ] Vitest : destinations valides de « Déplacer vers… » (`treeModel`).
- [ ] Définition de « terminé » (`CLAUDE.md` §10).

## Notes d'implémentation

_À remplir par l'agent._
