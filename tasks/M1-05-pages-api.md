# M1-05 — API arbre, éléments, pages, références, révisions

- **Statut** : todo
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

- [ ] Tests d'intégration par endpoint : nominal, 404, 409, 422 en `ProblemDetails`.
- [ ] Renommer un élément ne modifie aucun contenu Markdown (base d'E2E-02-02).
- [ ] Déplacement interdit → 422 avec code d'erreur métier stable.
- [ ] Enregistrement d'une page < 100 ms sur une page de taille réaliste (mesure indicative).
- [ ] Instantané OpenAPI et client TS régénérés.

## Points d'attention

- Codes d'erreur métier stables (`tree.move.crossRoot`…) pour que l'UI les traduise.
- Prévoir dès maintenant `updatedBy` (utilisateur local « moi » en desktop).

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
