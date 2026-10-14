# M1-07 — Projet « Chroniques d'Aube », builders, points d'appui `/_test`

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-05, M1-06
- **Références** : features §1.4, features §2, §T-4.3

## Objectif

Disposer du monde de test unique utilisé par les tests d'intégration, les e2e, les captures et la démo.

## Périmètre

- Générateur (code C# versionné) qui produit `spec/projects/chroniques/` à partir d'une description lisible, avec **les ID figés** de features §2.1. Le dossier produit est versionné ; le générateur sert à le reconstruire après une migration de format.
- Contenu M1 : arbre de §2.1 (pages, dossiers, sous-pages), textes des pages (repris du mockup « Page — lecture » pour Ilsa), références croisées, pièces jointes de §2.2 (petits fichiers de test réels : PNG, WEBP, MP4 court, ZIP, SVG). Catégories, propriétés et liens s'ajoutent en M2.
- Dates de modification cohérentes avec l'horloge de test (13 octobre 2026, 10:00, Europe/Paris) : « Modifiée il y a 2 h par Camille R. » pour Ilsa.
- Points d'appui (mode test uniquement) : `POST /_test/reset?fixture=chroniques` (< 100 ms), `POST /_test/clock`, `GET /_test/blobs`. `login-as` arrive en M4-01.
- Fixtures Playwright : chargement du projet, horloge.

## Critères d'acceptation

- [x] Un test e2e d'exemple réinitialise le projet et ouvre Ilsa Varenne par son ID.
- [x] `reset` est isolé par fichier de test (exécution parallèle possible).
- [ ] Le projet passe `erable doctor` (dès que M1-16 existe).

## Points d'attention

- Les binaires de test doivent rester petits (< 1 Mo au total).

## Notes d'implémentation

- **Description lisible** : `tests/Erable.Testing/Reference/Chroniques.cs` (arbre de features §2.1, textes, dates, auteurs, pièces jointes). Les ID de §2.1 sont figés ; les autres sont dérivés du nom (`SHA-256("chroniques/" + nom)`, bit de poids fort forcé, donc hors de la plage réservée) : une régénération après migration garde **tous** les ID. `projectId` figé `CA0B0E5EC0A5D1E1`.
- **Builders** : `ProjectBuilder` gagne `WithAttachment`, `Stamped` (dates et auteurs), une fabrique d'ID, et des repères `[[@Nom]]` / `![[@Nom|légende]]` remplacés par les ID à l'écriture (une page peut citer un élément déclaré après elle). `ProjectSeeder` écrit un vrai projet avec le code de Core (texte brut, index `refs`, détection MIME) : un projet semé est indiscernable d'un projet écrit par l'API. `Erable.Testing` référence désormais `Erable.Storage`.
- **Génération** : test explicite `ReferenceProjectTests.GenerateChroniques` (même mécanisme que `legacy/`, pas de nouveau projet dans la solution). Garde-fous : `Committed_MatchesTheDescription` compare un vidage logique du dossier versionné et d'une génération fraîche ; `Committed_IsHealthy` (intégrité, arbre, refs, blobs) ; `Assets_StaySmall` (< 1 Mo).
- **Fichiers de test** réels dans `spec/projects/assets/` (86 Ko) : PNG et WEBP dessinés avec PIL, MP4 de 8 s (192×108, une image clé par seconde pour l'avance rapide, `faststart`) avec ffmpeg, ZIP, SVG **malveillant** (`onload` + `<script>`, pour E2E-06-05).
- **Dates** (horloge de test 13/10/2026 10:00 Paris = 08:00 Z) : Ilsa modifiée à 06:00 Z par `camille` (« il y a 2 h par Camille R. »), créée le 12 mars 2026. Le Col gelé, Hautbois et Ilsa sont modifiés dans les dernières 24 h (E2E-10-07).
- **Auteurs** : `spec/projects/users.json` (id, nom affiché, rôle ; mots de passe en M4-01). `UserDirectory` côté serveur donne les noms : `ElementDetails` gagne `createdByName` / `updatedByName`, et `GET /api/v1/me` renvoie `{ id, name }` (l'interface affichera « moi » quand l'auteur est l'utilisateur courant). En local, le nom est celui du compte du système (réponse au point d'attention de M1-08, « Bonjour, Arthur »).
- **Points d'appui** (`ERABLE_TEST_MODE=1`, jamais en Release) :
  - `POST /_test/reset?slot=<id>&fixture=chroniques|none` : **écart avec features §1.4**, paramètre `slot` ajouté. Chaque slot reçoit une copie fraîche, avec un **nouveau `projectId`** (sinon deux copies ouvertes côte à côte sont refusées comme doublons) et un **profil propre**, choisi par le cookie `erable-test-slot`. La copie précédente du slot est fermée. ~quelques ms.
  - `POST /_test/clock` `{ now }` : `TestClock`, qui démarre à la date de référence et **continue d'avancer** (deux enregistrements successifs restent ordonnés) ; `now: null` restaure la date de référence.
  - `GET /_test/blobs?projectId=` ; `POST /_test/limits` `{ maxAttachmentSize }` (E2E-06-07).
- **Profils** : `UserProfiles` choisit le profil de la requête (un seul hors mode test) ; `UserProfile` est injecté par requête ; `ProjectWorkspace.Create/Open` reçoivent le profil.
- **Playwright** : fixture `reference` (copie fraîche **par test** et par navigateur, plus fin que « par fichier » puisque `fullyParallel` exécute en parallèle les tests d'un même fichier), `freshProfile`, horloge du navigateur installée à la date de référence ; IDs figés dans `e2e/fixtures/chroniques.ts`. `start-backend.mjs` passe `ERABLE_TEST_FIXTURES`. Le test d'exemple passe par l'API (l'ouverture dans l'interface arrive avec M1-11).
- Critère « passe `erable doctor` » : à cocher en M1-16.

**Vérification** : `dotnet test --solution Erable.sln` (378 réussis, 3 ignorés dont le générateur) · `dotnet format --verify-no-changes` · `pnpm typecheck`, `lint`, `format:check` · `playwright test e2e/specs/m1-reference.spec.ts e2e/specs/m0-skeleton.spec.ts` (6 réussis, Chromium + WebKit).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
