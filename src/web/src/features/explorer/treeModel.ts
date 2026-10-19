import type { ElementKind, TreeNodeItem } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { roots } from "../../shared/ids/roots";

/** Kind of the elements filed under each root, besides folders (docs/architecture.md §5.1). */
const itemKindOfRoot = new Map<string, ElementKind>([
  [roots.pages, "page"],
  [roots.categories, "category"],
  [roots.properties, "propertyDefinition"],
  [roots.links, "linkType"],
]);

/** The tree as the explorer reads it: nodes by ID and ordered children. Rebuilt from each API answer. */
export interface TreeIndex {
  readonly byId: ReadonlyMap<ElementId, TreeNodeItem>;
  readonly children: ReadonlyMap<ElementId | null, readonly TreeNodeItem[]>;
}

export function indexTree(nodes: readonly TreeNodeItem[]): TreeIndex {
  const byId = new Map<ElementId, TreeNodeItem>();
  const children = new Map<ElementId | null, TreeNodeItem[]>();
  for (const node of nodes) {
    byId.set(node.id, node);
    const siblings = children.get(node.parentId) ?? [];
    siblings.push(node);
    children.set(node.parentId, siblings);
  }
  for (const siblings of children.values()) {
    siblings.sort((a, b) => a.sortOrder - b.sortOrder || (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  }
  return { byId, children };
}

export function childrenOf(tree: TreeIndex, id: ElementId | null): readonly TreeNodeItem[] {
  return tree.children.get(id) ?? [];
}

/** Parent first, up to the root; stops on a broken chain. */
export function ancestorsOf(tree: TreeIndex, id: ElementId): TreeNodeItem[] {
  const result: TreeNodeItem[] = [];
  const seen = new Set<ElementId>([id]);
  let parent = tree.byId.get(id)?.parentId ?? null;
  while (parent !== null && !seen.has(parent)) {
    const node = tree.byId.get(parent);
    if (node === undefined) break;
    result.push(node);
    seen.add(parent);
    parent = node.parentId;
  }
  return result;
}

export function rootOf(tree: TreeIndex, id: ElementId): ElementId | null {
  const node = tree.byId.get(id);
  if (node?.kind === "root") return node.id;
  const top = ancestorsOf(tree, id).at(-1);
  return top?.kind === "root" ? top.id : null;
}

/** Whether `parent` may contain an element of `kind` (mirror of ElementTree.CheckParent in Core). */
export function canContain(tree: TreeIndex, parentId: ElementId, kind: ElementKind): boolean {
  const parent = tree.byId.get(parentId);
  if (parent === undefined) return false;
  const root = rootOf(tree, parentId);
  const itemKind = root === null ? undefined : itemKindOfRoot.get(root);
  switch (parent.kind) {
    case "root":
    case "folder":
      return kind === "folder" || kind === itemKind;
    case "page":
      // A sub-page is a page under a page (docs/architecture.md §5.1).
      return kind === "page";
    case "category":
    case "propertyDefinition":
    case "linkType":
    case "attachment":
      return false;
  }
}

export type MoveRefusal = "root" | "crossRoot" | "intoDescendant" | "invalidParent";

/** Why a move is refused, or null when it is allowed (F02.R1–R3). */
export function checkMove(
  tree: TreeIndex,
  id: ElementId,
  newParentId: ElementId,
): MoveRefusal | null {
  const node = tree.byId.get(id);
  if (node === undefined || node.kind === "root" || node.kind === "attachment") return "root";
  if (rootOf(tree, newParentId) !== rootOf(tree, id)) return "crossRoot";
  if (newParentId === id || ancestorsOf(tree, newParentId).some((a) => a.id === id))
    return "intoDescendant";
  return canContain(tree, newParentId, node.kind) ? null : "invalidParent";
}

/** A place where « Déplacer vers… » can put an element, with the names of its ancestors for the list. */
export interface MoveDestination {
  readonly node: TreeNodeItem;
  /** From the root down to the parent of `node`. */
  readonly path: readonly string[];
}

/**
 * Every parent that `id` may move into (F18.R5), in tree order: the same checks as a drop (F02.R1–R3), its current
 * parent left out since nothing would change.
 */
export function moveDestinations(tree: TreeIndex, id: ElementId): MoveDestination[] {
  const node = tree.byId.get(id);
  const root = rootOf(tree, id);
  if (node === undefined || root === null) return [];
  const result: MoveDestination[] = [];
  const walk = (current: TreeNodeItem, path: readonly string[]) => {
    // Nothing under the element itself can receive it: its subtree is skipped whole.
    if (current.id === id) return;
    if (current.id !== node.parentId && checkMove(tree, id, current.id) === null) {
      result.push({ node: current, path });
    }
    for (const child of childrenOf(tree, current.id)) walk(child, [...path, current.name]);
  };
  const top = tree.byId.get(root);
  if (top !== undefined) walk(top, []);
  return result;
}

export type DropPosition = "before" | "inside" | "after";

/** Where a drop on `targetId` puts the element: new parent and index among its future siblings (itself excluded). */
export function dropDestination(
  tree: TreeIndex,
  id: ElementId,
  targetId: ElementId,
  position: DropPosition,
): { parentId: ElementId; index: number | null } | null {
  const target = tree.byId.get(targetId);
  if (target === undefined) return null;
  if (position === "inside") return { parentId: targetId, index: null };
  if (target.parentId === null) return null; // nothing goes beside a root
  const siblings = childrenOf(tree, target.parentId).filter((n) => n.id !== id);
  const at = siblings.findIndex((n) => n.id === targetId);
  return { parentId: target.parentId, index: position === "before" ? at : at + 1 };
}

/** One visible line of the explorer. */
export interface TreeRow {
  readonly node: TreeNodeItem;
  /** 1 for roots (aria-level). */
  readonly level: number;
  readonly expandable: boolean;
  readonly expanded: boolean;
  /** Position among its siblings and their number (aria-posinset / aria-setsize). */
  readonly position: number;
  readonly siblings: number;
}

/**
 * The visible lines, depth first. With a filter, the matching elements and their ancestors are shown, expanded
 * (case and accent insensitive).
 */
export function visibleRows(
  tree: TreeIndex,
  expanded: ReadonlySet<ElementId>,
  filter = "",
): TreeRow[] {
  const needle = normalize(filter.trim());
  const keep = needle === "" ? null : matchesWithAncestors(tree, needle);
  const rows: TreeRow[] = [];
  const walk = (parentId: ElementId | null, level: number) => {
    const siblings = childrenOf(tree, parentId).filter((n) => keep === null || keep.has(n.id));
    siblings.forEach((node, i) => {
      const expandable = childrenOf(tree, node.id).length > 0;
      const open = expandable && (keep !== null || expanded.has(node.id));
      rows.push({
        node,
        level,
        expandable,
        expanded: open,
        position: i + 1,
        siblings: siblings.length,
      });
      if (open) walk(node.id, level + 1);
    });
  };
  walk(null, 1);
  return rows;
}

function matchesWithAncestors(tree: TreeIndex, needle: string): Set<ElementId> {
  const keep = new Set<ElementId>();
  for (const node of tree.byId.values()) {
    if (node.kind !== "root" && normalize(node.name).includes(needle)) {
      keep.add(node.id);
      for (const ancestor of ancestorsOf(tree, node.id)) keep.add(ancestor.id);
    }
  }
  return keep;
}

/** Lower case, without diacritics: « col gele » finds « Col gelé ». */
export function normalize(text: string): string {
  return text.normalize("NFD").replace(/\p{M}/gu, "").toLocaleLowerCase();
}

/** Applies a rename or a move to a copy of the nodes (optimistic update before the API answers). */
export function applyMove(
  nodes: readonly TreeNodeItem[],
  id: ElementId,
  change: { name?: string; parentId?: ElementId; index?: number | null },
): TreeNodeItem[] {
  const moving = nodes.find((n) => n.id === id);
  if (moving === undefined) return [...nodes];
  const parentId = change.parentId ?? moving.parentId;
  const renamed = { ...moving, name: change.name ?? moving.name, parentId };
  if (change.parentId === undefined && change.index === undefined) {
    return nodes.map((n) => (n.id === id ? renamed : n));
  }
  const tree = indexTree(nodes.filter((n) => n.id !== id));
  const siblings = [...childrenOf(tree, parentId)];
  const index = change.index ?? siblings.length;
  siblings.splice(Math.max(0, Math.min(index, siblings.length)), 0, renamed);
  const order = new Map(siblings.map((n, i) => [n.id, i + 1]));
  return nodes.map((n) => {
    const base = n.id === id ? renamed : n;
    const sortOrder = order.get(n.id);
    return sortOrder === undefined ? base : { ...base, sortOrder };
  });
}
