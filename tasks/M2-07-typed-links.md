# M2-07 — Types de liens et liens

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-03
- **Références** : §A-5.5, F09, Mockup « Page — lecture » (panneau Liens)

## Objectif

Relier explicitement deux pages par une relation nommée, visible des deux côtés.

## Périmètre

- Migration : `link_types`, `links` ; `doctor` étendu ; `legacy/` mis à jour.
- Type de lien : nom, description, libellé inverse optionnel ; écran simple (vue sous la racine Liens).
- API : CRUD types et liens ; `backlinks` enrichi (liens entrants typés **séparés** des références implicites, E2E-09-03).
- Panneau « Liens » de la page : « + Lier » (type puis page), sortants, entrants avec libellé inverse, « N liens entrants », suppression.
- Suppression d'un type utilisé : confirmation avec le nombre de liens (F09.R3).
- Données « Chroniques d'Aube » : Allié de, Situé à, Mentor de.

## Critères d'acceptation

- [ ] `E2E-09-01` à `E2E-09-05` verts.
- [ ] Tests d'intégration des endpoints.

## Points d'attention

- Interdire le lien d'une page vers elle-même et les doublons exacts (source, type, cible) ? Non spécifié : proposer.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
