# M2-06 — Propriétés sur la page (lecture et édition)

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-03, M1-12
- **Références** : F04.R4, F05.R6, F07.R3, F08.R3, Mockups « Page — lecture » et « Page — édition » (panneau latéral)

## Objectif

Afficher et saisir les valeurs de propriétés d'une page, avec héritage, conflits et validation en direct.

## Périmètre

- **Lecture** : panneau « Propriétés · N valeurs » (clé, type, valeur, « via <catégorie> »), badge « hors règles » sur la page, pastilles de catégories dans l'en-tête.
- **Édition** : onglets du panneau latéral (Propriétés, Catégories, Liens, Fichiers) ; éditeur par type (interrupteur, liste, champ numérique filtré, sélecteur de page filtré par `in_category`, date…) ; « Valeur héritée de la catégorie X » ; « Rétablir la valeur héritée » ; conflit à résoudre ; « Ajouter une propriété ».
- Ajout / retrait de catégories sur la page.
- Validation instantanée (évaluateur TS), compteur « N erreur(s) de validation » dans la barre d'état, enregistrement jamais bloqué.
- Les valeurs s'enregistrent avec le contenu (même révision).

## Critères d'acceptation

- [ ] `E2E-05-12`, `E2E-07-04`, `E2E-07-05`, `E2E-07-06`, `E2E-08-06` verts.
- [ ] Tests de composant de chaque éditeur de type.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
