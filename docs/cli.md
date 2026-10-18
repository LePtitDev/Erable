# `erable-cli` — extraire un projet

`erable-cli` lit un projet Erable et le restitue en **JSON** (sur la sortie standard) ou sous forme d'un **dossier d'export** (JSON, Markdown, pièces jointes), pour qu'un autre outil l'exploite : génération de site, indexation, statistiques, migration, IA…

- **Lecture seule.** Il ne modifie jamais le projet : il lit un instantané de la base (copie SQLite cohérente dans un dossier temporaire, supprimée à la fin). Il fonctionne donc aussi quand le projet est **ouvert** dans l'application ou servi par `erable serve`, sans prendre son verrou.
- **Formats anciens.** Un projet d'un format plus ancien est lu après migration **de la copie** ; le projet lui-même reste tel quel. Un format plus récent que l'outil est refusé (code 2).
- **Corbeille exclue.** Seuls les éléments vivants sont restitués.

Obtenir l'outil : l'archive `erable-cli-<rid>.zip|tar.gz` jointe à chaque release (un exécutable autonome, sans .NET à installer), `scripts/publish.sh` (dans `artifacts/cli/`), ou depuis les sources : `dotnet run --project src/Erable.Cli -- <commande> …`.

---

## 1. Contrat

Le contrat porte sur les **commandes, arguments, options, codes de sortie** et la **forme des documents JSON**. Sa version est `erable-cli/1`, écrite dans le champ `contract` de chaque document.

| Changement | Effet sur la version |
|---|---|
| ajouter une commande, une option, un champ, une valeur d'énumération documentée comme extensible | aucun : un consommateur doit **ignorer les champs inconnus** |
| retirer ou renommer une commande, une option, un champ ; changer le type ou le sens d'un champ ; changer un code de sortie | nouvelle version (`erable-cli/2`) |

Les tests `tests/Erable.Cli.Tests/ContractTests.cs` listent les champs : un retrait ou un renommage les fait échouer.

Conventions de tous les documents :

- JSON UTF-8 sans BOM, un document par commande, terminé par un saut de ligne ; compact par défaut, indenté avec `--pretty` ;
- **tous les champs documentés sont toujours présents** : une valeur absente vaut `null`, une liste vide `[]` ;
- ID : chaîne de 16 caractères hexadécimaux majuscules (`3F2A9C14E07B5D21`), jamais un nombre ;
- dates : UTC ISO 8601 avec millisecondes, `2026-10-13T08:00:00.000Z` ;
- `kind` d'un élément : `root`, `folder`, `page`, `category`, `propertyDefinition`, `linkType`, `attachment` ;
- ordre : celui de l'arbre (parcours en profondeur depuis les racines Pages, Catégories, Propriétés, Liens ; frères dans leur ordre), sauf mention contraire. Deux exécutions sur le même projet donnent la même sortie.

### Codes de sortie

| Code | Sens | Erreur |
|---|---|---|
| 0 | succès | — |
| 1 | ligne de commande invalide (commande, option ou argument manquant ou inconnu) | texte d'aide |
| 2 | le dossier n'est pas un projet lisible, ou son format est trop récent | `project.notAProject`, `project.manifestInvalid`, `project.formatTooNew`, `project.inaccessible` |
| 3 | aucun élément vivant n'a cet ID, ou il n'est pas du type attendu, ou l'ID est mal formé | `element.notFound`, `element.notAPage`, `element.notACategory`, `element.notAnAttachment`, `element.invalidId` |
| 4 | la sortie ne peut pas être écrite, ou le contenu d'une pièce jointe manque | `output.notEmpty`, `output.writeFailed`, `attachment.contentMissing` |

En cas d'erreur (codes 2 à 4), la sortie standard est vide et la sortie d'erreur contient une ligne JSON :

```json
{"error":{"code":"element.notFound","message":"No live element 0123456789ABCDEF in the project."}}
```

Le `code` est stable ; le `message` est un texte en anglais pour un humain, qui peut changer.

---

## 2. Commandes

```text
erable-cli info        <projet>
erable-cli tree        <projet> [--kind <kind>]…
erable-cli page        <projet> <id> [--no-content]
erable-cli pages       <projet> [--under <id>] [--category <id>] [--no-content]
erable-cli schema      <projet>
erable-cli links       <projet>
erable-cli attachments <projet>
erable-cli attachment  <projet> <id> [--output <fichier>]
erable-cli export      <projet> --output <dossier> [--markdown] [--no-attachments] [--force]

Options communes : --pretty (JSON indenté), --help, --version
```

`<projet>` est le dossier qui contient `erable.json`. Un `<id>` peut être tapé en minuscules.

| Commande | Document produit |
|---|---|
| `info` | identité du projet, nombre d'éléments par type, version de l'outil |
| `tree` | tous les éléments vivants (racines, dossiers, pages, catégories, propriétés, types de liens, pièces jointes) ; `--kind` (répétable) n'en garde que certains types |
| `page` | une page complète : Markdown, texte brut, titres, catégories, propriétés **effectives** (après héritage) avec leur origine et les règles non respectées, références `[[ID]]`, rétroliens, liens typés, pièces jointes ; `--no-content` omet `markdown` et `plainText` |
| `pages` | toutes les pages, sous la même forme ; `--under <id>` garde les descendantes d'un dossier ou d'une page, `--category <id>` celles de la catégorie ou d'une de ses sous-catégories |
| `schema` | catégories (parent, description, valeurs qu'elles donnent à leurs pages), définitions de propriétés (type, défaut, règles, options d'énumération), types de liens |
| `links` | le graphe du projet : liens typés entre pages et références `[[ID]]` de toutes les pages |
| `attachments` | métadonnées des pièces jointes |
| `attachment` | **contenu brut** d'une pièce jointe, sur la sortie standard (binaire) ou dans `--output` (écrasé) |
| `export` | tout le projet dans un dossier (§4) ; affiche un résumé |

---

## 3. Documents

Chaque document commence par `contract` et `project` :

```json
{
  "contract": "erable-cli/1",
  "project": {
    "id": "CA0B0E5EC0A5D1E1",
    "name": "Chroniques d'Aube",
    "contentLanguage": "fr",
    "formatVersion": 4,
    "createdAt": "2026-03-10T09:00:00.000Z",
    "folder": "/home/camille/wiki/chroniques"
  }
}
```

`formatVersion` est celui de la lecture (format courant, la copie ayant été migrée si besoin) ; `folder` le chemin absolu lu.

### `info`

```json
{ "contract": "…", "project": { … },
  "counts": { "pages": 9, "folders": 8, "categories": 11, "properties": 7, "linkTypes": 3, "links": 2, "attachments": 5, "attachmentBytes": 78692 },
  "tool": { "version": "1.0.0" } }
```

### Élément (`tree` → `elements[]`, `page.element`)

```json
{ "id": "3F2A9C14E07B5D21", "kind": "page", "parentId": "C965A137BFFA0B7E", "name": "Ilsa Varenne",
  "sortOrder": 65536, "root": "pages", "path": ["Personnages", "PNJ", "Ilsa Varenne"],
  "revision": 1, "createdAt": "…", "createdBy": "camille", "updatedAt": "…", "updatedBy": "camille" }
```

- `parentId` : `null` pour les quatre racines ;
- `root` : `pages`, `categories`, `properties` ou `links` (racine sous laquelle vit l'élément) ;
- `path` : noms depuis le premier niveau sous la racine jusqu'à l'élément inclus (`[]` pour une racine). Les noms peuvent contenir `/` : utiliser la liste, pas une chaîne jointe.

### Page (`page` → `page`, `pages` → `pages[]`, export `pages/<ID>.json`)

| Champ | Contenu |
|---|---|
| `element` | l'élément ci-dessus |
| `categories[]` | `{id, kind, name}` dans l'ordre de la page |
| `properties[]` | propriétés effectives, triées par nom (voir ci-dessous) |
| `markdown`, `plainText` | source et texte sans balisage ; `null` avec `--no-content` |
| `headings[]` | `{level, text, anchor}` ; `anchor` est la cible de `[[ID#anchor]]` |
| `references[]` | dans l'ordre du texte : `{targetId, anchor, label, embed, attributes, state, target}` ; `attributes` = `{width, align}` d'un `![[…]]{…}` ou `null` ; `state` = `existing`, `deleted` (à la corbeille) ou `unknown` ; `target` = `{id, kind, name}` si `existing`, sinon `null` |
| `backlinks[]` | `{id, kind, name}` des éléments dont le contenu référence la page |
| `links` | `{outgoing[], incoming[]}`, chacun `{linkId, typeId, type, label, pageId, pageName}` ; `label` est le nom du type (sortant) ou son nom inverse (entrant) |
| `attachments[]` | pièces jointes de la page (voir `attachments`) |

Propriété effective :

```json
{ "propertyId": "44B107E9C2A63F58", "key": "niveau", "name": "Niveau", "type": "int",
  "value": 72, "origin": "own", "sourceIds": [], "ownValue": 72, "candidates": [],
  "violations": [ { "rule": { "op": "less_or_equal", "value": 60 }, "index": null } ] }
```

- `type` : `bool`, `int`, `float`, `string`, `text`, `enum`, `date`, `ref`, ou `list<…>` de l'un d'eux ;
- `value` : la valeur effective dans la forme JSON du type — booléen, nombre, chaîne (`text`/`string`), clé d'option (`enum`), `"yyyy-MM-dd"` (`date`), ID (`ref`), tableau (`list<…>`) ; `null` en cas de conflit ;
- `origin` : `own` (définie sur la page), `inherited` (d'une catégorie, listée dans `sourceIds`), `default` (défaut de la définition), `conflict` (des catégories non liées divergent : les valeurs sont dans `candidates[]`, `{categoryId, value}`) ;
- `ownValue` : la valeur posée sur la page elle-même, `null` si elle n'en a pas (elle peut différer de `value` en conflit) ;
- `violations[]` : règles non respectées (elles avertissent, elles ne bloquent pas) ; `rule` a la forme JSON des règles de l'API, `index` désigne l'élément fautif d'une liste ou vaut `null`.

### `schema`

- `categories[]` : `{id, name, path, parentCategoryId, description, values[]}` ; `values[]` = `{propertyId, key, value}` posées sur la catégorie, héritées par ses pages ;
- `properties[]` : `{id, name, key, description, type, default, rules[], options[]}` ; `options[]` = `{key, label}` pour une énumération ;
- `linkTypes[]` : `{id, name, description, inverseName}`.

### `links`

- `links[]` : `{id, typeId, type, sourceId, targetId, createdAt, createdBy}`, entre pages vivantes, par page source ;
- `references[]` : `{sourceId, targetId, anchor, embed, state}`, toutes les références `[[ID]]` / `![[ID]]` des pages, par page source puis position.

### `attachments`

`attachments[]` : `{id, pageId, fileName, mime, size, sha256, file}`. `file` est le chemin relatif où `export` écrit le contenu : `attachments/<ID>/<nom>`, où `<nom>` est `fileName` rendu valide sur tous les systèmes (`<>:"/\|?*` et caractères de contrôle → `_`, points et espaces finaux retirés, nom réservé Windows comme `CON` préfixé de `_`).

---

## 4. Export

```bash
erable-cli export ~/wiki/chroniques --output ~/exports/chroniques --markdown
```

```text
<dossier>/
├─ project.json        document « info »
├─ tree.json           document « tree »
├─ schema.json         document « schema »
├─ links.json          document « links »
├─ attachments.json    document « attachments »
├─ pages/
│  ├─ <ID>.json        document « page » (contract, project, page)
│  └─ <ID>.md          avec --markdown
└─ attachments/
   └─ <ID>/<nom>       contenu brut, sauf avec --no-attachments
```

- Fichiers JSON indentés, nommés par ID : un renommage ou un déplacement dans le wiki ne change pas les noms de fichiers, et deux exports du même état sont identiques octet pour octet (un `diff` montre ce qui a changé).
- Dossier de destination non vide : refusé (code 4) sauf avec `--force`, qui remplace **uniquement** les entrées ci-dessus et laisse le reste du dossier intact.
- Une pièce jointe dont le contenu manque dans le projet est listée dans `missingAttachments` du résumé, sans faire échouer l'export (`erable doctor` en dit plus).

Résumé affiché : `{contract, project, output, pages, attachments, markdown, missingAttachments[]}`.

### Markdown (`--markdown`)

Chaque page devient un Markdown lisible par n'importe quel outil (éditeur, générateur de site statique) :

```markdown
---
id: "3F2A9C14E07B5D21"
title: "Ilsa Varenne"
path: ["Personnages","PNJ","Ilsa Varenne"]
categories: ["PNJ","Marchand"]
properties:
  "hostile": false
  "niveau": 72
  "region": "5B19C0E2A4D7F311"
  "role": "marchande"
updatedAt: "2026-10-13T06:00:00.000Z"
updatedBy: "camille"
---

Ilsa Varenne tient le comptoir d'échange de [Hautbois](<5B19C0E2A4D7F311.md>)…

![Portrait par Malo L.](<../attachments/91F88043221F6AF8/portrait_ilsa.png>)
```

- **Front matter** YAML : chaque valeur est écrite en JSON (sous-ensemble valide de YAML) ; `properties` contient les valeurs effectives par clé (`null` en conflit).
- **Références** : `[[ID]]` vers une page → `[nom ou libellé](<ID.md#ancre>)` ; vers une pièce jointe → lien vers `../attachments/<ID>/<nom>`, ou image `![…](…)` pour un `![[ID]]` d'image (sauf SVG, jamais affiché en ligne, §A-9) ; vers une catégorie, une propriété, un type de lien ou un dossier → son nom en texte ; vers un élément supprimé ou inconnu → laissée telle quelle. Une référence dans du code (`` `[[ID]]` ``, bloc de code) n'est pas une référence et reste inchangée.
- Le reste du texte est conservé à l'identique (syntaxe Erable comprise : `==surlignage==`, ancres explicites `{#…}`, attributs d'embed, que d'autres outils afficheront tels quels).

---

## 5. Exemples

```bash
# Toutes les pages de la catégorie PNJ (et sous-catégories), sans le texte, avec jq
erable-cli pages ~/wiki/chroniques --category 0D7EA95162C3B804 --no-content \
  | jq -r '.pages[] | [.element.name, (.properties[] | select(.key=="niveau") | .value)] | @tsv'

# Pages dont une propriété ne respecte pas une règle
erable-cli pages ~/wiki/chroniques --no-content \
  | jq '.pages[] | select(any(.properties[]; .violations != [])) | .element.name'

# Une image
erable-cli attachment ~/wiki/chroniques 91F88043221F6AF8 --output portrait.png
```
