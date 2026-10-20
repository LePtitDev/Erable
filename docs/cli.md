# `erable-cli` — extracting a project

`erable-cli` reads an Erable project and returns it as **JSON** (on the standard output) or as an **export folder** (JSON, Markdown, attachments), so that another tool can use it: site generation, indexing, statistics, migration, AI…

- **Read-only.** It never modifies the project: it reads a snapshot of the database (a consistent SQLite copy in a temporary folder, deleted at the end). It therefore also works while the project is **open** in the application or served by `erable serve`, without taking its lock.
- **Older formats.** A project in an older format is read after migrating **the copy**; the project itself stays as it is. A format newer than the tool is refused (code 2).
- **Trash excluded.** Only live elements are returned.

Getting the tool: the `erable-cli-<rid>.zip|tar.gz` archive attached to each release (a self-contained executable, no .NET to install), `scripts/publish.sh` (in `artifacts/cli/`), or from the sources: `dotnet run --project src/Erable.Cli -- <command> …`.

---

## 1. Contract

The contract covers the **commands, arguments, options, exit codes** and the **shape of the JSON documents**. Its version is `erable-cli/1`, written in the `contract` field of every document.

| Change | Effect on the version |
|---|---|
| adding a command, an option, a field, a value of an enumeration documented as extensible | none: a consumer must **ignore unknown fields** |
| removing or renaming a command, an option, a field; changing the type or meaning of a field; changing an exit code | new version (`erable-cli/2`) |

The tests `tests/Erable.Cli.Tests/ContractTests.cs` list the fields: a removal or a rename makes them fail.

Conventions of every document:

- UTF-8 JSON without BOM, one document per command, ending with a line break; compact by default, indented with `--pretty`;
- **every documented field is always present**: a missing value is `null`, an empty list `[]`;
- IDs: a string of 16 upper-case hexadecimal characters (`3F2A9C14E07B5D21`), never a number;
- dates: UTC ISO 8601 with milliseconds, `2026-10-13T08:00:00.000Z`;
- `kind` of an element: `root`, `folder`, `page`, `category`, `propertyDefinition`, `linkType`, `attachment`;
- order: the order of the tree (depth-first from the Pages, Categories, Properties, Links roots; siblings in their order), unless stated otherwise. Two runs on the same project give the same output.

### Exit codes

| Code | Meaning | Error |
|---|---|---|
| 0 | success | — |
| 1 | invalid command line (missing or unknown command, option or argument) | help text |
| 2 | the folder is not a readable project, or its format is too recent | `project.notAProject`, `project.manifestInvalid`, `project.formatTooNew`, `project.inaccessible` |
| 3 | no live element has this ID, or it is not of the expected kind, or the ID is malformed | `element.notFound`, `element.notAPage`, `element.notACategory`, `element.notAnAttachment`, `element.invalidId` |
| 4 | the output cannot be written, or the content of an attachment is missing | `output.notEmpty`, `output.writeFailed`, `attachment.contentMissing` |

On error (codes 2 to 4), the standard output is empty and the error output contains one JSON line:

```json
{"error":{"code":"element.notFound","message":"No live element 0123456789ABCDEF in the project."}}
```

The `code` is stable; the `message` is an English text for a human, and may change.

---

## 2. Commands

```text
erable-cli info        <project>
erable-cli tree        <project> [--kind <kind>]…
erable-cli page        <project> <id> [--no-content]
erable-cli pages       <project> [--under <id>] [--category <id>] [--no-content]
erable-cli schema      <project>
erable-cli links       <project>
erable-cli attachments <project>
erable-cli attachment  <project> <id> [--output <file>]
erable-cli export      <project> --output <folder> [--markdown] [--no-attachments] [--force]

Common options: --pretty (indented JSON), --help, --version
```

`<project>` is the folder that contains `erable.json`. An `<id>` may be typed in lower case.

| Command | Document produced |
|---|---|
| `info` | identity of the project, number of elements per kind, version of the tool |
| `tree` | every live element (roots, folders, pages, categories, properties, link types, attachments); `--kind` (repeatable) keeps only some kinds |
| `page` | a complete page: Markdown, plain text, headings, categories, **effective** properties (after inheritance) with their origin and the broken rules, `[[ID]]` references, backlinks, typed links, attachments; `--no-content` omits `markdown` and `plainText` |
| `pages` | every page, in the same shape; `--under <id>` keeps the descendants of a folder or a page, `--category <id>` those of the category or of one of its sub-categories |
| `schema` | categories (parent, description, values they give to their pages), property definitions (type, default, rules, enumeration options), link types |
| `links` | the graph of the project: typed links between pages and the `[[ID]]` references of every page |
| `attachments` | metadata of the attachments |
| `attachment` | **raw content** of an attachment, on the standard output (binary) or in `--output` (overwritten) |
| `export` | the whole project in a folder (§4); prints a summary |

---

## 3. Documents

Every document starts with `contract` and `project`:

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

`formatVersion` is the one that was read (the current format, the copy having been migrated if needed); `folder` is the absolute path that was read.

### `info`

```json
{ "contract": "…", "project": { … },
  "counts": { "pages": 9, "folders": 8, "categories": 11, "properties": 7, "linkTypes": 3, "links": 2, "attachments": 5, "attachmentBytes": 78692 },
  "tool": { "version": "1.0.0" } }
```

### Element (`tree` → `elements[]`, `page.element`)

```json
{ "id": "3F2A9C14E07B5D21", "kind": "page", "parentId": "C965A137BFFA0B7E", "name": "Ilsa Varenne",
  "sortOrder": 65536, "root": "pages", "path": ["Personnages", "PNJ", "Ilsa Varenne"],
  "revision": 1, "createdAt": "…", "createdBy": "camille", "updatedAt": "…", "updatedBy": "camille" }
```

- `parentId`: `null` for the four roots;
- `root`: `pages`, `categories`, `properties` or `links` (the root the element lives under);
- `path`: names from the first level under the root down to the element included (`[]` for a root). Names may contain `/`: use the list, not a joined string.

### Page (`page` → `page`, `pages` → `pages[]`, export `pages/<ID>.json`)

| Field | Content |
|---|---|
| `element` | the element above |
| `categories[]` | `{id, kind, name}` in the order of the page |
| `properties[]` | effective properties, sorted by name (see below) |
| `markdown`, `plainText` | source and text without markup; `null` with `--no-content` |
| `headings[]` | `{level, text, anchor}`; `anchor` is the target of `[[ID#anchor]]` |
| `references[]` | in text order: `{targetId, anchor, label, embed, attributes, state, target}`; `attributes` = `{width, align}` of a `![[…]]{…}` or `null`; `state` = `existing`, `deleted` (in the trash) or `unknown`; `target` = `{id, kind, name}` when `existing`, otherwise `null` |
| `backlinks[]` | `{id, kind, name}` of the elements whose content references the page |
| `links` | `{outgoing[], incoming[]}`, each `{linkId, typeId, type, label, pageId, pageName}`; `label` is the name of the type (outgoing) or its inverse name (incoming) |
| `attachments[]` | attachments of the page (see `attachments`) |

Effective property:

```json
{ "propertyId": "44B107E9C2A63F58", "key": "niveau", "name": "Niveau", "type": "int",
  "value": 72, "origin": "own", "sourceIds": [], "ownValue": 72, "candidates": [],
  "violations": [ { "rule": { "op": "less_or_equal", "value": 60 }, "index": null } ] }
```

- `type`: `bool`, `int`, `float`, `string`, `text`, `enum`, `date`, `ref`, or `list<…>` of one of them;
- `value`: the effective value in the JSON form of the type — boolean, number, string (`text`/`string`), option key (`enum`), `"yyyy-MM-dd"` (`date`), ID (`ref`), array (`list<…>`); `null` on conflict;
- `origin`: `own` (set on the page), `inherited` (from a category, listed in `sourceIds`), `default` (default of the definition), `conflict` (unrelated categories disagree: the values are in `candidates[]`, `{categoryId, value}`);
- `ownValue`: the value set on the page itself, `null` if it has none (it may differ from `value` on conflict);
- `violations[]`: broken rules (they warn, they do not block); `rule` has the JSON form of the rules of the API, `index` designates the faulty item of a list or is `null`.

### `schema`

- `categories[]`: `{id, name, path, parentCategoryId, description, values[]}`; `values[]` = `{propertyId, key, value}` set on the category, inherited by its pages;
- `properties[]`: `{id, name, key, description, type, default, rules[], options[]}`; `options[]` = `{key, label}` for an enumeration;
- `linkTypes[]`: `{id, name, description, inverseName}`.

### `links`

- `links[]`: `{id, typeId, type, sourceId, targetId, createdAt, createdBy}`, between live pages, by source page;
- `references[]`: `{sourceId, targetId, anchor, embed, state}`, every `[[ID]]` / `![[ID]]` reference of the pages, by source page then position.

### `attachments`

`attachments[]`: `{id, pageId, fileName, mime, size, sha256, file}`. `file` is the relative path where `export` writes the content: `attachments/<ID>/<name>`, where `<name>` is `fileName` made valid on every system (`<>:"/\|?*` and control characters → `_`, trailing dots and spaces removed, a Windows reserved name such as `CON` prefixed with `_`).

---

## 4. Export

```bash
erable-cli export ~/wiki/chroniques --output ~/exports/chroniques --markdown
```

```text
<folder>/
├─ project.json        "info" document
├─ tree.json           "tree" document
├─ schema.json         "schema" document
├─ links.json          "links" document
├─ attachments.json    "attachments" document
├─ pages/
│  ├─ <ID>.json        "page" document (contract, project, page)
│  └─ <ID>.md          with --markdown
└─ attachments/
   └─ <ID>/<name>      raw content, except with --no-attachments
```

- Indented JSON files, named by ID: a rename or a move in the wiki does not change the file names, and two exports of the same state are identical byte for byte (a `diff` shows what changed).
- Non-empty destination folder: refused (code 4) unless `--force` is given, which replaces **only** the entries above and leaves the rest of the folder untouched.
- An attachment whose content is missing from the project is listed in `missingAttachments` of the summary, without failing the export (`erable doctor` tells more).

Summary printed: `{contract, project, output, pages, attachments, markdown, missingAttachments[]}`.

### Markdown (`--markdown`)

Each page becomes Markdown readable by any tool (editor, static site generator):

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

- YAML **front matter**: each value is written as JSON (a valid subset of YAML); `properties` holds the effective values by key (`null` on conflict).
- **References**: `[[ID]]` to a page → `[name or label](<ID.md#anchor>)`; to an attachment → a link to `../attachments/<ID>/<name>`, or an image `![…](…)` for a `![[ID]]` of an image (except SVG, never shown inline, §A-9); to a category, a property, a link type or a folder → its name as text; to a deleted or unknown element → left as it is. A reference in code (`` `[[ID]]` ``, code block) is not a reference and stays unchanged.
- The rest of the text is kept identical (Erable syntax included: `==highlight==`, explicit anchors `{#…}`, embed attributes, which other tools will show as they are).

---

## 5. Examples

```bash
# Every page of the PNJ category (and sub-categories), without the text, with jq
erable-cli pages ~/wiki/chroniques --category 0D7EA95162C3B804 --no-content \
  | jq -r '.pages[] | [.element.name, (.properties[] | select(.key=="niveau") | .value)] | @tsv'

# Pages where a property breaks a rule
erable-cli pages ~/wiki/chroniques --no-content \
  | jq '.pages[] | select(any(.properties[]; .violations != [])) | .element.name'

# An image
erable-cli attachment ~/wiki/chroniques 91F88043221F6AF8 --output portrait.png
```
