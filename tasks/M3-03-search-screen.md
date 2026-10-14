# M3-03 — Écran Recherche et filtres

- **Statut** : todo
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : F10, Mockup « Recherche, filtres et récents »

## Objectif

L'écran de recherche du mockup, avec un état entièrement porté par l'URL.

## Périmètre

- Panneau Filtres : Type (avec compteurs), Catégories (puces + « Autre »), Propriétés (conditions ajoutables, éditeur adapté au type), Modifié (24 h / 7 j / 30 j / Tout), « Hors règles », « Réinitialiser » (conserve le texte, E2E-10-10).
- Résultats : « N résultats pour « … » », tri, carte par résultat (type, titre, chemin, extrait surligné, catégories, méta).
- Colonne Consultées / Modifiées (alimentée par M3-04).
- État complet dans l'URL (texte, filtres, tri), partageable (F10.R4).
- « Aucun résultat » avec proposition de réinitialiser.
- Raccordement de « Voir dans la recherche » depuis la catégorie (E2E-07-07).

## Critères d'acceptation

- [ ] `E2E-10-02` à `E2E-10-11` et la partie recherche de `E2E-07-07` verts.
- [ ] Le surlignage n'utilise jamais `dangerouslySetInnerHTML`.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
