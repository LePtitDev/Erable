# M1-02 — Domaine : éléments et arbre

- **Statut** : todo
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

- [ ] Propriété CsCheck : après toute suite d'opérations valides, chaque élément a un seul parent, aucune boucle, aucun élément hors de sa racine.
- [ ] Tests par exemples pour chaque règle F02.R1–R3, R6.
- [ ] Builders fluides de test (`Project.Build()…`, §T-4.3) amorcés ici.

## Points d'attention

- Le libellé des racines dépend de la **langue du contenu** du projet (§A-11.1, point 3) : stocker une clé, pas un texte.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
