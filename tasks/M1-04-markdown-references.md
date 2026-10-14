# M1-04 — Grammaire `[[ID]]` : parseurs C# et TS

- **Statut** : review
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

- [x] Les fixtures passent en xUnit **et** Vitest.
- [x] Propriétés : toute référence générée insérée dans du Markdown arbitraire est retrouvée à la bonne position ; aucune exception sur entrée aléatoire (C# et TS).
- [x] HTML brut non interprété par le pipeline TS (base d'E2E-04-07).
- [ ] Temps linéaire en la taille de l'entrée (test sur 1 Mo).

## Points d'attention

- Vérifier que Markdig et remark-gfm s'accordent sur le texte brut extrait (tableaux, listes de tâches) ; signaler tout écart.

## Notes d'implémentation

- **Grammaire** : `docs/markdown-syntax.md` (ABNF + règles R1–R12, titres et ancres, slug, texte brut), écrite avant le code ; architecture §6.3 y renvoie. Décisions qui n'étaient pas dans les documents :
  - un libellé vide ou blanc équivaut à l'absence de libellé ; il est rogné ; il s'arrête au premier `]]` et peut contenir `|`, `#`, `[` ou un `]` isolé ;
  - l'ancre n'admet que `[A-Za-z0-9_-]` ; une intégration n'a pas d'ancre (`![[ID#x]]` reste du texte, ce que fait naturellement CommonMark) ;
  - attributs : `width` (1–4096, sans zéro en tête) et `align` (`left`/`center`/`right`), chacun une fois, collés à `]]` ; un bloc invalide n'est pas consommé ;
  - **R11 — longueurs bornées** (ajout de sécurité) : libellé ≤ 200 caractères, attributs ≤ 64. Sans borne, une ligne `[[ID|[[ID|…` rendait les deux parseurs quadratiques, soit un déni de service possible en mode serveur. Testé : 20 000 libellés non fermés sur une ligne ;
  - **R12 — tableaux** : écart trouvé par les fixtures. GFM découpe une ligne de tableau sur chaque `|` nu **avant** l'analyse en ligne ; Markdig analyse d'abord. Règle retenue : dans un tableau, le séparateur s'écrit `\|` ; `\|` est accepté partout comme séparateur, et dans un libellé il vaut `|`. Côté Markdig, une référence à `|` nu dans un paragraphe-tableau (2ᵉ ligne = ligne de délimitation) est refusée, pour reproduire GFM ;
  - une référence vaut **son libellé** (ou rien) dans le texte brut et dans le calcul des ancres de titres : renommer la cible ne change ni l'index ni les ancres de la page qui la cite ;
  - ancres explicites `## Titre {#ancre}` ; doublons numérotés `-1`, `-2` à la manière de GitHub ; slug ASCII (ligatures `œ æ ß ø đ ł`, NFD, signes diacritiques retirés, `[^a-z0-9]+` → `-`, repli `section`) ;
  - HTML brut désactivé **des deux côtés** (`DisableHtml()` dans Markdig ; constructions `htmlFlow`/`htmlText` désactivées dans micromark) : le HTML devient du texte, les références qu'il contient sont trouvées de la même façon, et le rendu ne peut pas l'interpréter (base d'E2E-04-07, test : aucun nœud `html` dans l'arbre).
- **C#** (`Erable.Core/Markdown`) : Markdig 1.4.0 (BSD-2, pur, sans IO) ajouté à Core. `ReferenceSyntax` (grammaire caractère par caractère), `ErableMarkdown.Analyze` → références (id, ancre, libellé, intégration, attributs, `start`/`end` en unités UTF-16), titres (niveau, texte, ancre unique), texte brut normalisé. L'analyseur en ligne passe avant `LinkInlineParser`. Pipeline : tableaux, listes de tâches, barré `~~`, autoliens, notes, emplacement précis.
- **TS** (`src/web/src/shared/markdown`) : `referenceSyntax.ts` (même grammaire, pour valider et relire les jetons), `micromarkErable.ts` (automate micromark, `add: "before"` pour passer devant les ouvertures de lien et d'image), `mdastErable.ts` (nœuds `erableReference` / `erableEmbed`, types mdast augmentés), `remarkErable.ts` (plugin et `createMarkdownParser()` : remark-parse + remark-gfm `singleTilde: false`, comme Markdig), `slug.ts`, `analyze.ts` (références, titres, texte brut). Dépendances : unified 11, remark-parse 11, remark-gfm 4, mdast-util-from-markdown 2, micromark-util-character 2 (MIT) ; types en dev.
- **Fixtures** `spec/fixtures/markdown/` : `refs.json` (43), `edge-cases.json` (41), `plain-text.json` (17), `headings.json` (15), `slugs.json` (17), soit 133 cas, verts en xUnit **et** Vitest. Positions calculées par un générateur ponctuel (non versionné) ; les fichiers JSON font foi. CRLF, caractères astraux (offsets UTF-16), tabulation, listes imbriquées, citations, tableaux, liens imbriqués, code, échappements y figurent.
- **Propriétés** (CsCheck et fast-check) : références générées dans de la prose aléatoire retrouvées à la bonne position ; aucune exception sur entrée aléatoire. **Bug trouvé et corrigé** par ce test : un demi-surrogate isolé faisait lever `string.Normalize` dans `Slugify` (C#) ; il est désormais traité comme un séparateur, comme en JavaScript.
- **Temps linéaire** : C# vérifié sur 1 Mo de contenu mixte (×8 en taille → bien moins que ×64 en temps).
- **Point à challenger — performance de micromark** · *constat* : remark-parse **seul** (sans notre extension, en build de production) est quadratique dès que des paragraphes et des listes alternent : 2 000 paires → 0,5 s, 8 000 → 8,3 s, 16 000 (≈ 300 Ko) → 33 s. Mesuré dans `micromark` 4.0.3 (dernière version), étape `subtokenize`. · *impact* : une page de 20 Ko de ce type se rend en ≈ 0,2 s, une page de 100 Ko en plusieurs secondes ; le budget « ouverture < 200 ms » (§T-11) est tenu pour des pages courantes (Ilsa ≈ 3 Ko), pas pour de très longues pages à listes. · *options* : (a) signaler le problème en amont et attendre un correctif (coût nul, délai inconnu) ; (b) rendre les grandes pages hors du fil principal (Web Worker) et par morceaux (coût moyen, M1-11 ou M3-06) ; (c) changer de parseur côté client (markdown-it + plugin) : coût élevé, toute la chaîne unified/rehype-sanitize serait à reprendre. · *recommandation* : (a) tout de suite, puis (b) si M3-06 montre des pages réelles dépassant le budget. En attendant, le test TS de linéarité porte sur 1 Mo **sans** cette alternance, et un second test vérifie que notre extension n'ajoute pas de surcoût (≤ 2×) au cas pathologique.
- Écart Markdig ↔ remark-gfm sur le texte brut : aucun sur les tableaux et les listes de tâches une fois R12 posée. Les notes de bas de page ne sont pas couvertes par les fixtures (rendu et texte brut non comparés).

**Vérification** : `dotnet test --solution Erable.sln` (271 réussis) · `dotnet format --verify-no-changes` · `pnpm -C src/web test` (251), `typecheck`, `lint`, `format:check`.

## Propositions

- Signaler en amont (micromark) la complexité quadratique sur l'alternance paragraphes/listes, avec le cas minimal `"mot\n\n- x\n\n".repeat(n)`.
- M3-06 : mesurer l'ouverture de pages longues réalistes ; si le budget est dépassé, rendre les grandes pages dans un Web Worker.
- L'éditeur (M1-12/M1-13) pourrait insérer automatiquement `\|` quand on ajoute un libellé à une référence placée dans un tableau.
