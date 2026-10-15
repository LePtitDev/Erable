# M2-01 — Domaine : catégories, héritage, résolution des valeurs

- **Statut** : review
- **Jalon** : M2
- **Dépend de** : M1-02
- **Références** : §A-5.3, F07.R1–R4, §T-4.1 (Héritage), §T-4.2, décision Q4

## Objectif

Implémenter dans Core la logique la plus subtile du modèle : l'héritage des valeurs de propriétés et la détection des conflits.

## Périmètre

- `Category` : description, parent optionnel unique, valeurs par défaut.
- Reparentage refusé s'il crée un cycle, avec le chemin de la boucle dans l'erreur (« Personnage › PNJ › Marchand › Personnage », E2E-07-03).
- Une page porte 0..n catégories (ordonnées).
- **Résolution** d'une valeur pour une page : page > catégorie la plus spécifique > défaut de la définition. Résultat typé : `Value(source)`, `Inherited(source category)`, `Conflict(candidates)`, `Unset`.
- Définir précisément « la plus spécifique » quand une page a plusieurs catégories dans **la même** chaîne (ex. PNJ + Marchand) : la plus profonde gagne, sans conflit. Conflit seulement entre catégories **sans lien d'héritage** fournissant des valeurs différentes.
- Pages d'une catégorie avec ou sans sous-catégories (F07.R4).

## Critères d'acceptation

- [x] Propriétés CsCheck : aucun cycle accepté quelle que soit la suite de reparentages ; résolution indépendante de l'ordre de déclaration des catégories.
- [x] Exemples du mockup : PNJ < Personnage < Entité ; Ilsa PNJ + Marchand ; conflit PNJ / Montagnard.
- [x] Couverture ≥ 90 % sur ce module.

## Points d'attention

- Cas limite à trancher et documenter : deux catégories sans lien fournissant la **même** valeur → pas de conflit (à confirmer avec l'utilisateur).
- `list<T>` : une liste héritée et une liste locale se remplacent-elles ou se cumulent-elles ? Le document ne le dit pas : poser la question.

## Notes d'implémentation

- **Décisions de l'utilisateur** (15 oct. 2026) : deux catégories sans lien qui fournissent la même valeur → pas de conflit ; `list<T>` héritée et locale **se remplacent** ; P-14 : catégories rangées par dossiers dans l'arbre, héritage montré dans l'écran Catégorie. §A-5.3 complété en conséquence.
- `Erable.Core.Categories.CategoryGraph` : graphe d'héritage indépendant de l'arbre de l'explorateur. Un parent inconnu (catégorie supprimée) est conservé mais ignoré par les parcours : restaurer la catégorie rétablit la chaîne. `FindCycles` sert à `doctor` (données corrompues).
- Cycle refusé avec le code `category.parent.cycle` et la boucle en extension `loop` (ID hexa) : « Personnage › PNJ › Marchand › Personnage » ; le choix de soi-même comme parent donne `[X, X]`. `DomainError` gagne un champ optionnel `Extensions` (renvoyé dans le `ProblemDetails` à partir de M2-03).
- **Règle de résolution précisée** : pour chaque catégorie de la page, la plus proche de sa chaîne qui définit une valeur est candidate ; une candidate **ancêtre d'une autre candidate** est écartée. Ainsi Garde (5) l'emporte sur PNJ (10) hérité par Marchand pour une page Garde + Marchand : la valeur la plus spécifique gagne. Résultat typé `Resolution<T>` : `Own`, `Inherited(sources)`, `FromDefinition` (défaut de la définition ; `Default` est réservé par CA1716), `Conflict(candidates)`, `Unset`.
- `CategoryMembership.PagesOf` : « via » = catégorie la plus profonde de la page dans le sous-arbre (Ilsa PNJ + Marchand → « via Marchand », E2E-07-07).
- Valeurs opaques (`T : class`, égalité au choix) : les valeurs typées arrivent avec M2-02.
- Vérification : `dotnet test --project tests/Erable.Core.Tests` (309 tests verts) ; couverture de `Erable.Core.Categories` ≈ 99 % (`-- --coverage --coverage-output-format cobertura`).

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
