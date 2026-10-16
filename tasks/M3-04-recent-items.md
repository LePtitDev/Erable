# M3-04 — Récemment consultées / modifiées

- **Statut** : review
- **Jalon** : M3
- **Dépend de** : M3-01
- **Références** : F11, §A-7.4 (`user_activity`, `revisions`), Mockups « Page — lecture » (sous l'arbre) et « Recherche »

## Objectif

Retrouver ce sur quoi on travaillait.

## Périmètre

- Consultation comptée après 2 s d'affichage (F11.R1), enregistrée dans `user_activity` par utilisateur.
- `me/recent` : consultées (par utilisateur) et modifiées (communes, avec auteur et date, issues des révisions).
- Affichage : sous l'explorateur (type, nom, temps relatif) et dans l'écran Recherche (onglets Consultées / Modifiées).
- Accueil : « dernière page consultée » du projet (E2E-01-02) si ce n'est pas déjà fait.

## Critères d'acceptation

- [x] `E2E-11-01` vert (`f11-recent.spec.ts`, Chromium) ; `E2E-11-02` s'appuie sur l'onglet « Modifiées » de l'écran Recherche : il est écrit avec M3-03 (`E2E-11-03` en M4).
- [x] Le minuteur de 2 s est testé avec une horloge simulée, sans attente réelle.

## Notes d'implémentation

- **Pas de migration** : `user_activity` existe depuis le format v1. Le backend est `ProjectService.RecordViewAsync` / `GetRecent` (`ProjectService.Recent.cs`) ; les racines, les pièces jointes et les éléments dans la corbeille ne sont pas enregistrés (`element.notFound`), ceux de la corbeille sortent de la liste et reviennent à la restauration, la purge efface la ligne (déjà fait par `TrashRepository.Purge`).
- **Modifiées** : lues sur `elements.updated_at/updated_by`, que chaque révision met à jour dans la même transaction, plutôt que sur la table `revisions` (une ligne par élément, déjà indexée, sans `GROUP BY`). Pièces jointes et racines exclues : ajouter un fichier modifie la page.
- **API** : `GET /me/recent?limit=` (`viewed` propre à l'utilisateur, `edited` commun avec auteur résolu par `UserDirectory`) et `POST /me/views` ; sous le projet, comme le reste de `me/*` propre à un projet (`docs/architecture.md` §8.1 listait `me/recent` sans préciser la portée). `limit` est borné à 50.
- **Minuteur de 2 s** : `useCountView` (dans `ElementRoute`) arme un `setTimeout(2000)` quand un élément s'affiche et l'annule s'il change avant ; testé avec `vi.useFakeTimers` (aucune attente réelle), côté e2e avec `page.clock.runFor`. La clé de requête `recent` est sous `projectContent` : toute modification de contenu rafraîchit aussi les listes.
- **Interface** : `features/recent` (`RecentList` : type, nom, temps relatif, auteur optionnel ; `RecentViewedPanel` sous l'arbre à la place du texte provisoire). Les onglets Consultées/Modifiées de l'écran Recherche et la liste de Ctrl+K réutilisent `RecentList` (M3-03, M3-02).
- **Accueil** : « dernière page consultée » (E2E-01-02) était déjà fait par M1 (`recordLastElement` / `lastElementId`).
- Vérification : `dotnet test --solution Erable.sln`, `pnpm test` (630), `lint`, `typecheck`, `format:check`, `pnpm exec playwright test e2e/specs/f11-recent.spec.ts --project chromium`.
- Commit proposé : `feat(storage,server,web): list recently viewed and edited elements (M3-04)`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
