# M4-05 — Concurrence optimiste et fusion à trois voies

- **Statut** : todo
- **Jalon** : M4
- **Dépend de** : M4-03, M3-05
- **Références** : §A-8.3, F14.R5, §T-4.1 (Fusion), §T-15, P-05, décision Q3

## Objectif

Deux personnes éditent la même page : **aucun texte n'est jamais perdu**.

## Périmètre

- Trancher P-05 (fusion côté serveur dans Core recommandée).
- Fusion à trois voies (base / la mienne / la leur) au niveau des lignes, affinée au niveau des mots dans un bloc modifié des deux côtés ; propriétés fusionnées champ par champ.
- `PUT content` avec révision de base : fusion propre → enregistrée, réponse indique « fusionné avec les modifications de X » ; conflit → `409` avec les blocs en conflit.
- Vue de résolution : les deux versions de chaque bloc, choix par bloc ou édition manuelle, puis enregistrement.
- Session expirée pendant l'édition : reconnexion sans perdre les modifications (E2E-14-09).

## Critères d'acceptation

- [ ] Propriétés CsCheck : `merge(b,b,x)=x`, `merge(b,x,b)=x`, sections disjointes → pas de conflit et les deux présentes, **aucun caractère perdu sans conflit signalé**.
- [ ] Test d'intégration : N écritures parallèles sur des pages différentes → aucune perte.
- [ ] `E2E-14-07`, `E2E-14-08`, `E2E-14-09` verts.

## Points d'attention

- Tâche critique (§T-15) : privilégier un algorithme simple et prouvé (diff3) plutôt qu'une heuristique maison ; le dire si une bibliothèque existante convient.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
