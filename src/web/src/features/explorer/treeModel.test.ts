import fc from "fast-check";
import { describe, expect, it } from "vitest";
import type { ElementKind, TreeNodeItem } from "../../shared/api/tree";
import { requireElementId, type ElementId } from "../../shared/ids/elementId";
import { roots } from "../../shared/ids/roots";
import {
  applyMove,
  checkMove,
  dropDestination,
  indexTree,
  rootOf,
  ancestorsOf,
  visibleRows,
} from "./treeModel";

let next = 0x100000;
function id(): ElementId {
  next += 1;
  return requireElementId(next.toString(16).toUpperCase().padStart(16, "0"));
}

function node(
  nodeId: ElementId,
  kind: ElementKind,
  parentId: ElementId | null,
  name: string,
  sortOrder = 1,
): TreeNodeItem {
  return { id: nodeId, kind, parentId, name, sortOrder, duplicateName: false };
}

const personnages = id();
const pnj = id();
const ilsa = id();
const dialogues = id();
const lieux = id();
const hautbois = id();
const colGele = id();
const categoryPnj = id();

const chroniques: TreeNodeItem[] = [
  node(roots.pages, "root", null, "Pages", 1),
  node(roots.categories, "root", null, "Catégories", 2),
  node(roots.properties, "root", null, "Propriétés", 3),
  node(roots.links, "root", null, "Liens", 4),
  node(personnages, "folder", roots.pages, "Personnages", 1),
  node(pnj, "folder", personnages, "PNJ", 1),
  node(ilsa, "page", pnj, "Ilsa Varenne", 1),
  node(dialogues, "page", ilsa, "Dialogues", 1),
  node(lieux, "folder", roots.pages, "Lieux", 2),
  node(hautbois, "page", lieux, "Hautbois", 1),
  node(colGele, "page", lieux, "Col gelé", 2),
  node(categoryPnj, "category", roots.categories, "PNJ", 1),
];
const tree = indexTree(chroniques);

describe("checkMove (F02.R1–R3)", () => {
  it("allows a page into another folder of Pages (E2E-02-03)", () => {
    expect(checkMove(tree, hautbois, personnages)).toBeNull();
  });

  it("refuses a category under Pages (E2E-02-04)", () => {
    expect(checkMove(tree, categoryPnj, roots.pages)).toBe("crossRoot");
  });

  it("refuses a page under its own sub-page (E2E-02-05)", () => {
    expect(checkMove(tree, ilsa, dialogues)).toBe("intoDescendant");
  });

  it("allows a page under a page: it becomes a sub-page", () => {
    expect(checkMove(tree, hautbois, ilsa)).toBeNull();
  });

  it("refuses a folder under a page", () => {
    expect(checkMove(tree, lieux, ilsa)).toBe("invalidParent");
  });

  it("never moves a root", () => {
    expect(checkMove(tree, roots.pages, lieux)).toBe("root");
  });
});

describe("dropDestination", () => {
  it("places before a sibling at its index, the moved element excluded (E2E-02-06)", () => {
    expect(dropDestination(tree, colGele, hautbois, "before")).toEqual({
      parentId: lieux,
      index: 0,
    });
  });

  it("places after a sibling", () => {
    expect(dropDestination(tree, hautbois, colGele, "after")).toEqual({
      parentId: lieux,
      index: 1,
    });
  });

  it("places inside at the end", () => {
    expect(dropDestination(tree, hautbois, personnages, "inside")).toEqual({
      parentId: personnages,
      index: null,
    });
  });

  it("refuses to place beside a root", () => {
    expect(dropDestination(tree, lieux, roots.pages, "before")).toBeNull();
  });
});

describe("visibleRows", () => {
  it("shows roots and the children of expanded nodes, with ARIA levels and positions", () => {
    const rows = visibleRows(tree, new Set([roots.pages, lieux]));

    expect(rows.map((r) => `${String(r.level)}:${r.node.name}`)).toEqual([
      "1:Pages",
      "2:Personnages",
      "2:Lieux",
      "3:Hautbois",
      "3:Col gelé",
      "1:Catégories",
      "1:Propriétés",
      "1:Liens",
    ]);
    expect(rows[3]).toMatchObject({ position: 1, siblings: 2, expandable: false });
    expect(rows[2]).toMatchObject({ expanded: true, expandable: true });
  });

  it("filters ignoring case and accents, keeping and opening the ancestors", () => {
    const rows = visibleRows(tree, new Set(), "col gele");

    expect(rows.map((r) => r.node.name)).toEqual(["Pages", "Lieux", "Col gelé"]);
  });
});

describe("applyMove", () => {
  it("renames without moving", () => {
    const moved = indexTree(applyMove(chroniques, hautbois, { name: "Port-Hautbois" }));

    expect(moved.byId.get(hautbois)?.name).toBe("Port-Hautbois");
    expect(moved.byId.get(hautbois)?.parentId).toBe(lieux);
  });

  it("moves and renumbers the new siblings", () => {
    const moved = indexTree(applyMove(chroniques, colGele, { parentId: lieux, index: 0 }));

    expect(
      visibleRows(moved, new Set([roots.pages, lieux]))
        .slice(3, 5)
        .map((r) => r.node.name),
    ).toEqual(["Col gelé", "Hautbois"]);
  });
});

describe("tree invariants under random valid moves (docs/testing.md §4.1)", () => {
  it("keeps one parent per element, no cycle and every element under its root", () => {
    const movable = chroniques.filter((n) => n.kind !== "root").map((n) => n.id);
    const all = chroniques.map((n) => n.id);
    fc.assert(
      fc.property(
        fc.array(fc.tuple(fc.constantFrom(...movable), fc.constantFrom(...all), fc.nat(5)), {
          maxLength: 30,
        }),
        (moves) => {
          let nodes = chroniques;
          const rootBefore = new Map(movable.map((m) => [m, rootOf(tree, m)]));
          for (const [moving, parent, index] of moves) {
            const current = indexTree(nodes);
            if (checkMove(current, moving, parent) === null) {
              nodes = applyMove(nodes, moving, { parentId: parent, index });
            }
          }
          const after = indexTree(nodes);
          for (const m of movable) {
            expect(ancestorsOf(after, m).some((a) => a.id === m)).toBe(false);
            expect(rootOf(after, m)).toBe(rootBefore.get(m));
          }
          expect(nodes).toHaveLength(chroniques.length);
        },
      ),
    );
  });
});

describe("performance (indicative, formal budget in M3-06)", () => {
  it("indexes 10 000 elements and computes the visible lines in well under 50 ms", () => {
    const nodes: TreeNodeItem[] = [node(roots.pages, "root", null, "Pages")];
    const folders: ElementId[] = [];
    for (let f = 0; f < 100; f++) {
      const folder = id();
      folders.push(folder);
      nodes.push(node(folder, "folder", roots.pages, `Dossier ${String(f)}`, f));
      for (let p = 0; p < 100; p++)
        nodes.push(node(id(), "page", folder, `Page ${String(f)}-${String(p)}`, p));
    }
    const expanded = new Set<ElementId>([roots.pages, ...folders.slice(0, 50)]);

    const start = performance.now();
    const index = indexTree(nodes);
    const rows = visibleRows(index, expanded);
    const elapsed = performance.now() - start;

    expect(rows).toHaveLength(1 + 100 + 50 * 100);
    expect(elapsed).toBeLessThan(50);
  });
});
