# M1-02 — Domaine : éléments et arbre

- **Statut** : review
- **Jalon** : M1
- **Dépend de** : M1-01
- **Références** : §A-5.1, F02.R1–R6, §T-4.1 (Arbre), P-02

## Objectif

Modéliser les éléments et les règles de l'explorateur dans Core, indépendamment du stockage.

## Périmètre

- `ElementKind` : `Root`, `Folder`, `Page`, `Category`, `PropertyDefinition`, `LinkType`, `Attachment` (à confirmer selon P-02).
- `Element` : id, kind, parentId, name, sortOrder, created/updated (at, by), revision, deletedAt.
- Quatre racines fixes (Pages, Catégories, Propriétés, Liens), non renommables, non supprimables, avec ID stables connus du format (F02.R1).
- Règles de déplacement : rester sous sa racine (F02.R2) ; une page peut aller sous une page mais jamais sous sa descendante (F02.R3) ; un dossier ne contient que des éléments du type de sa racine.
- Ordre des frères (`sortOrder` fractionnaire ou entier espacé : choisir et justifier).
- Doublons de noms entre frères autorisés, détectables (F02.R6).
- Résultats d'opération typés (succès / erreur métier explicite), pas d'exceptions pour les cas métier.
- Trancher P-02.

## Critères d'acceptation

- [x] Propriété CsCheck : après toute suite d'opérations valides, chaque élément a un seul parent, aucune boucle, aucun élément hors de sa racine.
- [x] Tests par exemples pour chaque règle F02.R1–R3, R6.
- [x] Builders fluides de test (`Project.Build()…`, §T-4.3) amorcés ici.

## Points d'attention

- Le libellé des racines dépend de la **langue du contenu** du projet (§A-11.1, point 3) : stocker une clé, pas un texte.

## Notes d'implémentation

- **P-02 tranché** (recommandation retenue) : quatre racines. `ElementKind.Attachment` existe : une pièce jointe est un élément du même espace d'ID dont le parent est sa page ; elle n'est jamais déplacée par l'arbre (`tree.move.notMovable`) et l'explorateur ne l'affiche pas. Architecture §5.1 mis à jour.
- **Racines** (`Core/Elements/Roots.cs`) : ID fixes `0000000000000001` (Pages) à `…04` (Liens), communs à tous les projets. `ElementId.NewRandom()` ne tire jamais une valeur `< 0x10000` (plage réservée aux éléments connus du format). Les racines stockent une **clé** (`pages`, `categories`…) ; `Roots.Label(root, contentLanguage)` donne le libellé dans la langue du **contenu** (fr/en/de/es, repli anglais).
- **Langues du contenu** : `ContentLanguages` = les quatre langues d'interface (F01.R4). Une autre langue serait possible plus tard, avec un repli anglais pour les libellés.
- **Ordre des frères** (`Tree/SortOrder.cs`) : entiers `long` espacés de 2^16 ; une insertion prend le milieu de l'intervalle, et quand il n'y a plus de place (≈ 16 insertions au même endroit), les frères sont renumérotés (l'opération renvoie la liste des nœuds modifiés). Écartés : les flottants (perte de précision silencieuse) et l'indexation fractionnaire par chaînes (moins lisible en SQL).
- **`ElementTree`** (`Tree/ElementTree.cs`) : arbre en mémoire des éléments vivants. `Create`, `Rename`, `Move` (déplacement et réordonnancement, position = index parmi les futurs frères), `Remove` (sous-arbre, pour M2-08), `CheckMove`/`CheckParent` (sans effet, pour le retour visuel du glisser-déposer), `HasDuplicateName`, `Ancestors`, `RootOf`, `Descendants`, et `FindAnomalies` (parent manquant, cycle, élément détaché, élément hors de sa racine) pour `erable doctor`. Une opération refusée ne modifie rien.
- **Règles** : un dossier ne contient que des dossiers et des éléments du type de sa racine ; seule une page peut aller sous une page (sous-page) ; rien d'autre ne va sous une catégorie, une propriété ou un type de lien en M1. La hiérarchie des catégories affichée dans l'arbre de features §2.1 (Entité › Personnage › PNJ) est celle de l'**héritage** : à trancher en M2-01 (afficher l'héritage, ou autoriser une catégorie sous une catégorie).
- **Résultats typés** : `Result<T>` / `DomainError(Code, Message)` (`Core/Result.cs`), codes stables dans `Core/ErrorCodes.cs` (`tree.move.crossRoot`, `tree.move.intoDescendant`, `tree.parent.invalid`, `tree.root.immutable`, `element.name.empty`…). La règle « croiser les racines » est vérifiée avant « descendante », pour un message plus clair.
- **Noms** : espaces de début et de fin retirés, non vides, 200 caractères au plus, sans caractère de contrôle. Doublon = même nom sans tenir compte de la casse (F02.R6).
- **Builders** : `Project.Build().WithFolder(…).WithPage(name, under:, id:, markdown:)…` dans `tests/Erable.Testing/ProjectBuilder.cs` (qui référence désormais Core) ; `BuildTree()` et `IdOf(name)`. Le stockage et le contenu s'y brancheront en M1-03.
- **Tests** : 38 tests (exemples F02.R1–R4, R6, ordre, anomalies) + 2 propriétés CsCheck (suites aléatoires de créations, déplacements et renommages : aucune anomalie, chaque nœud listé une seule fois sous son parent, ordres uniques ; un déplacement ne modifie aucun autre parent). Couverture de Core : 91 % des lignes.

**Vérification** : `dotnet test --project tests/Erable.Core.Tests` · `dotnet format Erable.sln --verify-no-changes`.

## Propositions

- M2-01 : décider si l'arbre des catégories montre l'héritage (features §2.1 le suggère) ou des dossiers uniquement.
