# M3-03 — Écran Recherche et filtres

- **Statut** : review
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

- [x] `E2E-10-02` à `E2E-10-11`, la partie recherche de `E2E-07-07` et `E2E-11-02` écrits (`f10-search.spec.ts`, `f07-categories.spec.ts`), verts en Chromium et WebKit.
- [x] Le surlignage n'utilise jamais `dangerouslySetInnerHTML`.

## Notes d'implémentation

- **Route** `/p/:projectId/search` ; tout l'état est dans l'URL (`q`, `kind`, `cat`, `sub`, `where`, `mod`, `oor`, `sort`), lu et réécrit sous forme courte par `validateSearch` : un lien contenant un paramètre inconnu ouvre une recherche valide. Les listes sont sérialisées en JSON par le routeur (lisible, partageable). La logique (lecture, écriture, requête d'API, bascule des types) est pure : `features/search/searchState.ts`, testée y compris par propriété (aller-retour URL).
- **Types** : sans filtre, toutes les cases sont cochées (comme le mockup) ; décocher un type garde les autres ; tout recocher ou tout décocher revient à « aucun filtre ». Les dossiers ont leur ligne (« Dossiers »), absente du mockup.
- **Catégories** : six puces (les choisies d'abord, puis par nom) et « + Autre » pour le reste. **Propriétés** : une condition ajoutée a déjà une valeur adaptée au type (Non, 0, première option), donc filtre aussitôt ; l'éditeur de valeur est celui de la page (`ValueEditor`), sauf le booléen (liste Oui/Non, comme le mockup). Les opérateurs proposés sont ceux que le backend accepte.
- **Surlignage** : `Highlighted` rend les segments `{text, hit}` du backend en `<mark>` ; aucun `dangerouslySetInnerHTML`, un test vérifie qu'un `<img onerror>` dans une page reste du texte.
- **Texte** : la saisie est écrite dans l'URL après 250 ms de pause ou sur Entrée. Résultats : compteur annoncé (`role=status`), tri, « Afficher plus » (curseur), carte avec type, titre, chemin, extrait, catégories, auteur/date (ou type et taille pour une pièce jointe), règle enfreinte pour « Hors règles ». Colonne Consultées/Modifiées via `RecentList` (M3-04).
- **Voir dans la recherche** (catégorie) devient un vrai lien (`cat` + `sub`) ; sans texte, l'ordre est « dernière modification ».
- **Écart** : les paramètres de `GET search` portent maintenant des noms explicites en camelCase dans l'OpenAPI (`nameOnly`, `subCategories`…).
- Vérification faite pendant la tâche : Vitest de `features/search` (29), Playwright Chromium + WebKit des specs F07, F10, F11 (34), lint, typecheck, format, `api:check`. La suite complète a été lancée à la fin de M3 : sous la charge d'un passage complet, des tests Vitest d'écrans entiers dépassaient les délais par défaut (5 s par test, 1 s par `findBy`) ; `testTimeout` est passé à 20 s et `asyncUtilTimeout` à 5 s (`vitest.config.ts`, `src/test/setup.ts`). Deux passages complets consécutifs : 671 tests verts.
- Commit proposé : `feat(web,server): add the search screen with filters in the URL (M3-03)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
