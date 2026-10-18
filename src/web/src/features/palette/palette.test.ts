import { EditorSelection, EditorState, type TransactionSpec } from "@codemirror/state";
import { describe, expect, it } from "vitest";
import type { TreeNodeItem } from "../../shared/api/tree";
import { requireElementId } from "../../shared/ids/elementId";
import { roots } from "../../shared/ids/roots";
import {
  closePalette,
  insertPalette,
  insertReference,
  openPalette,
  paletteField,
  paletteQuery,
} from "./palette";
import { buildPaletteIndex, searchPalette } from "./paletteSearch";

const orsen = requireElementId("7A1C4E90B3F2D615");
const keys = { move: () => undefined, pick: () => false, tab: () => undefined };

function create(doc = "", cursor = doc.length) {
  return EditorState.create({
    doc,
    selection: EditorSelection.cursor(cursor),
    extensions: [insertPalette(keys)],
  });
}

/** Types text at the cursor, like a user (the palette opens on user input only). */
function type(state: EditorState, text: string) {
  let s = state;
  for (const c of text) {
    const head = s.selection.main.head;
    s = s.update({
      changes: { from: head, insert: c },
      selection: { anchor: head + 1 },
      userEvent: "input.type",
    }).state;
  }
  return s;
}

function apply(state: EditorState, spec: TransactionSpec) {
  return state.update(spec).state;
}

describe("palette state (F05.R3)", () => {
  it("opens when [[ is typed and reads the query typed after it (E2E-05-03)", () => {
    const s = type(create("Voir "), "[[orse");

    expect(s.field(paletteField)).toMatchObject({
      open: true,
      trigger: "typed",
      from: 5,
      embed: false,
    });
    expect(paletteQuery(s)).toBe("orse");
  });

  it("opens as an embed when ![[ is typed (E2E-05-04)", () => {
    const s = type(create(), "![[carte");

    expect(s.field(paletteField)).toMatchObject({ open: true, embed: true, from: 0 });
    expect(paletteQuery(s)).toBe("carte");
  });

  it("does not open after an escaped bracket, nor when the text is pasted", () => {
    expect(type(create("\\"), "[[").field(paletteField).open).toBe(false);
    const pasted = create().update({
      changes: { from: 0, insert: "[[" },
      selection: { anchor: 2 },
      userEvent: "input.paste",
    }).state;
    expect(pasted.field(paletteField).open).toBe(false);
  });

  it("closes when the cursor goes back before the marker, or on ] or a new line", () => {
    const open = type(create(), "[[ab");

    expect(apply(open, { selection: { anchor: 1 } }).field(paletteField).open).toBe(false);
    expect(type(open, "]").field(paletteField).open).toBe(false);
    expect(type(open, "\n").field(paletteField).open).toBe(false);
  });

  it("closes on Escape-like effect and leaves only the typed text (E2E-05-06)", () => {
    const open = type(create(), "[[");
    const s = apply(open, { effects: closePalette.of(null) });

    expect(s.field(paletteField).open).toBe(false);
    expect(s.doc.toString()).toBe("[[");
  });

  it("replaces [[query by the reference, cursor after it, palette closed", () => {
    const open = type(create("Voir "), "[[orse");
    const s = apply(open, insertReference(open, orsen, false));

    expect(s.doc.toString()).toBe(`Voir [[${orsen}]]`);
    expect(s.selection.main.head).toBe(s.doc.length);
    expect(s.field(paletteField).open).toBe(false);
  });

  it("inserts an embed with Shift+Enter even when the palette was opened by [[", () => {
    const open = type(create(), "[[carte");

    expect(apply(open, insertReference(open, orsen, true)).doc.toString()).toBe(`![[${orsen}]]`);
  });

  it("keeps the embed when ![[ was typed and the choice is validated with Enter", () => {
    const open = type(create(), "![[carte");

    expect(apply(open, insertReference(open, orsen, false)).doc.toString()).toBe(`![[${orsen}]]`);
  });

  it("opened by the shortcut, inserts at the cursor without removing any text", () => {
    const start = create("Avant après", 6);
    const open = apply(start, {
      effects: openPalette.of({ trigger: "shortcut", from: 6, embed: false }),
    });

    expect(paletteQuery(open)).toBe("");
    expect(apply(open, insertReference(open, orsen, false)).doc.toString()).toBe(
      `Avant [[${orsen}]]après`,
    );
  });
});

describe("palette search and ranking", () => {
  const n = (
    id: string,
    kind: TreeNodeItem["kind"],
    parentId: string | null,
    name: string,
  ): TreeNodeItem => ({
    id: requireElementId(id),
    kind,
    parentId: parentId === null ? null : requireElementId(parentId),
    name,
    sortOrder: 1,
    duplicateName: false,
  });
  const nodes = [
    n(roots.pages, "root", null, "Pages"),
    n(roots.categories, "root", null, "Catégories"),
    n("1000000000000001", "folder", roots.pages, "Personnages"),
    n(orsen, "page", "1000000000000001", "Capitaine Orsen"),
    n("1000000000000002", "page", "1000000000000001", "Orsenal du Nord"),
    n("1000000000000003", "page", roots.pages, "Corsaire"),
    n("1000000000000004", "page", roots.pages, "Hautbois"),
    n("1000000000000005", "category", roots.categories, "Marchand"),
  ];
  const attachments = [
    {
      id: requireElementId("2000000000000001"),
      pageId: requireElementId("1000000000000004"),
      fileName: "carte_hautbois.webp",
      mime: "image/webp",
      size: 1,
      display: "image" as const,
    },
  ];
  const index = buildPaletteIndex(nodes, attachments);

  it("ranks a name prefix, then a word prefix, then a substring, then letters in order", () => {
    expect(searchPalette(index, "ors", "all").map((i) => i.name)).toEqual([
      "Orsenal du Nord",
      "Capitaine Orsen",
      "Corsaire",
    ]);
    expect(searchPalette(index, "cpo", "all").map((i) => i.name)).toEqual(["Capitaine Orsen"]);
  });

  it("ignores case and accents", () => {
    expect(searchPalette(index, "CATEG", "all")).toEqual([]);
    expect(searchPalette(index, "hautbóis", "page").map((i) => i.name)).toEqual(["Hautbois"]);
  });

  it("shows attachments as « Page › fichier », with the page's path (E2E-05-04)", () => {
    const [carte] = searchPalette(index, "carte", "all");

    expect(carte).toMatchObject({
      kind: "attachment",
      label: "Hautbois › carte_hautbois.webp",
      path: "Pages / Hautbois",
    });
  });

  it("filters by tab and never proposes roots nor folders (E2E-05-05)", () => {
    expect(searchPalette(index, "", "category").map((i) => i.name)).toEqual(["Marchand"]);
    expect(searchPalette(index, "", "all").map((i) => i.kind)).not.toContain("folder");
    expect(searchPalette(index, "pers", "all")).toEqual([]);
  });

  it("finds the best match among 10 000 elements", () => {
    const big: TreeNodeItem[] = [n(roots.pages, "root", null, "Pages")];
    for (let i = 0; i < 10_000; i++) {
      big.push(
        n(
          (0x3000000000000000 + i).toString(16).toUpperCase(),
          "page",
          roots.pages,
          `Page ${String(i)} du monde`,
        ),
      );
    }
    const bigIndex = buildPaletteIndex(big, []);

    // The 50 ms budget is measured in a real browser by the @perf tests (perf-large.spec.ts), never here (§T-1.6).
    const found = searchPalette(bigIndex, "page 99", "all");

    expect(found[0]?.name).toBe("Page 99 du monde");
  });
});
