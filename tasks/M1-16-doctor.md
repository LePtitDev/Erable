# M1-16 — Commande `erable doctor`

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-06
- **Références** : §T-6, §T-15

## Objectif

Un diagnostic d'intégrité qu'on lance sur n'importe quel projet, et qui sert de garde-fou à la fin de chaque test e2e.

## Périmètre

- `erable doctor <dossier> [--json] [--fix]` dans l'exécutable serveur (et exposé en interne pour les tests).
- Vérifications : `PRAGMA integrity_check`, `foreign_key_check`, blobs manquants / orphelins / empreinte incorrecte, références cassées, cycles dans l'arbre, éléments hors de leur racine, index `refs` désynchronisé du Markdown, `formatVersion`. FTS et propriétés résolues ajoutés en M2/M3.
- `--fix` limité aux réparations sûres (reconstruire un index dérivé, supprimer un blob orphelin) ; jamais de suppression de contenu utilisateur.
- Projets volontairement corrompus dans `spec/projects/corrupted/` pour les tests.
- Hook Playwright : `doctor` exécuté après chaque fichier e2e.

## Critères d'acceptation

- [x] Chaque type d'anomalie a un projet corrompu et un test.
- [x] Code de sortie non nul en cas d'anomalie ; sortie JSON stable.
- [x] « Chroniques d'Aube » passe sans anomalie.

## Points d'attention

- Les références cassées sont un état **légitime** (élément supprimé) : les signaler en information, pas en erreur.

## Notes d'implémentation

- **Core du diagnostic** : `Erable.Storage/ProjectDoctor` (testable sans CLI). Commande : `erable doctor <dossier> [--json] [--fix]` (`Cli/DoctorCommand`). Codes de sortie : `0` sain (les infos ne comptent pas), `1` anomalies restantes, `2` projet illisible (manifeste absent ou invalide, verrou pris par une instance ouverte).
- **Vérifications** (codes stables, `DoctorCodes`) : `manifest.invalid`, `project.locked`, `format.mismatch` (manifeste ≠ `user_version`), `format.tooNew`, `format.outdated` (ne migre **jamais** : le doctor ne modifie rien sans `--fix`, et arrête les vérifications dépendantes du schéma), `sqlite.integrity` (arrête la suite : données non fiables), `sqlite.foreignKey`, `tree.missingParent|cycle|detached|outsideOfRoot` (via `ElementTree.FindAnomalies` de Core), `page.contentMissing`, `index.outOfSync` (texte brut ou index `refs` recalculés depuis le Markdown par le même code que l'enregistrement), `ref.deletedTarget` / `ref.unknownTarget` (**info**, état légitime), `blob.missing`, `blob.corrupted` (SHA-256 ou taille), `blob.missingForRevision` (avertissement : historique non restaurable), `blob.orphan`, `blob.temporary`.
- **`--fix`** : reconstruit `refs` et `plain_text`, supprime les blobs non référencés et les restes de `blobs/tmp`. Rien d'autre ; aucune suppression de contenu. Un fichier temporaire n'est signalé que sur un projet fermé (dans un backend en marche, ce peut être un téléversement en cours).
- **JSON** : `{ folder, formatVersion, healthy, findings: [{ code, severity, message, elementId?, blob?, fixable, fixed }] }`, constatations triées (code, élément, blob) : sortie stable pour les scripts.
- **Projets corrompus** : **écart** avec le périmètre (« dans `spec/projects/corrupted/` ») : chaque corruption est une **recette** (SQL brut sans clés étrangères, ou fichier modifié) appliquée à un petit projet semé, dans `ProjectDoctorTests`. Même couverture (un test par type d'anomalie), sans versionner une douzaine de bases binaires qu'il faudrait régénérer à chaque migration. `tree.detached` et `format.outdated` n'ont pas de test : le premier est inatteignable depuis le stockage (`CHECK` sur `parent_id`), le second attend un format 2.
- **Hook e2e** : `GET /_test/doctor?projectId=` exécute le même diagnostic dans le backend (le projet y est ouvert et verrouillé) ; la fixture Playwright `reference` échoue le test si le projet laissé n'est pas sain.
- « Chroniques d'Aube » passe sans anomalie (`Diagnose_ReferenceProject_IsHealthy`, `Run_ReferenceProject_ExitsZero`) : critère de M1-07 coché aussi.
- FTS et propriétés résolues : M2/M3.

**Vérification** : `dotnet test --solution Erable.sln` (404 réussis, 3 ignorés) · `dotnet format --verify-no-changes` · `pnpm lint` · `playwright test e2e/specs/m1-reference.spec.ts` (2 réussis avec le hook).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
