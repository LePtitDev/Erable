# M2-08 — Suppression et corbeille

- **Statut** : review
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

- [x] `E2E-12-01` à `E2E-12-06`, `E2E-04-05`, `E2E-06-06` verts ; `E2E-09-05` (laissé par M2-07) vert aussi.
- [x] Propriété : supprimer puis restaurer laisse le projet identique (hors dates) : `DeleteThenRestore_LeavesTheProjectIdentical`, qui couvre aussi page, catégorie, type de lien, dossier et pièce jointe.

## Points d'attention

- Vidage automatique « après N jours » (§A-7.4) : non couvert par les scénarios ; proposer de le reporter.

## Notes d'implémentation

- **Format v3** (`Migrations/0003_trash.sql`, `ProjectFormat.CurrentVersion = 3`) : trois colonnes sur `elements` (`deleted_root`, `deleted_by`, `deleted_orphan`). **Écart avec le plan** (aucune migration prévue) : sans elles, on ne sait pas quels descendants reviennent avec un parent (un enfant supprimé plus tôt ne doit pas revenir), ni distinguer deux suppressions de même instant (l'horloge des tests est figée). `spec/projects/legacy/v3` figé ; v1 et v2 sont migrés par `LegacyProjectTests`. `docs/architecture.md` §7.4 mis à jour.
- **Entrée de corbeille** : une suppression = une entrée, restaurée ou vidée en bloc. Restaurer replace l'entrée sous son parent d'origine, sinon sous la racine de son type (la racine est retrouvée en remontant la chaîne des parents, supprimés compris) et l'API le dit (`relocated`). Une pièce jointe dont la page est dans la corbeille ne se restaure pas seule (`trash.parent.deleted`).
- **Suppression** (`ProjectService.DeleteElementAsync`) : une transaction ; les valeurs dérivées des pages supprimées, des pages qui perdent une catégorie et des valeurs d'une propriété supprimée sont recalculées. Catégories, valeurs d'une propriété et liens d'un type ne sont pas effacés mais masqués (le moteur ne lit que les éléments vivants) : ils reviennent avec l'élément (F12.R4).
- **Impact** (`GET elements/{id}/impact`) : pages qui mentionnent l'élément ou un de ses descendants (hors suppression ; pour une pièce jointe, hors sa propre page), sous-pages, éléments emportés, liens typés, pages qui perdent une catégorie, propriétaires de valeurs. La boîte de confirmation en tire ses phrases.
- **API** : `DELETE elements/{id}` (204), `GET trash`, `POST trash/{id}/restore`, `DELETE trash/{id}` (définitif), `DELETE trash` (vider). Le vidage supprime lignes et dépendances puis lance le ramasse-miettes des blobs ; les entrées qui pendaient sous une entrée effacée sont rattachées à la racine de leur type (`deleted_orphan`, E2E-12-05).
- **Interface** : « Supprimer » dans le menu contextuel de l'arbre et touche Suppr, bouton de suppression sur chaque pièce jointe ; lien « Corbeille » (avec compteur) sous l'arbre ; écran `/p/:projectId/trash` (restaurer, supprimer définitivement avec confirmation, vider en tapant le nom du projet). Un lien vers un élément supprimé a l'infobulle « Élément supprimé — Restaurer » et mène à la corbeille.
- **Données de référence** : la page Hautbois ne mentionne plus Capitaine Orsen, pour que « 1 page fait référence » à Orsen (Ilsa) soit vrai comme dans `docs/features.md` §2.2.
- **Dette** : pas de vidage automatique « après N jours » (proposition faite ci-dessous) ; la suppression ne crée pas de révision ; un lien d'une propriété `ref` vers une page supprimée reste en place (l'instance est signalée par les règles `kind`/`in_category`).
- Vérification : `pnpm -C src/web test` (620 verts), `lint`, `typecheck`, `format:check`, `api:check` ; `pnpm e2e:pr` (124 verts, Chromium + WebKit) ; `dotnet test --solution Erable.sln` (930 verts).
- Commit proposé : `feat(core,storage,server,web): add deletion, the trash and restoring (M2-08)`.

## Propositions

- Vidage automatique après N jours (§A-7.4) : à reporter après M4 (il faut un planificateur côté serveur et une politique par projet).
- Ajouter une révision « supprimé » / « restauré » à l'historique (M3-05) pour tracer qui a supprimé quoi, au-delà de `deleted_by`.
