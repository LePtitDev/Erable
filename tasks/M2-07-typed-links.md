# M2-07 — Types de liens et liens

- **Statut** : review
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

- [x] `E2E-09-01` à `E2E-09-04` verts. `E2E-09-05` (suppression d'un type utilisé) est livré avec la suppression elle-même, en M2-08 : le compteur « N liens utilisent ce type » est déjà exposé (`linkCount`).
- [x] Tests d'intégration des endpoints (`LinksApiTests`), de stockage (`LinkStorageTests`), du domaine (`LinkTypeRulesTests`).

## Points d'attention

- Interdire le lien d'une page vers elle-même et les doublons exacts (source, type, cible) ? Non spécifié : proposer.

## Notes d'implémentation

- **Pas de nouvelle migration** : les tables `link_types` et `links` (clé unique `source, type, cible`) existent depuis le schéma v2 de M2-03. `spec/projects/legacy/` est donc inchangé ; le projet de référence est régénéré avec les liens et les libellés inverses de §2.2 (Allié de ↔ Allié de, Situé à ↔ Lieu de, Mentor de ↔ Élève de ; Ilsa → Orsen « Allié de », Ilsa → Hautbois « Situé à »).
- **Décision (« Points d'attention »)** : un lien d'une page vers elle-même est refusé (`link.self`, 422) et un doublon exact (même source, type et cible) aussi (`link.duplicate`, 409). Le lien inverse (Orsen → Ilsa) reste un autre lien. À confirmer ou à assouplir.
- **Domaine** : `Erable.Core/Links/LinkTypeRules` (description ≤ 2 000 caractères, libellé inverse ≤ 200 sans caractère de contrôle, libellé vide = aucun). Le nom d'un type est le nom de l'élément (renommage par l'explorateur).
- **Stockage** : `LinkRepository` + `ProjectService.Links` (types, liens). Un lien dont le type ou une des pages est supprimé est masqué et revient avec l'élément (restauration en M2-08). Les révisions d'un type de lien portent description et libellé inverse (`LinkTypeSnapshot`). `erable doctor` signale `link.dangling` (extrémité qui n'est pas une page, type absent).
- **API** : `GET/POST link-types`, `GET/PUT link-types/{id}` (avec `linkCount`), `GET pages/{id}/links` (sortants : libellé du type ; entrants : libellé inverse, sinon celui du type), `POST links`, `DELETE links/{id}`. **Écart avec la tâche** : `backlinks` n'est pas modifié ; les liens typés entrants viennent de `pages/{id}/links` et les références implicites de `backlinks`, que le panneau affiche dans deux listes distinctes (E2E-09-03).
- **Interface** : écran du type de lien (`features/links/LinkTypeView`, édition de la description et du libellé inverse), création depuis la racine Liens (« Nouveau type de lien »), panneau « Liens » (`PageLinksPanel`) dans le panneau latéral en lecture et dans l'onglet « Liens » de l'éditeur : « Lier » (type puis page), sortants, « N liens entrants », références implicites, suppression. Les liens sont enregistrés tout de suite, ils ne font pas partie du brouillon de la page.
- **Dette** : le sélecteur de page de « Lier » est une liste simple triée par nom (deux pages homonymes ne se distinguent pas) ; un sélecteur avec recherche arrivera avec la palette de M3.
- Vérification : `pnpm -C src/web test` (609 verts), `lint`, `typecheck`, `format:check`, `api:check` ; `pnpm e2e:pr` (114 verts, Chromium + WebKit) ; `dotnet test --solution Erable.sln` (883 verts).
- Commit proposé : `feat(core,storage,server,web): add link types and typed links between pages (M2-07)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
