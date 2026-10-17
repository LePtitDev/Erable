# Erable — Syntaxe Markdown étendue

> Statut : **validé avec M1-04** · Version 1.0 · 14 octobre 2026
> Complète `docs/architecture.md` §6. Spécification exécutable : `spec/fixtures/markdown/*.json`, lancée par xUnit (`Erable.Core.Tests`) **et** Vitest (`src/web`). Une règle de ce document sans cas de fixture n'est pas une règle.

Le backend (Markdig) **extrait** les références, les titres et le texte brut ; le frontend (micromark / remark) **rend** la page. Les deux implémentations suivent cette grammaire et sont vérifiées sur les mêmes cas.

---

## 1. Base

- **CommonMark 0.31 + GFM** : tableaux, listes de tâches, barré (`~~`), autoliens étendus (`www.…`, `https://…`), notes de bas de page.
- **HTML brut désactivé** (§A-9, F04.R5) : `<img src=x onerror=…>` n'est pas une balise mais du texte, affiché échappé. Les autoliens `<https://…>` restent des liens.
- Les extensions ci-dessous sont des constructions **en ligne** (*inline*) : elles sont reconnues partout où CommonMark analyse du texte en ligne (paragraphes, titres, listes, citations, cellules de tableau, texte d'un lien) et **nulle part ailleurs** : ni dans un code en ligne, ni dans un bloc de code (indenté ou délimité), ni dans une destination de lien.

## 2. Grammaire

```abnf
reference   = "[[" id [ "#" anchor ] [ separator label ] "]]"
embed       = "![[" id [ separator label ] "]]" [ attributes ]
separator   = "|" / "\|"                    ; "\|" obligatoire dans un tableau (R12)

id          = 16HEXDIG                      ; insensible à la casse, différent de 0000000000000000
anchor      = 1*( ALPHA / DIGIT / "-" / "_" )
label       = 0*200( any character except line ending ) ; s'arrête au premier "]]"
attributes  = "{" attribute *( 1*SP attribute ) "}" ; 64 caractères au plus entre les accolades
attribute   = "width=" width / "align=" align
width       = 1*4DIGIT                      ; entier de 1 à 4096, sans zéro en tête
align       = "left" / "center" / "right"
```

### 2.1 Règles

| # | Règle | Exemple |
|---|---|---|
| R1 | L'ID fait **exactement** 16 caractères hexadécimaux, dans n'importe quelle casse. 15 ou 17 caractères, un caractère non hexadécimal ou l'ID nul : **pas** une référence, le texte reste tel quel. | `[[3F2A9C14E07B5D2]]` → texte |
| R2 | Le **libellé** commence après le premier `\|` et s'arrête au **premier** `]]`. Il peut contenir `\|`, `#`, `[` ou un `]` isolé. Ses espaces de début et de fin sont retirés ; un libellé vide équivaut à l'absence de libellé. Pas de saut de ligne. | `[[ID\|a \| b]]` → libellé `a \| b` |
| R3 | L'**ancre** suit `#` et précède `\|` ou `]]` ; elle n'admet que lettres ASCII, chiffres, `-` et `_`. Sinon, pas de référence. | `[[ID#les-annees]]` |
| R4 | Une **intégration** (`![[…]]`) n'a pas d'ancre : `![[ID#x]]` n'est ni une intégration ni une référence, il reste du texte (CommonMark y voit une image sans destination). | |
| R5 | Les **attributs** ne s'appliquent qu'à une intégration, collés à `]]`. Clés connues seulement (`width`, `align`), chacune au plus une fois, séparées par des espaces. Un bloc d'attributs invalide n'est pas consommé : il reste du texte. | `![[ID]]{width=320 align=right}` |
| R6 | **Échappement** : `\[[ID]]` n'est pas une référence (le premier `[` est échappé). Il n'existe pas d'autre échappement à l'intérieur d'une référence. | |
| R7 | **Code** : rien n'est reconnu dans un code en ligne ni dans un bloc de code. | `` `[[ID]]` `` → code |
| R8 | **Liens imbriqués** : une référence dans le texte d'un lien Markdown est extraite (elle compte dans les liens entrants) ; au rendu, elle n'est pas cliquable séparément (pas de lien dans un lien). `[[ID]](https://x)` est une référence suivie du texte `(https://x)`. | `[voir [[ID]]](https://x)` |
| R9 | **Crochets** : `[[[ID]]]` donne `[`, la référence, puis `]`. | |
| R10 | **Position** : `start` est l'index (en unités UTF-16, identique en C# et en JavaScript) du premier caractère (`[` ou `!`) ; `end` est l'index qui suit le dernier caractère consommé, attributs compris. | |
| R11 | **Longueurs bornées** : un libellé de plus de 200 caractères (avant rognage) ou un bloc d'attributs de plus de 64 caractères n'est pas reconnu. Cette borne garantit un temps d'analyse linéaire même sur une ligne hostile (`[[ID\|[[ID\|…` sans fin). | |
| R12 | **Tableaux** : dans une ligne de tableau GFM, une barre verticale non échappée sépare les cellules **avant** toute analyse en ligne : une référence à libellé écrite avec une barre nue y est coupée en deux cellules et n'est pas une référence. On y écrit le séparateur `\|` précédé d'une barre oblique inverse. Partout, cette forme échappée est acceptée comme séparateur, et une barre échappée à l'intérieur d'un libellé vaut une barre simple. | voir `refs.json` › *table* |

### 2.2 Texte brut d'une référence

Pour le texte brut (recherche plein texte) et le calcul des ancres de titres, une référence vaut **son libellé**, ou rien sans libellé : le nom de la cible peut changer, il ne doit pas modifier le texte indexé de la page qui la cite ni les ancres de ses titres.

## 3. Titres et ancres

- Chaque titre reçoit une ancre. Par défaut c'est le **slug** de son texte brut.
- **Ancre explicite** : un titre qui se termine par ` {#ancre}` (espace, puis `{#`, une ancre au sens de R3, puis `}`) prend cette ancre, et ce suffixe est retiré du texte affiché. `## Histoire {#histoire}`.
- **Doublons** dans une page : la deuxième occurrence d'une ancre reçoit `-1`, la troisième `-2`, etc. (comme GitHub). Les ancres explicites participent au comptage.

### 3.1 Slug

1. Remplacer les ligatures et lettres sans décomposition : `œ→oe`, `æ→ae`, `ß→ss`, `ø→o`, `đ→d`, `ł→l` (majuscules comprises) ;
2. décomposer en NFD et retirer les signes diacritiques (catégorie Unicode `Mn`) ;
3. passer en minuscules (invariant) ;
4. remplacer chaque suite de caractères hors `[a-z0-9]` par un seul `-` ;
5. retirer les `-` de début et de fin ; un résultat vide devient `section`.

« Les années de cartographe » → `les-annees-de-cartographe`.

## 4. Texte brut

Utilisé pour l'index plein texte (M3) et comparé entre les deux implémentations après **normalisation** : toute suite d'espaces (dont les sauts de ligne) devient une espace, puis le tout est rogné.

- Texte de tous les blocs, y compris les blocs de code et le code en ligne ;
- références : §2.2 ; intégrations : leur libellé ;
- liens et images : leur texte (pas l'URL) ; autoliens : l'URL ;
- cellules de tableau séparées par des espaces ; case à cocher d'une tâche ignorée ;
- le suffixe `{#ancre}` d'un titre est exclu.

## 5. Ce qui n'est pas prévu

- Pas de référence dans les attributs, les destinations de lien ni les définitions de référence (`[x]: url`).
- Pas d'intégration de section (`![[ID#ancre]]`).
- Pas d'autre attribut que `width` et `align` ; tout ajout passe par ce document, puis par les fixtures.

## 6. Limites de sécurité

Les analyseurs (Markdig côté serveur, remark/micromark côté navigateur) ont un temps **quadratique**, voire un dépassement de pile, sur quelques dizaines de Ko d'entrée dégénérée : `]` répété 40 000 fois fige le navigateur près de 40 s, `> ` répété 8 000 fois lève une `RangeError`. Un même éditeur pourrait ainsi figer ses collègues ou saturer le serveur. Un pré-balayage **linéaire** refuse ces formes avant tout analyseur, au serveur (`MarkdownLimits`, erreur 422, rien n'est écrit) et dans le client (message avant l'envoi). Une page réelle n'approche aucune de ces valeurs.

| Règle | Limite | Code d'erreur |
|---|---|---|
| Taille de la page | 1 048 576 unités UTF-16 (≈ 1 Mo) | `page.content.tooLarge` |
| Longueur d'une ligne | 10 000 caractères | `page.content.lineTooLong` |
| Répétition de `[` `]` `(` `)` `>` | 256 d'affilée | `page.content.repeatedCharacter` |
| Marqueurs de citation et de liste en début de ligne (`> > 1. - `) | 32 ; indentation : 256 espaces ou tabulations | `page.content.tooNested` |
| Lignes consécutives sans ligne vide | 2 000 | `page.content.paragraphTooLong` |

- La première règle enfreinte, dans l'ordre du document, est signalée avec sa ligne (1 ; 0 pour la taille). `\r\n` et `\n` sont équivalents.
- Ces limites **bloquent** l'enregistrement, contrairement aux règles de propriétés qui avertissent (§A-5.4) : ce sont des garde-fous, pas des règles métier.
- Une page déjà enregistrée qui les dépasse (importée, ou écrite avant leur existence) reste lisible : elle est affichée en **texte brut**, sans mise en forme ni sommaire, et le serveur l'indexe comme texte.
- Cas partagés : `spec/fixtures/limits/limits.json` (`md` + `unit` répété `count` fois), exécutés par xUnit et Vitest.
