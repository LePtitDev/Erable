# M2-08 — Suppression et corbeille

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-03, M1-10
- **Références** : F12, F04.R2, F06.R4, §A-7.4 (Corbeille)

## Objectif

Supprimer sans peur : avertissement sur l'impact, restauration fidèle, liens cassés affichés proprement.

## Périmètre

- Suppression logique (`deleted_at`) en cascade : sous-pages, contenu des dossiers, pièces jointes de la page (F12.R1).
- Confirmation listant les éléments qui référencent l'élément et le nombre de descendants (F12.R2).
- Suppression d'une catégorie : retirée des pages ; d'une propriété : valeurs retirées mais restaurables (F12.R4).
- Vue Corbeille : liste, restauration (emplacement d'origine ou racine avec message, même ID, F12.R3), vider la corbeille en tapant le nom du projet (F12.R5).
- Liens cassés : style + infobulle « Élément supprimé — Restaurer » (E2E-04-05) ; pièce jointe supprimée : emplacement dédié (E2E-06-06).
- Ramasse-miettes des blobs après vidage.

## Critères d'acceptation

- [ ] `E2E-12-01` à `E2E-12-06`, `E2E-04-05`, `E2E-06-06` verts.
- [ ] Propriété : supprimer puis restaurer laisse le projet identique (hors dates).

## Points d'attention

- Vidage automatique « après N jours » (§A-7.4) : non couvert par les scénarios ; proposer de le reporter.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
