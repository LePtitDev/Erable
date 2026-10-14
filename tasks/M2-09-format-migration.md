# M2-09 — Migration de format à l'ouverture

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-03, M1-08
- **Références** : F01.R3, E2E-01-05, §T-6 (Migrations)

## Objectif

Ouvrir un projet créé par une version antérieure sans jamais risquer de le perdre.

## Périmètre

- Détection d'un `formatVersion` plus ancien à l'ouverture → dialogue de confirmation expliquant la sauvegarde.
- Sauvegarde `erable.db.bak-<date>` (copie cohérente via l'API de backup SQLite), migration, `doctor`, ouverture.
- En cas d'échec : restauration de la sauvegarde et message clair.
- Test sur chaque projet `spec/projects/legacy/vN/`.

## Critères d'acceptation

- [ ] `E2E-01-05` vert.
- [ ] Échec simulé en milieu de migration → projet d'origine intact.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
