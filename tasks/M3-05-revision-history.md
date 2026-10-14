# M3-05 — Historique des révisions

- **Statut** : todo
- **Jalon** : M3
- **Dépend de** : M1-05
- **Références** : F13, §A-7.4 (Historique), §A-8.1 (`revisions`)

## Objectif

Consulter, comparer et restaurer les versions d'une page sans jamais réécrire l'historique.

## Périmètre

- Instantané de révision complet : contenu, catégories, valeurs de propriétés (F13.R1) — vérifier que M1-05/M2-03 l'assurent.
- API : liste (auteur, date, résumé), lecture d'une révision, diff entre deux révisions (texte ligne/mot + propriétés), restauration = **nouvelle** révision « Restauration de la révision N » (F13.R3).
- Écran Historique : liste, sélection de deux révisions, vue de diff, bouton Restaurer.
- Algorithme de diff : bibliothèque éprouvée (côté C# ou TS, à justifier), réutilisable pour la vue de conflits de M4-05.

## Critères d'acceptation

- [ ] `E2E-13-01` à `E2E-13-03` verts.
- [ ] Propriété : restaurer la révision N donne un contenu identique à N.

## Points d'attention

- Politique de rétention des révisions non définie : signaler la croissance sur les gros projets et proposer une option (ex. compactage des révisions anciennes) **sans** l'implémenter.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
