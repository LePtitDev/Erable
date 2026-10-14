# M1-04 — Grammaire `[[ID]]` : parseurs C# et TS

- **Statut** : todo
- **Jalon** : M1
- **Dépend de** : M1-01
- **Références** : §A-6, §T-4.1 (Parseur), §T-5, F04.R1, F04.R5

## Objectif

Une grammaire minimale des références, implémentée deux fois (extraction côté serveur, rendu côté client) et garantie identique par des fixtures communes.

## Périmètre

- Syntaxe (§A-6.1) : `[[ID]]`, `[[ID|texte]]`, `[[ID#ancre]]`, `![[ID]]`, `![[ID|légende]]`, `![[ID]]{width=… align=…}` ; ancres explicites `## Titre {#ancre}`.
- **Écrire la grammaire formelle** dans `docs/markdown-syntax.md` avant d'implémenter : échappements, `|` et `#` dans le libellé, références dans du code (inline et bloc → ignorées), attributs autorisés, comportement des liens imbriqués.
- **C#** : extension Markdig qui extrait les références (id, ancre, libellé, embed, position) et le texte brut pour FTS.
- **TS** : plugin remark (micromark) qui produit des nœuds `erableRef` / `erableEmbed` pour le rendu.
- Slugs d'ancres : même algorithme des deux côtés (accents retirés, ex. `#les-annees-de-cartographe`).
- Fixtures `spec/fixtures/markdown/refs.json` et `edge-cases.json`.

## Critères d'acceptation

- [ ] Les fixtures passent en xUnit **et** Vitest.
- [ ] Propriétés : toute référence générée insérée dans du Markdown arbitraire est retrouvée à la bonne position ; aucune exception sur entrée aléatoire (C# et TS).
- [ ] HTML brut non interprété par le pipeline TS (base d'E2E-04-07).
- [ ] Temps linéaire en la taille de l'entrée (test sur 1 Mo).

## Points d'attention

- Vérifier que Markdig et remark-gfm s'accordent sur le texte brut extrait (tableaux, listes de tâches) ; signaler tout écart.

## Notes d'implémentation

_À remplir par l'agent : décisions, écarts, dettes, commandes de vérification, message de commit proposé._

## Propositions

_Idées hors périmètre, à valider par l'utilisateur._
