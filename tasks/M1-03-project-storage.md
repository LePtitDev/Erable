# M1-03 — Format de projet, SQLite, migrations

- **Statut** : review
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

- [x] Créer → fermer → rouvrir un projet conserve tout ; `PRAGMA integrity_check` = ok.
- [x] Ouvrir un dossier sans manifeste n'écrit rien (F01.R1).
- [x] `formatVersion` trop récent : erreur typée, empreinte des fichiers inchangée (base d'E2E-01-04).
- [x] Une seconde ouverture du même projet est détectée (F16.R2).
- [x] Tests contre un vrai SQLite en dossier temporaire.

## Points d'attention

- Encodage des `ulong` en `INTEGER` signé (M1-01).
- Stocker les dates en UTC ISO 8601 ou en ticks : choisir une convention unique et la documenter.

## Notes d'implémentation

- **Dépendances** : `Microsoft.Data.Sqlite` 10.0.13 (MIT ; SQLite e_sqlite3 embarqué, FTS5 inclus) et `Dapper` 2.1.89 (Apache-2.0) ; tests : `Microsoft.Extensions.TimeProvider.Testing` 10.11.0 (`FakeTimeProvider`).
- **P-08 tranché** : `projectId` au format `ElementId` (16 hexa). Manifeste `ProjectManifest` (Core) : `formatVersion`, `projectId`, `name`, `contentLanguage`, `createdAt`. Il est écrit de façon atomique (fichier temporaire puis renommage) et **en dernier** à la création : une création interrompue laisse un dossier qui n'est pas un projet, jamais un projet à moitié valide.
- **Base** (`ProjectDatabase`) : WAL (persistant), `foreign_keys=ON`, `busy_timeout=5000`, `synchronous=FULL` (un enregistrement de wiki est rare et petit : on préfère garder le dernier commit en cas de coupure de courant). Écritures sérialisées dans le processus (sémaphore), transactions `IMMEDIATE`. À la fermeture : `wal_checkpoint(TRUNCATE)` puis vidage du pool ; il ne reste que `erable.db` (vérifié par un test).
- **Migrations** : `Migrations/NNNN_nom.sql` embarqués. Les versions doivent être contiguës (vérifié au chargement) ; chaque migration tourne dans sa propre transaction, qui fixe aussi `PRAGMA user_version`. Le nombre de migrations doit égaler `ProjectFormat.CurrentVersion`, sinon exception au démarrage. Projet plus ancien → sauvegarde via l'API de sauvegarde SQLite (cohérente même avec un WAL), migration, mise à jour du manifeste. La confirmation à l'utilisateur arrive en M2-09.
- **Schéma v1** : `elements`, `page_contents`, `revisions` (+ `blob_sha256`), `refs` (+ `position` ; pas de clé étrangère sur la cible, un lien cassé est légitime), `attachments` (sans `page_id` : le propriétaire est le parent de l'élément), `user_activity`, `search_fts` (FTS5, `remove_diacritics 2`). Tables `STRICT`.
- **Conventions** : dates en texte UTC ISO 8601 à la milliseconde (`2026-10-13T08:00:00.000Z`), ID en `INTEGER` signé, types d'élément en texte camelCase. Documenté dans architecture §7.1 et §7.4.
- **P-04 — écart avec la recommandation** : `erable.lock` est ouvert **sans partage** pendant toute la session (Windows : violation de partage ; Linux/macOS : `flock` exclusif, c'est ainsi que .NET implémente `FileShare.None`) et supprimé à la fermeture (`DeleteOnClose`). Le système le libère si le processus meurt : jamais de verrou périmé. Il ne contient que le PID, car un fichier verrouillé exclusivement n'est pas lisible par la seconde instance. Le port et le jeton IPC (M1-17) passeront donc par un autre canal (voir Propositions).
- **Trop récent** : refusé avant toute écriture (pas même le verrou), sur le manifeste **et** sur `user_version` (manifeste incohérent). Empreinte des fichiers inchangée (test).
- **Dépôt** : `ProjectRepository`, classe concrète sans interface (une seule implémentation prévue). SQL explicite, paramètres liés. Les lectures ouvrent leur propre connexion ; les écritures reçoivent la connexion et la transaction de `ProjectDatabase.WriteAsync`, pour qu'une opération métier soit une transaction. Les lignes Dapper sont en types primitifs, converties à la main (pas de gestionnaire de type global). `GetElements` passe les ID en tableau JSON (`json_each`) : une seule requête préparée, quel que soit leur nombre.
- **`ProjectStore.Inspect`** : statut d'un dossier sans rien écrire (absent, pas un projet, projet, trop récent, invalide), pour l'écran d'accueil.
- **Projet figé** `spec/projects/legacy/v1/` : un dossier, deux pages, des références, une pièce jointe et son blob. Il est généré par le test explicite `GenerateCurrentLegacyProject`, à lancer une seule fois par version de format. `LegacyProjectTests` copie chaque `legacy/vN`, l'ouvre et vérifie migration, `integrity_check`, `foreign_key_check`, contenus, références et arbre. `.gitattributes` : `spec/projects/**/blobs/**` en binaire (un blob adressé par son contenu ne doit jamais être normalisé).

**Vérification** : `dotnet test --solution Erable.sln` (135 réussis, 2 ignorés = tests explicites) · `dotnet format Erable.sln --verify-no-changes`.

## Propositions

- M1-17 : publier le port et le jeton IPC de l'instance qui détient un projet dans le profil applicatif (`instances/<projectId>.json`), puisque `erable.lock` n'est pas lisible tant qu'il est détenu.
