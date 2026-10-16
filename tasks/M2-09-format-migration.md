# M2-09 — Migration de format à l'ouverture

- **Statut** : review
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

- [x] `E2E-01-05` vert (Chromium et WebKit).
- [x] Échec simulé en milieu de migration → projet d'origine intact (`Open_MigrationFailsMidWay_RestoresTheOriginalProject`, échec à l'étape 2 et à l'étape 3 ; `Open_MigrationThatLeavesAnInvalidProject_IsRolledBack` pour un projet que le doctor refuse).

## Notes d'implémentation

- **État de départ** : l'ouverture d'un projet ancien faisait déjà sauvegarde (API de backup SQLite) puis migration, mais **sans confirmation** et sans reprise en cas d'échec. Cette tâche ajoute la confirmation, la vérification par le doctor et la restauration.
- **Confirmation** : `ProjectStore.Open(folder, clock, migrate)`. Sans `migrate`, un projet plus ancien répond `project.migration.required` (409, `fromVersion` / `toVersion`) et rien n'est écrit (empreinte du dossier identique, vérifié sur v1 et v2). `POST projects/open` accepte `confirmMigration` (faux par défaut). `FolderInspection` ne change pas : l'écran d'accueil ouvre normalement, reçoit la demande de confirmation et affiche `MigrationDialog` (« Sauvegarder et migrer » / Annuler) depuis « Ouvrir un dossier » comme depuis les projets récents (`useMigratingOpen`).
- **Migration** (`ProjectStore.Migrate`) : sauvegarde cohérente `erable.db.bak-<date>` ; migrations (chacune dans sa transaction) ; manifeste ; `ProjectDoctor.Diagnose` (une erreur = échec). Au moindre échec : la base est fermée puis **remplacée par la sauvegarde** (fichiers `-wal` / `-shm` retirés), le manifeste d'origine est remis, la sauvegarde devenue inutile est supprimée (le dossier retrouve ses fichiers d'avant) et l'erreur `project.migration.failed` (500) le dit. Si la restauration elle-même échoue, la sauvegarde est conservée et son chemin figure dans l'erreur (`backup`).
- **Point d'injection de test** : `Migrations.Apply(…, onApplied)` et `ProjectStore.Open(…, onMigrationStep)` appellent un délégué après chaque version appliquée ; aucun effet en production.
- **Format v3 déjà publié** : `legacy/v1` et `legacy/v2` sont migrés vers v3 par `ProjectMigrationTests` (théorie sur toutes les versions plus anciennes que la courante) et `LegacyProjectTests`.
- **Dette** : `Erable.Desktop <dossier>` sur un projet ancien retombe sur l'écran d'accueil (la demande de confirmation n'est pas rejouée au lancement) ; une sauvegarde par migration s'accumule, sans purge automatique.
- Vérification : `pnpm -C src/web test` (624 verts), `lint`, `typecheck`, `format:check`, `api:check`, `build` (422 kB gzip) ; `pnpm e2e` (180 verts, dont les `@nightly`) ; `pnpm e2e:trace` (M2 : 34/34) ; `dotnet test --solution Erable.sln` (943 verts).
- Commit proposé : `feat(storage,server,web): ask before migrating an older project and restore it on failure (M2-09)`.

## Propositions

- Rejouer la confirmation de migration quand `Erable.Desktop <dossier>` pointe sur un projet ancien (l'accueil pourrait ouvrir directement le dialogue sur ce dossier).
- Limiter le nombre de sauvegardes `erable.db.bak-*` conservées (ou proposer de les supprimer après quelques jours d'usage).
