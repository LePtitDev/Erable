# Erable — User guide

Erable is a structured, "IDE-style" wiki: Markdown pages, arranged in a tree, linked to each other by their identifier, described by categories and properties. This guide fits in four points; the rest of the application is discovered by using it.

## 1. Getting started

1. **Home.** On the first launch, choose *New project*: a name, the **content language** (pages will be written in this language; it cannot be changed afterwards) and a location. *Open a folder…* reopens an existing project (`Ctrl+O`), *Recent projects* lists the latest ones.
2. **The tree** (on the left) has four fixed roots: **Pages**, **Categories**, **Properties**, **Links**. The context menu (right click) creates a folder or a page; `F2` renames the selected element; elements are moved by drag and drop, or with the keyboard with `Ctrl+X` then `Ctrl+V`.
3. **A page** opens in reading mode; *Edit* switches to editing. `Ctrl+S` saves. A draft is kept automatically: if the application closes before saving, it is offered back when the page is reopened.
4. **Finding things**: `Ctrl+K` opens the quick search (a name, `Enter`); the *Search* page adds filters on categories and properties.
5. Nothing is lost by mistake: deleted elements go to the **trash**, and every save creates a **revision** that can be compared and restored (*History*).
6. **Backing up** a local project means copying its folder while the application is closed. To work together, an administrator installs a server (`docs/deployment.md`).

On Linux, the application needs WebKitGTK 4.1 and libnotify (Debian / Ubuntu: `sudo apt install libwebkit2gtk-4.1-0 libnotify4`).

Preferences (interface language, light or dark theme, shortcuts) are set from the home screen (*Preferences*) as well as from the top bar of a project.

**On a phone.** An Erable server can also be used from a phone's browser, which can *add it to the home screen* like an application. The tree opens with the button on the left of the top bar (tapping a folder expands it); the "…" menu of the bar groups back / forward, preferences, theme and sign-out. Each element of the tree has its own "…" button (or a long press) for its menu, and *Move to…* replaces drag and drop. When editing, *Text* and *Page panel* (properties, categories, links, files) alternate above the editor. On Android, the **Erable application** (APK of the releases) does the same while keeping the list of your servers: *Add a server*, its address, then the usual sign-in; a long press on a server forgets it. It does not open local projects: a server is required.

## 2. Linking pages: the `[[ID]]` syntax

Every element — page, category, property, attachment — has an **identifier** of 16 hexadecimal characters that never changes. You refer to it by writing it between double square brackets:

| You write | Result |
|---|---|
| `[[3F2A9C14E07B5D21]]` | a link to the element, shown under **its current name** |
| `[[3F2A9C14E07B5D21\|the merchant]]` | the same link with a label of your choice |
| `[[3F2A9C14E07B5D21#history]]` | a link to the "History" heading of the page |
| `![[3F2A9C14E07B5D21]]` | the element **embedded** (an image, a file) |
| `![[3F2A9C14E07B5D21]]{width=320 align=right}` | an embed 320 px wide, aligned to the right |

You almost never type an identifier by hand: type `[[` (or `![[`) in the editor and the **insert palette** opens. Type a few letters of the name; `Tab` changes the kind (All, Pages, Attachments, Categories, Properties), `Enter` inserts a link, `Shift+Enter` an embed, `Esc` closes the palette. In the editor, the identifier is shown as a pill with the name; *Show raw IDs* shows the actual text.

Since links go through the identifier, **renaming or moving an element never breaks anything**: every link shows the new name. The details of the syntax (tables, code, edge cases) are in `docs/markdown-syntax.md`. Raw HTML is disabled: a tag written in a page is shown as text.

Dropping a file into the editor, or pasting it, adds it as an attachment of the page and inserts the embed.

## 3. Keyboard shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl+K` (`⌘K`) | Quick search * |
| `Ctrl+Space` (`⌘⇧K`) | Open the insert palette, in the editor * |
| `Ctrl+S` (`⌘S`) | Save the page * |
| `Ctrl+O` (`⌘O`) | Open a project folder (home) * |
| `[[` or `![[` | Open the insert palette while typing |
| `F2` | Rename the selected element of the tree |
| `Ctrl+X`, `Ctrl+V` | Cut, paste an element of the tree |
| `Delete` | Move the element to the trash |
| `Esc` | Close a dialog or the palette |

The shortcuts marked with an asterisk are **configurable**: *Preferences → Keyboard shortcuts*. Click the combination to change, then press the new one; if it is already taken, Erable says so ("Already used by: Search") and offers to replace it. *Help and shortcuts*, from the home screen, shows the current list.

## 4. Folders and sub-pages

Erable offers two ways to build a hierarchy, with two different roles:

- A **folder** is for **arranging**. It has no content: it can be moved or renamed without changing the meaning of anything. Example: a "Characters" folder that groups fifty pages.
- A **sub-page** carries **meaning**. It is a page placed under another page: it appears in the *Sub-pages* section of its parent and is part of it. Example: "Dialogues" and "Inventory" under the page of a character.

To decide: *"if I delete or move the parent, should the page follow?"* If so, it is a sub-page; otherwise, a folder is enough. Categories are not arranged in the tree of pages: a category describes what pages are (a character, a place…) and brings them **properties**; a page can have several.
