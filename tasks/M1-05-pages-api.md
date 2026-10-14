# M1-05 — API arbre, éléments, pages, références, révisions

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-03, M1-04, M0-04
- **Références** : §A-5.6, §A-7.4, §A-8.1, F02, F03.R3, F04.R1–R2, F13.R1

## Objectif

Exposer l'API nécessaire à un wiki mono-utilisateur : arbre, éléments, contenu de pages.

## Périmètre

- `GET /projects/{projectId}/tree` : arbre léger (id, kind, parent, nom, ordre, compteurs des racines).
- `elements/{id}` : GET, PATCH (renommer, déplacer, réordonner), en appliquant les règles de M1-02.
- `pages` : POST (création sous un parent), GET.
- `pages/{id}/content` : GET (markdown + révision), PUT avec `If-Match: <revision>` → `409` si la révision a changé (la fusion arrive en M4-05).
- À chaque enregistrement, en **une transaction** : contenu, `plain_text`, table `refs` (extraction M1-04), nouvelle révision (instantané compressé).
- `resolve?ids=…` : nom, type, chemin, état (existant / supprimé / inconnu) pour N ID.
- `elements/{id}/backlinks` : références entrantes (les liens typés viendront en M2-07).
- Projet courant en mode desktop : ouverture / création via `POST /projects/open`, `POST /projects` ; liste des récents dans le profil applicatif (F01.R2).

## Critères d'acceptation

- [x] Tests d'intégration par endpoint : nominal, 404, 409, 422 en `ProblemDetails`.
- [x] Renommer un élément ne modifie aucun contenu Markdown (base d'E2E-02-02).
- [x] Déplacement interdit → 422 avec code d'erreur métier stable.
- [x] Enregistrement d'une page < 100 ms sur une page de taille réaliste (mesure indicative).
- [x] Instantané OpenAPI et client TS régénérés.

## Points d'attention

- Codes d'erreur métier stables (`tree.move.crossRoot`…) pour que l'UI les traduise.
- Prévoir dès maintenant `updatedBy` (utilisateur local « moi » en desktop).

## Notes d'implémentation

- **Découpage** :
  - `Erable.Storage/ProjectService` : opérations applicatives testables sans HTTP. Chaque écriture est **une** transaction : arbre chargé dans la transaction (règles de Core), éléments, contenu, texte brut, index `refs` (analyse M1-04) et révision. Écritures sérialisées par `ProjectDatabase` ;
  - `Erable.Server/Projects/ProjectWorkspace` : projets ouverts, verrouillés jusqu'à l'arrêt de l'hôte. Rouvrir un projet déjà ouvert le renvoie ; deux dossiers au même `projectId` (copie) sont refusés (`project.duplicateId`) ;
  - `Erable.Server/Projects/UserProfile` : petits documents JSON du profil (écriture atomique ; un document illisible est ignoré). Desktop : dossier du profil applicatif ; serveur : `<data>/profile` (passera dans `server.db` avec les comptes en M4) ;
  - `Api/ProjectEndpoints` : endpoints minces, `Api/Contracts.cs` (contrat, source du client TS), `Api/Problems.cs`.
- **Endpoints** (`/api/v1`) : `GET/POST projects`, `POST projects/open`, `POST projects/inspect` (statut d'un dossier sans écriture), `GET projects/{id}`, `GET …/tree`, `GET|PATCH …/elements/{id}`, `GET …/elements/{id}/backlinks`, `GET …/resolve?ids=A,B` (500 ID au plus), `POST …/folders`, `POST …/pages`, `GET …/pages/{id}` (vue complète : élément + chemin, Markdown, sous-pages, pièces jointes, en un appel), `GET|PUT …/pages/{id}/content`, `GET|DELETE me/recent-projects[/{id}]`, `PUT me/recent-projects/{id}/last-element` (dernière page consultée, pour E2E-01-02).
- **Ajouts au périmètre**, nécessaires à l'arbre et à l'accueil : `POST folders` (E2E-02-01), `projects/inspect` (F01.R1 : proposer la création sans rien écrire), `last-element`.
- **Erreurs** : `ProblemDetails` avec extension `code` stable (`tree.move.crossRoot`, `page.revisionConflict`, `project.locked`…). 404 = introuvable ; 409 = verrou, existe déjà, révision périmée ; 422 = règle métier ; 428 = `If-Match` absent ; 400 = ID invalide (route, requête, ou ID en nombre JSON).
- **Concurrence** : `GET content` renvoie `ETag: "<révision>"` ; `PUT content` exige `If-Match` (`"n"`, `W/"n"` ou `n`). La révision de l'**élément** sert de jeton : renommer ou déplacer une page en cours d'édition donnera un 409 à l'enregistrement, ce qui est volontairement prudent avant la fusion de M4-05.
- **Révisions** : chaque écriture d'un élément (création, renommage, déplacement, contenu) incrémente sa révision et insère une ligne `revisions` avec un instantané JSON compressé en Brotli (`RevisionSnapshot`). Les frères renumérotés ne changent ni de révision ni d'horodatage.
- **Auteur** : `CurrentUser.Id()` renvoie `local` (`KnownUsers.Local`) ; l'interface affichera « moi ». Les comptes arrivent en M4-01.
- **Racines** : stockées par clé, libellées dans la **langue du contenu** dans l'arbre, les chemins et les détails (`Seiten`, `Páginas`…).
- **OpenAPI** : `ElementId` = `type: string`, `pattern`, `format: element-id` ; `ElementId?` → `string | null`. Le générateur TS donne `ElementId` ou `ElementId | null` (le `null` était perdu avant ce correctif). Les champs de requête facultatifs ont une valeur par défaut, donc ne sont pas `required`. Instantané `openapi.json` et `schema.ts` régénérés.
- Renommage : `Storage.PageContent` → `StoredPageContent` (homonyme du contrat d'API).
- **Mesure indicative** : enregistrement d'une page réaliste (40 sections, une référence par section) : médiane de 20 enregistrements sous la borne de test de 250 ms (budget 100 ms mesuré formellement en M3-06). Le chargement de l'arbre entier à chaque écriture sera à revoir s'il pèse sur le budget avec 10 000 éléments.
- **Tests** : 38 tests d'intégration (`ProjectsApiTests`, `TreeApiTests`, `PagesApiTests`) : nominal, 404, 409, 422, 428, 400, renommage sans modification du Markdown (base d'E2E-02-02), ID en nombre JSON rejeté (critère reporté de M1-01).

**Vérification** : `dotnet test --solution Erable.sln` (307 réussis) · `dotnet format --verify-no-changes` · `pnpm -C src/web api:check`, `typecheck`, `lint`, `format:check`.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
