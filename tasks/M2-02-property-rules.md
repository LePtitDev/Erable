# M2-02 — Définitions de propriétés et moteur de règles

- **Statut** : todo
- **Jalon** : M2
- **Dépend de** : M2-01
- **Références** : §A-5.4, F08.R1–R6, §T-4.1 (Règles), §T-5 (`rules.json`)

## Objectif

Des propriétés typées et des règles exprimées en données, évaluées à l'identique en C# (autorité) et en TypeScript (retour instantané).

## Périmètre

- `PropertyDefinition` : nom, clé unique modifiable, description, type, défaut, règles, options d'`enum`.
- Types : `bool`, `int`, `float`, `string`, `text`, `enum`, `date`, `ref`, `list<T>` ; valeurs typées.
- Règles par type (tableau §A-5.4) au format `{ "op": "...", "value": ... }` ; aucune pour `bool` (F08.R2).
- Évaluateur C# **et** TS ; messages via clés i18n + paramètres (« Doit être inférieur ou égal à 60 »).
- `spec/fixtures/rules.json` exhaustif (chaque opérateur × bornes × types).
- Conversion de type : fonction qui dit si une valeur existante est convertible (F08.R6).
- Règle `match` : regex avec délai d'exécution maximal (protection ReDoS) et même dialecte des deux côtés — **vérifier** les différences .NET / JavaScript et restreindre la syntaxe acceptée si nécessaire.

## Critères d'acceptation

- [ ] `rules.json` vert en xUnit et Vitest.
- [ ] Propriété : `between(a,b)` ⇔ `greater_or_equal(a) ∧ less_or_equal(b)` ; aller-retour JSON d'une valeur valide.
- [ ] Une règle `match` pathologique ne bloque pas le serveur.

## Points d'attention

- Les règles `in_category` (pour `ref`) dépendent des données : l'évaluateur reçoit un contexte (interface) plutôt que d'accéder au stockage.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
