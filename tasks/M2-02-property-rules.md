# M2-02 — Définitions de propriétés et moteur de règles

- **Statut** : review
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

- [x] `rules.json` vert en xUnit et Vitest.
- [x] Propriété : `between(a,b)` ⇔ `greater_or_equal(a) ∧ less_or_equal(b)` ; aller-retour JSON d'une valeur valide.
- [x] Une règle `match` pathologique ne bloque pas le serveur.

## Points d'attention

- Les règles `in_category` (pour `ref`) dépendent des données : l'évaluateur reçoit un contexte (interface) plutôt que d'accéder au stockage.

## Notes d'implémentation

- **Core** (`Erable.Core.Properties`) : `PropertyType` (`int`, `list<string>`…), `PropertyValue` typé (égalité structurelle, listes comprises), `ValidationRule` (hiérarchie scellée, une classe par forme d'opérande), `RuleEvaluator` + `IRuleContext` (pour `kind` / `in_category`, sans accès au stockage), `ValueConversion` (F08.R6), `PropertyDefinition.Check`, `PropertyKey` (clé dérivée du nom : « Points de vie » → `points_de_vie`).
- **TS** (`src/web/src/shared/properties`) : `types.ts`, `rules.ts`, `safeRegex.ts` ; une règle TS est sa propre forme JSON canonique.
- **Cas partagés** : `spec/fixtures/rules/` (un dossier, comme `ids/` et `markdown/`) — `evaluation.json` (66 cas : chaque opérateur × bornes × types, listes, contexte), `definitions.json` (disponibilité par type et forme des opérandes), `values.json` (lecture des valeurs JSON), `regex.json` (sous-ensemble sûr + forme JS canonique attendue). Mêmes résultats en xUnit et Vitest. §T-5 mis à jour.
- **Valeurs** : JSON non étiqueté (le type vient de la définition). `int` limité à ±(2^53 − 1) (lisible exactement en JS) ; chaînes normalisées NFC ; `string` = une ligne de ≤ 1 000 caractères (tabulation admise), `text` ≤ 100 000 ; `date` = `yyyy-MM-dd` ; `ref` = ID canonique ; listes ≤ 1 000 éléments. Une valeur mal formée est **refusée** (422 en M2-03) ; seules les règles avertissent.
- **Enum** : options `{key, label}` ; la valeur stocke la clé, le libellé est renommable — F08.R5 (« renommer met à jour les pages ») est donc acquis sans réécriture des valeurs.
- **Violations** : `{rule, index?}` ; le message est construit par l'UI à partir de la règle (clé `validation.rule.<op>` + opérande), ce qui évite toute divergence de formatage entre C# et TS. Ordre : ordre des règles, puis ordre des éléments.
- **Longueurs** en points de code Unicode (un émoji compte pour 1). `not_empty` : au moins un caractère hors de l'ensemble d'espaces ECMAScript (liste explicite identique des deux côtés).
- **`match`** — vérifié : .NET et JS diffèrent sur `$` (.NET accepte un saut de ligne final), `.` (JS exclut aussi CR, U+2028/9), les classes `d w s` (Unicode en .NET), la soustraction de classes (.NET) et les classes vides. Solution : un sous-ensemble analysé par un parseur maison (C# et TS) qui émet un motif **canonique** (littéraux en échappement Unicode, classes ASCII explicites, groupes non capturants, `$` → fin réelle en .NET). Serveur : `RegexOptions.NonBacktracking` (temps linéaire) + délai de 200 ms. Navigateur (moteur à retour arrière, et motif partagé entre utilisateurs) : **un groupe répété ne peut contenir ni quantificateur ni alternative** (`(a+)+`, `(a|aa)*` refusés), ce qui supprime les explosions exponentielles.
- **Dette** : des motifs polynomiaux (`a*a*a*…`) restent possibles côté navigateur, bornés par la longueur maximale d'une chaîne (1 000) ; une évaluation dans un Web Worker avec délai serait la parade complète si besoin.
- **`in_category`** inclut les sous-catégories (le contexte s'en charge).
- Piège d'outillage rencontré : des échappements Unicode écrits dans les sources ont été décodés à l'écriture ; le code TS construit donc ces séquences (`u(code)`) au lieu de les écrire en dur.
- Vérification : `dotnet test --project tests/Erable.Core.Tests` (588 verts) ; `pnpm -C src/web exec vitest run src/shared/properties` (204 verts) ; lint et typecheck propres.

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
