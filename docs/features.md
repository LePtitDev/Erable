# Erable — Features and end-to-end scenarios

> Complements `docs/architecture.md` (§A-x) and `docs/testing.md` (§T-x).
> Role: **reference** catalogue of the v1 features and of the e2e scenarios that check them. Each scenario must be translatable as is into a Playwright test.

---

## 1. Conventions

### 1.1 Identifiers

- **Feature**: `F<nn>` (e.g. `F05` — Editing).
- **Business rule**: `F<nn>.R<n>`: an expected behaviour, verifiable, independent of the UI.
- **Scenario**: `E2E-<nn>-<nn>` (e.g. `E2E-05-03`). The ID appears **at the start of the title of the Playwright test**, followed by an English title, which makes traceability trivial (`grep E2E-05-03`). The `pnpm -C src/web e2e:trace` script checks the correspondence both ways.
- An ID is never reused. A removed scenario is marked ~~struck out~~ with the reason.

### 1.2 Format of a scenario

```gherkin
E2E-05-03 · Insert a link via the palette                     P0 · @pr · M1
Given        the page "Ilsa Varenne" open for editing
When         I type "[[orse"
Then         the palette opens with "Capitaine Orsen" first
```

- **Priority**: `P0` = blocking (data loss, central journey); `P1` = important; `P2` = comfort.
- **Tags**:

| Tag | Meaning |
|---|---|
| `@pr` | run on every PR (Chromium + WebKit) |
| `@nightly` | run only at night (3 browsers) |
| `@multi` | needs several browser contexts (several users) |
| `@server` | needs server mode (authentication) |
| `@desktop` | replayed in the desktop binary (§T-10) |
| `@mobile` | run in the Playwright `mobile` project (emulated phone, touch screen, §T-9.1); with `@pr`, on every PR |
| `@a11y` | keyboard-only journey, and axe-core check |

- **Milestone**: milestone from which the scenario must pass (§A-12).
- Texts in quotes are **interface labels**, given here in English. The tests run in the `fr` reference locale; page objects reach the labels by accessible role and label, through the i18n keys rather than hard-coded strings. Names of the reference project ("Ilsa Varenne", "Hautbois", "PNJ"…) are content and stay in French.

### 1.3 Code organization

```
src/web/e2e/
├─ fixtures/            loading of the reference project, users, clock
├─ pages/               page objects: HomePage, TreePanel, PageView, EditorPage,
│                       InsertPalette, CategoryView, PropertyView, SearchPanel…
└─ specs/
   ├─ f01-home.spec.ts
   ├─ f02-tree.spec.ts
   └─ …                 one file per feature, one `test()` per scenario
```

```ts
test("E2E-05-03 · Insert a link via the palette @pr", async ({ editor }) => { … });
```

### 1.4 Test endpoints (test mode only)

The backend started with `ERABLE_TEST_MODE=1` (never active in a Release build) exposes:

- `POST /_test/reset?fixture=chroniques&slot=<test>`: reloads the reference project (file copy, < 100 ms). Each `slot` (one per test) gets its own copy, with a new `projectId`, and its own profile (`erable-test-slot` cookie): tests run in parallel against a single backend;
- `POST /_test/clock`: sets the clock (`TimeProvider`), so that dates ("2 h ago") are stable;
- `POST /_test/login-as/{user}`: opens a session without going through the form. The sign-in form is tested only once, in `E2E-14-01`.
- `GET /_test/blobs?projectId=…`: lists the stored blobs (checking deduplication);
- `POST /_test/limits`: changes the maximum size of attachments (E2E-06-07).

**Default test clock: 13 October 2026, 10:00 (Europe/Paris).**

---

## 2. Reference data set "Chroniques d'Aube"

Versioned in `spec/projects/chroniques/`. Just enough elements to cover each scenario. **The IDs below are frozen**: the tests use them to check the stability of references.

### 2.1 Tree

```
Pages
├─ Personnages/
│  ├─ PNJ/
│  │  ├─ Ilsa Varenne              3F2A9C14E07B5D21
│  │  │  ├─ Dialogues              (sub-page)
│  │  │  └─ Inventaire             (sub-page)
│  │  └─ Capitaine Orsen           7A1C4E90B3F2D615
│  └─ Créatures/
│     └─ Dragon des Cimes          C47D2B9E0F6A1388
├─ Lieux/
│  ├─ Hautbois                     5B19C0E2A4D7F311
│  └─ Col gelé                     8C3E1F7A90B2D446
├─ Factions/
│  └─ Ordre du Givre               9E04B6A1C8D2F570
└─ Quêtes/
   └─ Le Col gelé                  2D8F5A7C1B9E0463
Catégories
├─ Entité                          E1A0000000000001
│  ├─ Personnage                   E1A0000000000002
│  │  ├─ PNJ                       0D7EA95162C3B804
│  │  │  └─ Marchand · Garde · Érudit
│  │  └─ Joueur
│  └─ Créature
└─ Région · Quête · Faction
Propriétés
├─ Personnages/  Niveau 44B107E9C2A63F58 · Rôle · Âge · Points de vie · Hostile
└─ Monde/        Région · Réputation
Liens
└─ Allié de · Situé à · Mentor de
```

### 2.2 Notable values

| Element | Data useful to the scenarios |
|---|---|
| **Ilsa Varenne** | Categories PNJ + Marchand · `Niveau = 72` (**breaks a rule**) · `Rôle = Marchande` · `Hostile` inherited from PNJ (`Non`) · `Région = Hautbois` · 4 headings (Présentation, Histoire › Les années de cartographe, Rôle dans le jeu) · references to Hautbois, Ordre du Givre, Capitaine Orsen, Le Col gelé · text containing "Col gelé" · embeds `portrait_ilsa.png` (its attachment) and `carte_hautbois.webp` (attachment of Hautbois) |
| **Capitaine Orsen** | PNJ + Garde · `Niveau = 40` · references Ilsa |
| **Dragon des Cimes** | Créature · `Niveau = 99` (**breaks a rule**) |
| **Hautbois** | Région · attachments `carte_hautbois.webp`, `bande_annonce.mp4`, `plans.zip`, `blason.svg` |
| **Le Col gelé** (quest) | Quête · text containing "col gelé" three times |
| **Col gelé** (place) | Région · name close to the quest (search tests) |
| **PNJ** | Parent Personnage · defaults `Hostile = Non`, `Niveau = 10` · sub-categories Marchand, Garde, Érudit |
| **Niveau** | `int` · rules `greater_or_equal 1`, `less_or_equal 60` · default provided by PNJ, Créature and Joueur · 2 pages breaking the rules (Ilsa, Dragon) |
| **Rôle** | `enum`: Marchande, Garde, Érudit, Voyageur |
| **Région** | `ref` · rule `in_category Région` |
| **Allié de** | Ilsa → Capitaine Orsen · inverse "Allié de" |
| **Situé à** | Ilsa → Hautbois · inverse "Lieu de" |
| **Mentor de** | no link in the data set · inverse "Élève de" |

### 2.3 Users (server mode)

| Login | Role on "Chroniques d'Aube" | Use |
|---|---|---|
| `admin` | Administrator | member management |
| `camille` | Editor | editing, multi-user |
| `malo` | Editor | second concurrent editor |
| `lea` | Reader | read-only rights |
| `externe` | none (member of another project) | isolation between projects |

The test passwords are in `spec/projects/users.json`, specific to the test environment.

---

## 3. Feature catalogue

| ID | Feature | Screen | Milestone |
|---|---|---|---|
| F01 | Home and project management | Home | M1 |
| F02 | Explorer (tree) | all | M1 |
| F03 | Stable identifiers | Page, Property | M1 |
| F04 | Reading a page | Page | M1 |
| F05 | Editing a page | Editing | M1 |
| F06 | Attachments | Page, Editing | M1 |
| F07 | Categories | Category | M2 |
| F08 | Properties and validation | Property, Editing | M2 |
| F09 | Typed links | Page | M2 |
| F10 | Search and filters | Search | M3 |
| F11 | Recently viewed / edited | Page, Search | M3 |
| F12 | Deletion and trash | — | M2 |
| F13 | Revision history | — | M3 |
| F14 | Multi-user | Page, Editing | M4 |
| F15 | Preferences: language, theme, shortcuts | Home | M1 → M5 |
| F16 | Desktop application | Home | M0 → M5 |
| F17 | Accessibility and keyboard | all | M1 → M5 |
| F18 | Mobile version (web) | all | M6 |
| F19 | Android application | — | M6 |

---

## F01 — Home and project management

**Rules**
- **F01.R1** A project is a folder containing `erable.json` (§A-7.1). Opening a folder without a manifest offers to create a project, without writing anything before confirmation.
- **F01.R2** The list of recent projects (local and server) is stored in the user profile, not in the project. A project that cannot be found stays listed, greyed out, with a "Remove" action.
- **F01.R3** A project of an older `formatVersion` is migrated when opened, after confirmation and an automatic backup (`erable.db.bak-<date>`). A format more recent than the application is refused with a clear message, without modification.
- **F01.R4** The content language is chosen at creation and frozen (decision Q1).

```gherkin
E2E-01-01 · Create an empty project                           P0 · @pr @desktop · M1
Given        the home screen
When         I choose "New project", enter the name "Atlas" and the language "Français"
Then         the project opens with the explorer and its roots Pages, Categories, Properties, Links
And          "Atlas" appears at the top of the recent projects when back on the home screen

E2E-01-02 · Reopen a recent project                           P0 · @pr · M1
Given        "Chroniques d'Aube" in the recent projects
When         I click it
Then         the project opens on the last page viewed

E2E-01-03 · Recent project not found                          P1 · @nightly · M1
Given        a recent project whose folder was deleted
Then         it is shown greyed out with the mention "Not found"
When         I click "Remove"
Then         it disappears from the list and no file is created

E2E-01-04 · Open a project of a more recent format            P1 · @nightly · M1
Given        a project whose formatVersion is greater than the one of the application
When         I open it
Then         a message says that an update of the application is needed
And          the files of the project are unchanged (identical hash)

E2E-01-05 · Migrate a project of an older format              P0 · @nightly · M2
Given        the frozen project spec/projects/legacy/v1
When         I open it and confirm the migration
Then         an erable.db.bak-* backup is created
And          every page is shown with its references resolved

E2E-01-06 · Connect to a server                               P0 · @server @desktop · M4
Given        the home screen
When         I choose "Connect to a server", enter the URL then the credentials of camille
Then         the list of the projects of the server is shown, with "Chroniques d'Aube"
And          the server appears in the recent ones with the "Server" mode
```

---

## F02 — Explorer (tree)

**Rules**
- **F02.R1** Four fixed roots, which cannot be renamed or deleted: Pages, Categories, Properties, Links.
- **F02.R2** An element can only be moved under its own root (a category does not go into Pages).
- **F02.R3** A page can be moved under another page (it becomes a sub-page), never under one of its own descendants.
- **F02.R4** Renaming or moving never changes the ID and breaks no reference (§A-4).
- **F02.R5** The expanded/collapsed state of each node is remembered per user and per project.
- **F02.R6** Two sibling elements may have the same name; a discreet indicator points it out.

```gherkin
E2E-02-01 · Create a folder and a page                        P0 · @pr @desktop · M1
When         I create the folder "Objets" under Pages, then the page "Lame de givre" in it
Then         the tree shows Pages › Objets › Lame de givre
And          the page opens for editing, title selected

E2E-02-02 · Rename a referenced page                          P0 · @pr · M1
Given        "Ilsa Varenne", which references "Capitaine Orsen"
When         I rename "Capitaine Orsen" to "Commandant Orsen" from the tree (F2)
Then         the page Ilsa Varenne shows "Commandant Orsen" where the link is
And          the Markdown source of Ilsa is unchanged (still contains 7A1C4E90B3F2D615)

E2E-02-03 · Move by drag and drop                             P0 · @pr · M1
When         I drag "Hautbois" from Lieux to Factions
Then         Hautbois appears under Factions, its breadcrumb is updated
And          the links that lead to it from Ilsa still work

E2E-02-04 · Move to another root forbidden                    P1 · @pr · M1
When         I try to drag the category "PNJ" into Pages
Then         the drop is refused (forbidden cursor), the tree is unchanged

E2E-02-05 · A page cannot become its own descendant           P1 · @nightly · M1
When         I try to drag "Ilsa Varenne" onto its sub-page "Dialogues"
Then         the drop is refused

E2E-02-06 · Reorder siblings                                  P2 · @nightly · M1
When         I drag "Col gelé" above "Hautbois"
Then         the order is kept after reloading

E2E-02-07 · Remember the expanded state                       P2 · @nightly · M1
Given        Pages › Personnages › PNJ expanded and Lieux collapsed
When         I reload the application
Then         the tree is in the same state

E2E-02-08 · Root counters                                     P2 · @nightly · M1
Then         each root shows its number of elements (folders excluded), updated after a creation or a deletion
```

---

## F03 — Stable identifiers

**Rules**
- **F03.R1** Each element receives a random 64-bit ID at creation, shown as `XXXX XXXX XXXX XXXX`.
- **F03.R2** The ID can be copied in one click, in the canonical format (16 characters, no spaces).
- **F03.R3** The URL of an element contains its ID; it stays valid after renaming and moving.

```gherkin
E2E-03-01 · Show and copy the ID                              P1 · @pr · M1
Given        the page "Ilsa Varenne"
Then         the header shows "3F2A 9C14 E07B 5D21"
When         I click the ID
Then         the clipboard contains "3F2A9C14E07B5D21" and a brief confirmation is shown

E2E-03-02 · Permanent link                                    P0 · @pr · M1
Given        the URL of "Hautbois" saved
When         I rename Hautbois and move it under Factions, then open the saved URL
Then         the page Hautbois is shown under its new name

E2E-03-03 · Unknown ID in the URL                             P1 · @nightly · M1
When         I open the URL of a nonexistent ID
Then         an "Element not found" page is shown with access to the search, without a technical error
```

---

## F04 — Reading a page

**Rules**
- **F04.R1** A `[[ID]]` reference shows the **current name** of the target; `[[ID|text]]` shows the text.
- **F04.R2** A reference to a deleted or nonexistent element is shown as a broken link (dedicated style, tooltip), never as an error.
- **F04.R3** The table of contents reflects the level 2 and 3 headings; a click scrolls to the heading and updates the anchor of the URL.
- **F04.R4** The side panel shows the properties (with their origin "via <category>"), the outgoing and incoming links, and the attachments.
- **F04.R5** Raw HTML in the Markdown is not interpreted (§A-9).

```gherkin
E2E-04-01 · Complete rendering of a page                      P0 · @pr @desktop · M1
When         I open "Ilsa Varenne"
Then         I see the breadcrumb Pages / Personnages / PNJ / Ilsa Varenne
And          the category pills PNJ and Marchand
And          "Edited 2 h ago by Camille R."
And          the links Hautbois, Ordre du Givre, Capitaine Orsen rendered with their name
And          the image portrait_ilsa.png and the image carte_hautbois.webp (caption "Attachment of Hautbois")

E2E-04-02 · Table of contents                                 P1 · @pr · M1
When         I click "Les années de cartographe" in the table of contents
Then         the page scrolls to the heading, the URL ends with #les-annees-de-cartographe
And          the entry is marked active in the table of contents

E2E-04-03 · Preview when hovering a reference                 P2 · @nightly · M1
When         I hover the link "Capitaine Orsen"
Then         a card shows its name, its ID, its path and the start of its content

E2E-04-04 · Navigating through a reference                    P0 · @pr · M1
When         I click "Hautbois" in the text
Then         the page Hautbois opens and going back returns to Ilsa, at the same scroll position

E2E-04-05 · Broken link                                       P1 · @pr · M2
Given        "Ordre du Givre" deleted
When         I open "Ilsa Varenne"
Then         the link is shown as broken with the tooltip "Deleted element — Restore"

E2E-04-06 · Sub-pages                                         P1 · @pr · M1
Then         the Sub-pages section of Ilsa lists Dialogues and Inventaire
When         I click "New sub-page"
Then         a child page of Ilsa is created and opened for editing

E2E-04-07 · HTML neutralized                                  P0 · @pr · M1
Given        a page whose Markdown contains <img src=x onerror="window.__xss=1">
When         I open it
Then         the code is not run (window.__xss undefined) and the text is shown escaped
```

---

## F05 — Editing a page

**Rules**
- **F05.R1** The editor shows a pill with the name in place of each ID; a pill behaves like a single character (cursor, deletion).
- **F05.R2** "Show raw IDs" replaces the pills with the source text, without changing the content.
- **F05.R3** The insert palette opens by typing `[[` or `![[`, or with the configured shortcut (§A-11.3): `Enter` inserts a link, `Shift+Enter` an embed, `Tab` changes the kind, `Esc` closes without inserting anything.
- **F05.R4** A local draft is saved automatically; it is offered on reopening if saving did not happen.
- **F05.R5** Leaving editing with unsaved changes asks for confirmation.
- **F05.R6** Rule violations are reported live, without blocking saving (§A-5.4).

```gherkin
E2E-05-01 · Edit and save                                     P0 · @pr @desktop · M1
Given        "Ilsa Varenne" open
When         I click "Edit", add the sentence "Elle parle peu." then press Ctrl+S
Then         the page goes back to reading and shows the sentence
And          "Edited just now by me" is shown

E2E-05-02 · Pills in place of IDs                             P0 · @pr · M1
Given        "Ilsa Varenne" being edited
Then         the editor shows the pills "Hautbois", "Capitaine Orsen" and no hexadecimal ID
When         I put the cursor right after the pill "Hautbois" and press Backspace
Then         the whole pill is deleted, nothing else

E2E-05-03 · Insert a link via the palette                     P0 · @pr · M1
When         I type "[[orse"
Then         the palette opens with "Capitaine Orsen" first, its path and its ID
When         I press Enter
Then         a pill "Capitaine Orsen" is inserted and the source contains [[7A1C4E90B3F2D615]]

E2E-05-04 · Embed an attachment of another page               P0 · @pr · M1
When         I type "![[carte", choose "Hautbois › carte_hautbois.webp" and press Shift+Enter
Then         the source contains ![[<ID of carte_hautbois.webp>]]
And          the preview shows the image

E2E-05-05 · Filter the palette by kind                        P1 · @pr · M1
When         I open the palette and press Tab three times
Then         the "Categories" tab is active and only categories are suggested

E2E-05-06 · Close the palette without inserting               P1 · @nightly · M1
When         I open the palette then press Esc
Then         the palette closes and only the typed text ("[[") stays in the editor

E2E-05-07 · Configurable shortcut                             P2 · @nightly · M5
Given        the palette shortcut set to Ctrl+Shift+L in the preferences
When         I press Ctrl+Shift+L in the editor
Then         the palette opens

E2E-05-08 · Show raw IDs                                      P1 · @pr · M1
When         I turn on "Show raw IDs"
Then         the editor shows [[7A1C4E90B3F2D615]] in place of the pill
And          the saved content is identical before and after toggling

E2E-05-09 · Draft recovered                                   P0 · @pr · M1
Given        an unsaved change of "Hautbois"
When         I abruptly close the tab then reopen Hautbois for editing
Then         the banner "An unsaved draft exists" offers "Restore" or "Ignore"
When         I choose "Restore"
Then         my change is back

E2E-05-10 · Safeguard before leaving                          P0 · @pr · M1
Given        an unsaved change
When         I click another page in the tree
Then         a confirmation offers "Save", "Discard" or "Cancel"

E2E-05-11 · Cancel editing                                    P1 · @pr · M1
When         I change the text then click "Cancel" and confirm
Then         the page returns to its saved content and the draft is deleted

E2E-05-12 · Live, non-blocking validation                     P0 · @pr · M2
Given        "Ilsa Varenne" being edited, Properties panel open
Then         "Niveau" shows "Must be less than or equal to 60" and the status bar "1 validation warning"
When         I save
Then         saving succeeds and the page carries a "breaking the rules" badge
When         I replace 72 with 55
Then         the error disappears immediately, before saving

E2E-05-13 · Formatting toolbar                                P2 · @nightly · M1
When         I select a word and click "B"
Then         the word is surrounded with ** in the source
```

---

## F06 — Attachments

**Rules**
- **F06.R1** A file pasted or dropped into the editor becomes an attachment of the current page, and `![[ID]]` is inserted at the cursor.
- **F06.R2** Images, videos, audio and PDF are embedded; other kinds are shown as a downloadable card. **SVG and HTML are never rendered inline** (§A-9).
- **F06.R3** Videos are streamed (seeking without downloading everything).
- **F06.R4** Deleting an attachment referenced elsewhere shows the list of the pages concerned before confirmation.
- **F06.R5** The maximum size is configurable; exceeding it is refused with a message, without a partial file.

```gherkin
E2E-06-01 · Paste an image                                    P0 · @pr @desktop · M1
Given        "Hautbois" being edited
When         I paste a PNG image from the clipboard
Then         an attachment pill appears at the cursor
And          the image is listed in the "Attachments" panel of Hautbois

E2E-06-02 · Drop several files                                P1 · @pr · M1
When         I drop three files (PNG, PDF, ZIP) into the editor
Then         three attachments are created with a visible progress
And          the image and the PDF are embedded, the ZIP shown as a card

E2E-06-03 · Add from the panel                                P1 · @pr · M1
When         I click "+ Add" in Attachments and choose a file
Then         it appears with its name, its extension and its size, without being inserted into the text

E2E-06-04 · Streamed video playback                           P1 · @nightly · M1
When         I embed bande_annonce.mp4 and seek playback to 80 %
Then         the video resumes at that position (a 206 Range request is observed)

E2E-06-05 · SVG not rendered inline                           P0 · @pr · M1
When         I embed blason.svg in a page
Then         it is shown as a downloadable card and is not inserted into the DOM as <svg> or <object>

E2E-06-06 · Delete a referenced attachment                    P1 · @pr · M2
When         I delete carte_hautbois.webp from Hautbois
Then         the confirmation says "Used in 1 other page: Ilsa Varenne"
When         I confirm
Then         Ilsa shows a "Deleted attachment" placeholder in place of the image

E2E-06-07 · File too large                                    P1 · @nightly · M1
Given        a limit of 10 MB
When         I drop an 11 MB file
Then         a message states the limit and no attachment is created

E2E-06-08 · Identical file deduplicated                       P2 · @nightly · M1
When         I attach the same image to two pages
Then         two distinct attachments exist, but a single blob is stored (checked through /_test)
```

---

## F07 — Categories

**Rules**
- **F07.R1** A category has a plain-text description, a single optional parent and default property values.
- **F07.R2** A reparenting that would create a cycle is refused.
- **F07.R3** A page can have several categories (decision Q4). Resolving a value: page > most specific category > default of the property. Two categories without an inheritance relation and with different values → **conflict** to resolve on the page (§A-5.3).
- **F07.R4** "Pages of the category" can include the sub-categories.

```gherkin
E2E-07-01 · View a category                                   P0 · @pr · M2
When         I open the category "PNJ"
Then         I see its description, the inheritance Entité › Personnage › PNJ
And          the sub-categories Marchand, Garde, Érudit
And          the table of properties with their source (PNJ or inherited)

E2E-07-02 · Create a category with a parent                   P0 · @pr · M2
When         I create "Forgeron" with the parent "PNJ" and the description "Artisan du métal"
Then         Forgeron inherits the default values of PNJ (Hostile = Non, source "PNJ")

E2E-07-03 · Cycle refused                                     P0 · @pr · M2
When         I choose "Marchand" as the parent of "Personnage"
Then         the choice is refused with "Would create a loop: Personnage › PNJ › Marchand › Personnage"

E2E-07-04 · Add a category to a page                          P0 · @pr · M2
When         I add the category "Érudit" to "Capitaine Orsen"
Then         the page shows the Érudit pill and its inherited properties

E2E-07-05 · Value inherited then overridden                   P0 · @pr · M2
Given        "Capitaine Orsen" with Hostile inherited from PNJ (Non)
When         I set Hostile = Oui on the page
Then         the value shown is Oui without a "via" mention
When         I click "Restore the inherited value"
Then         the value is Non again, "via PNJ"

E2E-07-06 · Conflict between categories                       P0 · @pr · M2
Given        a category "Montagnard" without a parent, with Hostile = Oui by default
When         I add Montagnard to "Ilsa Varenne" (already PNJ, Hostile = Non)
Then         Hostile is marked "In conflict: PNJ (Non) / Montagnard (Oui)"
When         I choose Non on the page
Then         the conflict disappears

E2E-07-07 · Pages of the category with sub-categories         P1 · @pr · M2
When         I open PNJ and tick "Include sub-categories"
Then         the list includes Ilsa (via Marchand) and Orsen (via Garde)
When         I click "View in search"
Then         the search opens filtered on PNJ with sub-categories

E2E-07-08 · Changing a default propagates                     P1 · @nightly · M2
When         I change the default Niveau of PNJ from 10 to 15
Then         a PNJ page without its own Niveau shows 15, a page with its own value is unchanged
```

---

## F08 — Properties and validation

**Rules**
- **F08.R1** A definition has a displayed name, a unique key, a description, a type, a default value and rules (§A-5.4).
- **F08.R2** The rules offered depend on the type; no rule for `bool`.
- **F08.R3** Rules are checked on every save; a violation **warns without blocking** and is listed on the definition ("N pages breaking the rules").
- **F08.R4** Changing a rule recomputes the violations of every page concerned.
- **F08.R5** Renaming an `enum` option updates the pages that use it; deleting an option in use asks for a replacement option.
- **F08.R6** Changing the type of a property already filled in is only allowed if the existing values are convertible; otherwise the list of blocking pages is shown.

```gherkin
E2E-08-01 · View a definition                                 P0 · @pr · M2
When         I open the property "Niveau"
Then         I see the type int, the rules "≥ 1" and "≤ 60"
And          "Used by": PNJ, Créature, Joueur · "Filled in on N pages"
And          "2 pages breaking the rules": Ilsa Varenne (72), Dragon des Cimes (99)

E2E-08-02 · Create a typed property                           P0 · @pr · M2
When         I create "Poids" of type float with the rule "greater_than 0"
Then         it is offered in "Add a property" of a page
And          entering -2 on a page shows "Must be greater than 0"

E2E-08-03 · Rules offered according to the type               P1 · @pr · M2
When         I change the type of the new property to string
Then         the rules offered are min_length, max_length, match, not_empty
When         I change it to bool
Then         the text "No rule available for a boolean" is shown

E2E-08-04 · Test a value                                      P1 · @pr · M2
When         I enter 72 in "Test a value" of Niveau
Then         the result says "Fails: less_or_equal 60"
When         I enter 30
Then         the result says "Valid"

E2E-08-05 · Changing a rule recomputes the violations         P0 · @pr · M2
When         I replace "≤ 60" with "≤ 80" on Niveau
Then         "1 page breaking the rules": Dragon des Cimes
And          the "breaking the rules" badge disappears from the page Ilsa Varenne

E2E-08-06 · Input by type on a page                           P0 · @pr · M2
Given        "Capitaine Orsen" being edited
Then         Hostile is edited with a switch, Rôle with a list, Région with a page picker filtered on the category Région
And          Âge refuses letters

E2E-08-07 · "match" rule on a string                          P1 · @nightly · M2
Given        a string property "Code" with match ^[A-Z]{3}-\d{2}$
When         I enter "ab-1" then "ABC-12"
Then         the first value is reported, the second one is valid

E2E-08-08 · Rename an enum option                             P1 · @nightly · M2
When         I rename the option "Marchande" of Rôle to "Négociante"
Then         Ilsa Varenne shows Rôle = Négociante without any other action

E2E-08-09 · Delete an enum option in use                      P1 · @nightly · M2
When         I delete the option "Garde" of Rôle
Then         a dialog asks for the replacement option for the pages concerned (Capitaine Orsen)

E2E-08-10 · Incompatible type change                          P1 · @nightly · M2
Given        Rôle filled in with text values
When         I change its type to int
Then         the change is refused with the list of the pages whose value cannot be converted

E2E-08-11 · Rename the key of a property                      P1 · @pr · M2
When         I rename the key "niveau" to "level"
Then         every page, category and saved filter that uses Niveau still works
```

---

## F09 — Typed links

**Rules**
- **F09.R1** A link type has a name, a description and an optional inverse label.
- **F09.R2** A link connects any two pages, independently of folders; it is visible from the source (outgoing) and from the target (incoming, with the inverse label).
- **F09.R3** Deleting a link type in use shows the number of links concerned before confirmation.

```gherkin
E2E-09-01 · Create a link type                                P0 · @pr · M2
When         I create the type "Rival de" with the inverse "Rival de"
Then         it appears under the Links root

E2E-09-02 · Link two pages                                    P0 · @pr · M2
Given        "Ilsa Varenne" open
When         I click "+ Link", choose "Mentor de" then "Capitaine Orsen"
Then         the Links panel of Ilsa shows "Mentor de · Capitaine Orsen"
And          the page Capitaine Orsen shows "Élève de · Ilsa Varenne" among its incoming links

E2E-09-03 · Incoming links                                    P1 · @pr · M2
When         I open "Hautbois"
Then         the incoming links include "Lieu de · Ilsa Varenne"
And          the implicit references (pages that mention Hautbois in their text) are listed separately

E2E-09-04 · Delete a link                                     P1 · @nightly · M2
When         I delete the link "Allié de" from Ilsa to Orsen
Then         it disappears from both pages

E2E-09-05 · Delete a link type in use                         P1 · @nightly · M2
When         I delete the type "Situé à"
Then         the confirmation says "1 link uses this type and will be deleted"
```

---

## F10 — Search and filters

**Rules**
- **F10.R1** `Ctrl+K` opens a quick search by name over every kind; `Enter` opens the first result.
- **F10.R2** Full-text search ignores case and accents ("col gele" finds "Col gelé"), and highlights the passages found.
- **F10.R3** Filters combine (AND): kind, categories (with optional sub-categories), conditions on properties (inherited values included), modification date, "breaking the rules".
- **F10.R4** Sorting by relevance, last modification or name; the state of the search is reflected in the URL (shareable).

```gherkin
E2E-10-01 · Quick search                                      P0 · @pr @desktop · M3
When         I press Ctrl+K and type "ilsa"
Then         "Ilsa Varenne" is the first result
When         I press Enter
Then         the page opens and the search closes

E2E-10-02 · Accent-insensitive full text                      P0 · @pr · M3
When         I search for "col gele"
Then         the results include the quest "Le Col gelé", the place "Col gelé" and Ilsa Varenne
And          the passages containing "Col gelé" are highlighted

E2E-10-03 · Filter by kind and category                       P0 · @pr · M3
When         I filter Kind = Pages, Category = PNJ with sub-categories
Then         only Ilsa Varenne and Capitaine Orsen are listed

E2E-10-04 · Condition on an inherited property                P0 · @pr · M3
When         I add the condition "Hostile = Non"
Then         Ilsa and Orsen are listed (value inherited from PNJ)

E2E-10-05 · Numeric condition                                 P1 · @pr · M3
When         I add the condition "Niveau > 50"
Then         Ilsa (72) and Dragon des Cimes (99) are listed, not Orsen (40)

E2E-10-06 · "Breaking the rules" filter                       P1 · @nightly · M3
When         I turn on the "Breaking the rules" filter
Then         only the pages in violation are listed, with the rule concerned

E2E-10-07 · Date filter                                       P1 · @nightly · M3
Given        the clock set and known modification dates
When         I choose "Modified: 24 h"
Then         only the pages modified since 12 October 2026 10:00 are listed

E2E-10-08 · Sorting and shareable URL                         P2 · @nightly · M3
When         I sort by "Name" then copy the URL and open it in a new tab
Then         the same search, with the same filters and the same sorting, is shown

E2E-10-09 · No result                                         P2 · @nightly · M3
When         I search for "zzzz"
Then         a "No result" message offers to reset the filters

E2E-10-10 · Reset the filters                                 P2 · @nightly · M3
When         I click "Reset"
Then         every filter is removed, the search text is kept

E2E-10-11 · Element created immediately findable              P0 · @pr · M3
When         I create the page "Lanterne d'ambre" then search for "ambre"
Then         it appears in the results without any noticeable indexing delay
```

---

## F11 — Recently viewed / edited

**Rules**
- **F11.R1** "Recently viewed" is **specific to each user**; a view counts after 2 seconds of display.
- **F11.R2** "Recently edited" is shared by everyone, with the author and the date.

```gherkin
E2E-11-01 · Recently viewed                                   P1 · @pr · M3
When         I view PNJ, then Hautbois, then Niveau
Then         the "Recently viewed" list shows Niveau, Hautbois, PNJ in this order, with their kind

E2E-11-02 · Recently edited                                   P1 · @pr · M3
When         I edit "Hautbois"
Then         Hautbois appears at the top of "Edited" with "me · just now"

E2E-11-03 · Views specific to each user                       P1 · @multi @server · M4
Given        camille viewed Hautbois
Then         Hautbois does not appear in the viewed list of malo
But          a change by camille appears in the edited list of malo
```

---

## F12 — Deletion and trash

**Rules**
- **F12.R1** Deleting an element puts it in the trash; its sub-pages and the content of its folders follow.
- **F12.R2** Before deletion, if the element is referenced, the list of the elements that reference it is shown.
- **F12.R3** Restoring puts the element back at its original location (or at the root if the parent no longer exists) with the same ID; the references become valid again.
- **F12.R4** Deleting a category removes the category from the pages, without deleting the pages. Deleting a property removes its values, restorable with it.
- **F12.R5** Emptying the trash is permanent and asks for an explicit confirmation.

```gherkin
E2E-12-01 · Delete a referenced page                          P0 · @pr · M2
When         I delete "Capitaine Orsen"
Then         the confirmation says "1 page references this element: Ilsa Varenne"
When         I confirm
Then         Orsen disappears from the tree and appears in the trash

E2E-12-02 · Restore from the trash                            P0 · @pr · M2
Given        "Capitaine Orsen" in the trash
When         I restore it
Then         it is back under PNJ with the same ID and the link from Ilsa works

E2E-12-03 · Cascading deletion of sub-pages                   P1 · @pr · M2
When         I delete "Ilsa Varenne"
Then         the confirmation mentions "and 2 sub-pages"
And          restoring Ilsa also restores Dialogues and Inventaire

E2E-12-04 · Delete a category                                 P1 · @nightly · M2
When         I delete the category "Marchand"
Then         Ilsa Varenne still exists, without the Marchand pill

E2E-12-05 · Restore when the parent has disappeared           P2 · @nightly · M2
Given        "Hautbois" deleted, then the folder "Lieux" deleted and permanently erased from the trash
When         I restore Hautbois
Then         it is put back at the Pages root, with a message saying so

E2E-12-06 · Empty the trash                                   P1 · @nightly · M2
When         I empty the trash and confirm by typing the name of the project
Then         the trash is empty and the elements can no longer be restored
```

---

## F13 — Revision history

**Rules**
- **F13.R1** Every save creates a revision (author, date, content, categories, properties).
- **F13.R2** Two revisions can be compared (diff of the text and of the properties).
- **F13.R3** Restoring a revision creates a **new** revision; the history is never rewritten.

```gherkin
E2E-13-01 · View the history                                  P1 · @pr · M3
Given        "Hautbois" edited three times
When         I open its history
Then         four revisions are listed with author and date, the most recent first

E2E-13-02 · Compare two revisions                             P1 · @pr · M3
When         I compare revision 1 and revision 4
Then         the text additions and deletions are highlighted, as well as the changed properties

E2E-13-03 · Restore a revision                                P0 · @pr · M3
When         I restore revision 2
Then         the content of the page is the one of revision 2
And          the history counts five revisions, the fifth marked "Restore of revision 2"
```

---

## F14 — Multi-user (server mode)

**Rules**
- **F14.R1** Rights are **per project only** (decision Q5): reader, editor, administrator.
- **F14.R2** A user has no access, not even read access, to a project they are not a member of.
- **F14.R3** Presence (who is viewing or editing which page) is visible in real time.
- **F14.R4** A change saved by a user is visible to the others without reloading.
- **F14.R5** Two concurrent saves are merged automatically if they touch different parts; otherwise a resolution view opens. **No text is lost** (§A-8.3). Live co-editing is out of v1 (decision Q3).

```gherkin
E2E-14-01 · Sign in and sign out                              P0 · @server · M4
When         I sign in with the credentials of camille
Then         her avatar is shown in the top bar
When         I sign out
Then         the sign-in screen is shown and the API answers 401 with the old cookie

E2E-14-02 · Invalid credentials                               P1 · @server · M4
When         I enter a wrong password
Then         the message "Incorrect login or password" is shown, without saying which one

E2E-14-03 · Reader is read-only                               P0 · @server · M4
Given        lea signed in
Then         no "Edit", "+ Add", "+ Link" button nor tree action is offered
And          a forged PUT request to a page receives 403

E2E-14-04 · Isolation between projects                        P0 · @server · M4
Given        externe signed in
Then         "Chroniques d'Aube" does not appear in their list of projects
And          the direct URL of Ilsa Varenne shows "Access denied", without leaking the name of the page

E2E-14-05 · Presence                                          P1 · @multi @server · M4
Given        camille and malo signed in
When         camille opens "Ilsa Varenne" for editing on the Histoire section
Then         malo sees the CR avatar on the page and the banner "Camille R. is also editing this page — Histoire section"
When         camille closes the tab
Then         the banner disappears for malo in less than 30 seconds

E2E-14-06 · Propagation without reloading                     P0 · @multi @server · M4
Given        malo is viewing "Hautbois"
When         camille edits and saves Hautbois
Then         malo sees the new content without reloading
And          a new element created by camille appears in the tree of malo

E2E-14-07 · Concurrent saves merged                           P0 · @multi @server · M4
Given        camille and malo edit "Ilsa Varenne" from the same revision
When         camille changes the Présentation section and saves
And          malo changes the Histoire section and saves afterwards
Then         the save of malo succeeds with the notification "Merged with the changes of Camille R."
And          the page contains both changes

E2E-14-08 · Real conflict                                     P0 · @multi @server · M4
Given        camille and malo change the same sentence differently
When         camille saves, then malo saves
Then         malo sees the resolution view with both versions of the sentence
When         malo chooses his version and saves
Then         the page contains the version of malo, and the version of camille can be viewed in the history

E2E-14-09 · Session expired while editing                     P0 · @server · M4
Given        camille editing with unsaved changes
When         her session expires and she presses Ctrl+S
Then         a sign-in dialog is shown
When         she signs in again
Then         saving succeeds with her changes intact

E2E-14-10 · Member management                                 P1 · @server · M4
Given        admin signed in
When         they change lea from the Reader role to Editor
Then         lea sees the "Edit" button after her session is refreshed
And          an editor has no access to the member management screen
```

---

## F15 — Preferences: language, theme, shortcuts

**Rules**
- **F15.R1** The interface language (Français, English, Deutsch, Español) is independent of the content language (decision Q1).
- **F15.R2** Light, Dark or System theme; System mode follows the setting of the OS live.
- **F15.R3** Preferences are remembered per user (local profile or server account) and applied before the first display (no theme flash).
- **F15.R4** Shortcuts are configurable; a combination already taken is reported.

```gherkin
E2E-15-01 · Change the interface language                     P0 · @pr · M1
When         I choose "English" in the preferences
Then         the interface is in English ("Explorer", "Edit", "Properties")
And          the content of the pages and the names of the elements are unchanged
And          dates are formatted the English way

E2E-15-02 · Dark theme                                        P0 · @pr · M1
When         I choose "Dark"
Then         the data-theme attribute is dark and the page background is dark
And          after reloading, the first render is directly dark

E2E-15-03 · System theme                                      P1 · @nightly · M1
Given        the "System" theme and the OS in light mode
When         the OS switches to dark (prefers-color-scheme emulation)
Then         the application switches to dark without reloading

E2E-15-04 · Shortcut conflict                                 P2 · @nightly · M5
When         I assign Ctrl+K to the insert palette
Then         a warning says "Already used by: Search" and offers to replace it
```

---

## F16 — Desktop application

These scenarios run **on the packaged binary** (§T-10): through CDP on Windows, through `--self-test` on macOS and Linux.

**Rules**
- **F16.R1** The embedded backend listens only on the loopback, on a random port, and requires the session token (§A-9).
- **F16.R2** The same project cannot be opened by two instances: the second brings the first to the foreground.
- **F16.R3** On close, no change is lost and no process remains.
- **F16.R4** In connected mode, an incompatible server version is detected before any operation (§A-2).

```gherkin
E2E-16-01 · Startup and smoke journey                         P0 · @desktop · M0
When         I start the installed application
Then         the home screen is shown in less than 3 seconds (10 s on the shared CI runners, testing.md §10)
And          the smoke scenario (create a project, a page, a reference, an image, search) succeeds

E2E-16-02 · Protected local server                            P0 · @desktop · M0
Then         the listening port is bound to 127.0.0.1 only
And          an HTTP request without a token, or with a different Host header, is refused

E2E-16-03 · Open a folder with the native picker              P1 · @desktop · M1
When         I choose "Open a folder…" (Ctrl+O) and select the folder of the project
Then         the project opens

E2E-16-04 · Second instance                                   P1 · @desktop · M1
Given        "Chroniques d'Aube" open
When         I start a second instance on the same project
Then         the first window comes to the foreground and the second one closes

E2E-16-05 · Clean shutdown                                    P0 · @desktop · M1
Given        a page being edited with unsaved changes
When         I close the window
Then         the confirmation "Save / Discard / Cancel" is shown
When         I choose "Save"
Then         the window closes, no Erable process remains and the change is there when reopening

E2E-16-06 · Incompatible server                               P1 · @desktop @server · M4
Given        a server exposing an unsupported API version
When         I connect to it
Then         a message states the versions involved, without any other request to the server

E2E-16-07 · Drag a folder onto the home screen                P2 · @desktop · M1
When         I drag the folder of a project onto the home screen
Then         the project opens
```

---

## F17 — Accessibility and keyboard

These scenarios complement the axe-core check of each screen (§T-9.3).

```gherkin
E2E-17-01 · Keyboard navigation in the tree                   P1 · @a11y · M1
When         I put the focus in the tree and use ↑ ↓ → ← Enter F2
Then         I browse, expand, collapse, open and rename without a mouse
And          the screen reader announces the nodes (role treeitem, aria-expanded, aria-level)

E2E-17-02 · Write a page without a mouse                      P1 · @a11y · M3
When         I open a page with Ctrl+K, switch to editing, insert a link with the palette and save with Ctrl+S
Then         the whole journey is done with the keyboard, with an always visible focus

E2E-17-03 · Modal dialogs                                     P1 · @a11y · M1
When         a confirmation dialog opens
Then         the focus is trapped in it, Esc closes it and the focus returns to the original element

E2E-17-04 · No axe-core violation                             P0 · @a11y @nightly · M5
Then         each main screen, in light and dark, has no "serious" or "critical" violation
```

---

## F18 — Mobile version (web)

The same SPA, adapted to phones: no second interface. The **narrow layout** applies below 720 px wide; the touch rules apply as soon as the primary pointer is coarse (`pointer: coarse`), whatever the width. The mobile layout reuses the components, tokens and screens of the desktop, stacked (§A-3.1).

**Rules**
- **F18.R1** In the narrow layout, the explorer becomes a **drawer** over the content: closed when a screen opens, closed again after each navigation, with Esc, by touching the backdrop or with its button; touching a folder or a root in it expands it instead of opening it (a page opens); the focus is trapped in it while it is open. The "open" state and the width of the desktop panel are neither read nor changed.
- **F18.R2** The top bar fits on one line: home, explorer, project name, search (icon), "More" menu which groups back / forward, language, theme, preferences and account. The status bar is reduced to the connection state and the mode.
- **F18.R3** **No horizontal scrolling** of the screen at 360 px wide, on every screen: only wide blocks (tables, code, revision comparison, grids) scroll in their own frame. Text breaks between words, never in the middle (except IDs and URLs that are too long).
- **F18.R4** With a coarse pointer, every interactive target offers a touch area of at least **44 × 44 px**, and no action is reachable only by hovering or right-clicking: the elements of the tree and the list rows have a "…" button that opens the same menu as the right click (a long press opens it too).
- **F18.R5** An element of the tree is **moved without drag and drop**: the "Move to…" action of its menu offers the valid destinations (same root, not under itself) and produces the same move as drag and drop (F02).
- **F18.R6** Editing stays complete: the toolbar scrolls horizontally, "Cancel" and "Save" stay visible when the virtual keyboard is open, and the page panel (properties, categories, links) goes into a tab above the editor instead of the side column. The insert palette fits in the screen above the keyboard.
- **F18.R7** In the narrow layout, the quick search and the dialogs take the whole screen (short confirmations stay centered cards); the filters of the Search screen open in a full-screen panel, closed by "Show the N results", to combine several of them.
- **F18.R8** The SPA served by a server is **installable** as a web application: manifest (name, icons, theme color, `display: standalone`, `start_url` at the root). No offline mode nor service worker, and still no durable state in the browser (decision Q7).

```gherkin
E2E-18-01 · Explorer in a drawer                              P0 · @mobile @pr · M6
Given        the page "Ilsa Varenne" open on a phone
Then         the explorer is hidden and the page takes the whole width
When         I open the explorer and touch "Hautbois"
Then         the page "Hautbois" is shown and the drawer is closed
And          on a wide screen, the explorer is still open at its previous width

E2E-18-02 · No horizontal scrolling                           P1 · @mobile · M6
When         I go through the home screen, a page in reading and editing, a category, a property, the search, the trash, the history and the members
Then         the width of the document never exceeds the one of the screen

E2E-18-03 · Read a page on a phone                            P1 · @mobile @pr · M6
Given        the page "Ilsa Varenne" open on a phone
Then         the title, the categories and the start of the text are visible without scrolling, the title broken between words
When         I expand "Page panel"
Then         I see "Hostile" inherited "via PNJ" and the link "Allié de" to "Capitaine Orsen"
When         I touch the link "Hautbois" in the text
Then         the page "Hautbois" is shown

E2E-18-04 · Edit and save a page                              P0 · @mobile @pr · M6
Given        the page "Ilsa Varenne" open on a phone
When         I touch "Edit", type "[[orse" then touch "Capitaine Orsen" in the palette
And          I touch "Save"
Then         the page is in reading mode and contains a link to "Capitaine Orsen"

E2E-18-05 · Properties when editing on a phone                P1 · @mobile · M6
Given        the page "Ilsa Varenne" being edited on a phone
When         I open the tab of the page panel and enter "50" in "Niveau" (72, breaks a rule)
Then         the warning "Must be less than or equal to 60" disappears
When         I touch "Save"
Then         the page in reading mode shows "Niveau" 50

E2E-18-06 · Search on a phone                                 P1 · @mobile @pr · M6
When         I touch the search and type "ilsa"
Then         the quick search takes the screen and suggests "Ilsa Varenne"
When         I open the Search screen and the filter panel, choose the category "PNJ" then touch "Show the results"
Then         the panel closes and the results are limited to the PNJ

E2E-18-07 · Menu of an element without a right click          P1 · @mobile · M6
When         I touch the "…" button of "Hautbois" in the explorer then "Rename"
And          I enter "Hautbois-sur-Givre" and confirm
Then         the element is renamed in the tree and in the references that cite it

E2E-18-08 · Move an element without drag and drop             P1 · @mobile @pr · M6
When         I choose "Move to…" in the menu of "Capitaine Orsen" then the folder "Lieux"
Then         "Capitaine Orsen" is placed in "Lieux" and its references are intact
And          the menu of "Personnages" offers neither itself, nor "PNJ" (its descendant), nor a destination outside Pages

E2E-18-09 · Preferences and account from the "More" menu      P2 · @mobile · M6
When         I open the "More" menu of the top bar
Then         I can change the language and the theme, go back and sign out

E2E-18-10 · Touch targets                                     P1 · @mobile · M6
When         I show the home screen, a page, editing and the explorer on a phone
Then         every visible button, navigation link and field offers a touch area of at least 44 × 44 px

E2E-18-11 · Installable web application                       P2 · @mobile · M6
Then         the SPA declares a valid manifest (name, reachable 192 and 512 px icons, `display: standalone`) and a theme color
```

---

## F19 — Android application

A client of Erable servers (decision Q11): the application contains neither a backend nor a project. It keeps a list of servers and opens the SPA of the chosen server in a WebView; authentication is the one of the web (cookie of the server).

**Rules**
- **F19.R1** The user adds a server by its address. Only the origin is kept (`https` scheme by default, lower-case host; path, query and fragment removed — the same rule as the connected desktop, `Erable.Core.Servers.ServerAddress`) and checked with `GET /api/info` **before** being saved: an Erable answer in `mode: "server"` is required; otherwise a message gives the cause (unreachable, not an Erable server, desktop application). The API version does not have to match: the SPA shown is the one of the server itself; the version of the server is shown in the list.
- **F19.R2** The WebView only navigates within the origin of the open server: any other link opens in the system browser; `file:`, `content:` and `javascript:` are refused.
- **F19.R3** Plain HTTP is accepted (server on the local network) but flagged in the list of servers; HTTPS is offered by default.
- **F19.R4** The Android back button walks back the history of the WebView, then returns to the list of servers.
- **F19.R5** Attachments are uploaded with the Android file picker and downloaded with the download manager, with the session of the user.
- **F19.R6** The session survives a restart of the application; "Forget this server" removes it from the list and clears its cookies.
- **F19.R7** The native interface (list, adding, errors) follows the system language among `fr`, `en`, `de`, `es` (English otherwise); the SPA keeps the language of the preferences of the user.

**Verification.** Normalization, version compatibility and navigation policy are tested with xUnit (`tests/Erable.Android.Tests`). The on-device journey (adding, signing in, reading, editing, uploading and downloading an attachment, back, forgetting) is a manual checklist in `docs/release.md`, for lack of an emulator in CI: there is no automated `E2E-19-*` scenario.

---

## 4. Summary

### 4.1 Volume per milestone

| Milestone | Added | Cumulative | of which P0 (cumulative) |
|---|---|---|---|
| M0 | E2E-16-01, 16-02 | 2 | 2 |
| M1 | F01–F06, F15, F16 and F17, except the scenarios attached to a later milestone (including E2E-17-02) | 50 | 22 |
| M2 | F07, F08, F09, F12, plus E2E-01-05, 04-05, 05-12, 06-06 | 85 | 38 |
| M3 | F10, F11, F13 | 101 | 44 |
| M4 | F14, plus E2E-01-06, 11-03, 16-06 | 114 | 52 |
| M5 | E2E-05-07, 15-04, 17-04 | 117 | 53 |
| M6 | F18 (F19 without automated scenarios) | 128 | 55 |

The PR pipeline runs the 62 `@pr` scenarios (a little more than half); the rest runs at night (§T-13).

### 4.2 What the e2e tests do not cover

Combinatorics are checked at faster levels, not in e2e (§T-4 to §T-8): every rule per type, every case of the Markdown syntax, every combination of roles per endpoint, merging on generated inputs. An e2e scenario checks that **the whole chain is wired**, not each case.

### 4.3 Evolution of the document

- A new feature adds its `F<nn>.R<n>` rules and its scenarios **before** the implementation; they serve as acceptance criteria.
- A bug found in real use adds a scenario if it concerns a journey; otherwise, a lower-level test is enough.
- A CI script checks that every scenario ID of the document has a Playwright test with the same ID, and conversely.
