# M1-03 — Format de projet, SQLite, migrations

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-02
- **Références** : §A-7.1, §A-7.4, F01.R1, F01.R3–R4, F16.R2, §T-6, P-04, P-08

## Objectif

Créer, ouvrir et migrer un projet Erable de façon sûre.

## Périmètre

- Manifeste `erable.json` : `formatVersion`, `projectId` (format à trancher, P-08), `name`, `contentLanguage`, date de création.
- `erable.db` : SQLite en WAL, `foreign_keys=ON`, `busy_timeout` ; Microsoft.Data.Sqlite + Dapper.
- Migrations SQL versionnées et embarquées, appliquées dans une transaction, version dans `PRAGMA user_version`. Schéma v1 : `elements`, `page_contents`, `revisions`, `refs`, `attachments`, `user_activity`, `search_fts` (table créée, alimentée en M3).
- Projet plus récent que l'application → refus sans écriture ; plus ancien → migration après sauvegarde `erable.db.bak-<date>` (la confirmation UI arrive en M2-09).
- Verrou mono-instance (P-04).
- Dépôt (`IProjectRepository` ou équivalent) pour les éléments et contenus, transactions explicites.
- Premier projet figé `spec/projects/legacy/v1/` et son test de migration (no-op pour v1).

## Critères d'acceptation

- [ ] Créer → fermer → rouvrir un projet conserve tout ; `PRAGMA integrity_check` = ok.
- [ ] Ouvrir un dossier sans manifeste n'écrit rien (F01.R1).
- [ ] `formatVersion` trop récent : erreur typée, empreinte des fichiers inchangée (base d'E2E-01-04).
- [ ] Une seconde ouverture du même projet est détectée (F16.R2).
- [ ] Tests contre un vrai SQLite en dossier temporaire.

## Points d'attention

- Encodage des `ulong` en `INTEGER` signé (M1-01).
- Stocker les dates en UTC ISO 8601 ou en ticks : choisir une convention unique et la documenter.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
