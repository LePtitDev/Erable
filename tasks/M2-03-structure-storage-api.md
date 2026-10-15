# M2-03 — Stockage et API : catégories, propriétés, valeurs

- **Statut** : review
- **Jalon** : M2
- **Dépend de** : M2-02, M1-05
- **Références** : §A-7.4, §A-8.1, F07, F08.R3–R6, §T-6, P-07

## Objectif

Persister la structure et exposer une API qui renvoie des valeurs déjà résolues et des violations déjà calculées.

## Périmètre

- Migration de schéma v2 : `categories`, `page_categories`, `property_defs`, `property_values` (colonnes typées), `validation_issues` ; nouveau projet figé `legacy/v2` ; `doctor` étendu.
- Trancher P-07 : table dérivée de valeurs résolues (recommandé) maintenue à l'écriture.
- Recalcul des violations et des valeurs résolues : à l'enregistrement d'une page, au changement d'une règle ou d'un défaut (toutes les pages concernées, F08.R4), en une transaction.
- Endpoints CRUD typés : `categories`, `properties`, valeurs d'une page (`pages/{id}/properties`), catégories d'une page.
- Opérations spéciales : renommer / supprimer une option d'`enum` avec remplacement (F08.R5) ; changement de type avec liste des pages bloquantes (F08.R6) ; renommer la clé (E2E-08-11).
- Réponse « page » enrichie : valeurs résolues avec origine (`via PNJ`), conflits, violations.
- Mettre à jour « Chroniques d'Aube » (M1-07) avec catégories, propriétés et valeurs de features §2.2.

## Critères d'acceptation

- [x] Tests d'intégration de chaque endpoint (nominal + erreurs).
- [x] Changer `≤ 60` en `≤ 80` sur Niveau met à jour les violations de toutes les pages dans la même transaction.
- [x] Migration `legacy/v1` → v2 testée.
- [x] Enregistrement d'une page toujours < 100 ms avec recalcul.

## Points d'attention

- Un changement de défaut sur une catégorie haute (Entité) peut toucher des milliers de pages : mesurer et, si besoin, proposer un recalcul en tâche de fond avec état « en cours ».

## Notes d'implémentation

- **Schéma v2** (`Migrations/0002_structure.sql`, `ProjectFormat.CurrentVersion = 2`) : `categories`, `page_categories`, `property_defs`, `property_values` (colonnes typées ; scalaire = `idx 0`, liste = une ligne par élément, liste vide = `idx -1` sans valeur), `resolved_values`, `validation_issues`, et **dès maintenant** `link_types` / `links` (M2-07) pour éviter une version de format de plus. Les éléments catégorie / propriété / type de lien d'un projet v1 reçoivent leur ligne (une propriété v1 devient `string`, clé `p_<id>`).
- **P-07 tranché** (recommandation retenue) : table dérivée `resolved_values` (valeur effective de chaque propriété de chaque page vivante, origine `own`/`inherited`/`default`/`conflict`, catégorie source) + `validation_issues`, maintenues à l'écriture et reconstruites par `doctor --fix`.
- **Un seul moteur** : `StructureState` (Storage) charge arbre, graphe, définitions, valeurs des catégories, liens page-catégorie et valeurs des pages de la portée ; il résout (Core), valide (contexte `in_category`/`kind`) et produit les lignes dérivées. Utilisé par la vue de page, l'écriture et `doctor` : ils ne peuvent pas diverger.
- **Recalcul en une transaction** : enregistrement d'une page → cette page ; catégorie (parent, description, défaut) → pages de la catégorie et de ses sous-catégories ; définition (règles, type, défaut) → toutes les pages, pour cette propriété (F08.R4). Mesure : enregistrement d'une page avec recalcul < 100 ms (test `SavePage_WithRecompute_StaysUnderTheBudget` ; à remesurer en M3-06 sur 10 000 pages). Pas de recalcul en tâche de fond pour l'instant.
- **Propriétés listées sur une page** : celles définies sur la page, celles fournies par la chaîne de ses catégories (conflits compris) et celles dont la définition a un défaut. « Renseignée sur N pages » = pages dont la valeur vient de la page ou d'une catégorie (conflits compris), hors défaut de la définition.
- **Validation** : sur la valeur **effective** (propre ou héritée) d'une page vivante ; les défauts des catégories ne sont pas validés en tant que tels.
- **API** (`StructureEndpoints`) : `GET/POST categories`, `GET/PUT categories/{id}`, `GET categories/{id}/pages?includeSubCategories`, `PUT/DELETE categories/{id}/values/{propertyId}`, `GET/POST properties`, `GET/PUT properties/{id}`, `GET pages/{id}/properties`. `PageView` gagne `categories` et `properties` (valeurs résolues, origine, sources « via », candidats d'un conflit, violations). `PUT pages/{id}/content` accepte `categories` et `properties` (remplacement complet) : même révision que le contenu (prépare M2-06). Les `PUT` sont des remplacements complets ; le nom passe toujours par `PATCH elements/{id}`.
- **Erreurs** : `DomainError.Extensions` → extensions du `ProblemDetails` : `loop` (cycle, E2E-07-03), `pages` (option utilisée sans remplacement F08.R5, valeurs non convertibles F08.R6), `propertyId` (valeur mal formée). `property.key.taken` → 409, le reste en 422.
- **Enum** : une option sans clé reçoit une clé dérivée de son libellé ; quitter le type `enum` est une conversion (par libellé), pas une suppression d'options.
- **Révisions** : l'instantané contient désormais les catégories et valeurs propres d'une page, la description / le parent / les défauts d'une catégorie, la définition d'une propriété (champs absents des instantanés v1).
- **OpenAPI** : `JsonObject` publié comme objet libre (`additionalProperties: {}`) ; valeurs et règles sont du JSON libre lu par `src/shared/properties`.
- **Données** : « Chroniques d'Aube » décrit catégories (héritage, descriptions, défauts PNJ `Hostile = Non`, `Niveau = 10` ; Créature `Niveau = 20`, `Hostile = Oui` ; Joueur `Niveau = 1`), propriétés (types, règles, options de Rôle) et valeurs de §2.2 ; projet régénéré. `spec/projects/legacy/v2` figé (une catégorie, une propriété avec règle, des valeurs) ; le test legacy vérifie aussi `doctor` (hors texte de recherche écrit à la main) et, à partir de v2, la résolution.
- **doctor** : `category.cycle`, `structure.rowMissing`, `value.invalid`, `value.unknownProperty` (info), `derived.outOfSync` (réparable).
- Vérification : `dotnet test --solution Erable.sln` (Core 588, Storage 90, Server 156 verts) ; `pnpm -C src/web typecheck` et `test` (544 verts) ; `pnpm -C src/web api:generate` exécuté.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
