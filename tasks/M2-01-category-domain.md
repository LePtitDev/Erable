# M2-01 — Domaine : catégories, héritage, résolution des valeurs

- **Statut** : todo
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

- [ ] Propriétés CsCheck : aucun cycle accepté quelle que soit la suite de reparentages ; résolution indépendante de l'ordre de déclaration des catégories.
- [ ] Exemples du mockup : PNJ < Personnage < Entité ; Ilsa PNJ + Marchand ; conflit PNJ / Montagnard.
- [ ] Couverture ≥ 90 % sur ce module.

## Points d'attention

- Cas limite à trancher et documenter : deux catégories sans lien fournissant la **même** valeur → pas de conflit (à confirmer avec l'utilisateur).
- `list<T>` : une liste héritée et une liste locale se remplacent-elles ou se cumulent-elles ? Le document ne le dit pas : poser la question.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
