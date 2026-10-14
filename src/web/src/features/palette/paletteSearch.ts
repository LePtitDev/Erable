import type { AttachmentItem } from "../../shared/api/pages";
import type { ElementKind, TreeNodeItem } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { ancestorsOf, indexTree, normalize } from "../explorer/treeModel";

/** Tabs of the palette, in the order Tab cycles through them (F05.R3, mockup). */
export const paletteTabs = ["all", "page", "attachment", "category", "propertyDefinition"] as const;
export type PaletteTab = (typeof paletteTabs)[number];

export interface PaletteItem {
  readonly id: ElementId;
  readonly kind: ElementKind;
  readonly name: string;
  /** « Pages / Personnages / PNJ »; for an attachment, its page first: « Hautbois › carte_hautbois.webp » is its label. */
  readonly path: string;
  /** What the list shows: the name, or « Page › fichier » for an attachment. */
  readonly label: string;
  /** Normalized texts searched (label and name), computed once. */
  readonly haystack: string;
  readonly words: readonly string[];
}

/** Elements that can be referenced: pages, attachments, categories, properties, link types (not roots nor folders). */
export function buildPaletteIndex(
  nodes: readonly TreeNodeItem[],
  attachments: readonly AttachmentItem[],
): PaletteItem[] {
  const tree = indexTree(nodes);
  const pathOf = (id: ElementId) =>
    ancestorsOf(tree, id)
      .reverse()
      .map((n) => n.name)
      .join(" / ");
  const items: PaletteItem[] = [];
  for (const node of nodes) {
    if (node.kind === "root" || node.kind === "folder" || node.kind === "attachment") continue;
    items.push(item(node.id, node.kind, node.name, node.name, pathOf(node.id)));
  }
  for (const a of attachments) {
    const page = tree.byId.get(a.pageId);
    const pageName = page?.name ?? "";
    const label = page === undefined ? a.fileName : `${pageName} › ${a.fileName}`;
    items.push(
      item(
        a.id,
        "attachment",
        a.fileName,
        label,
        page === undefined ? "" : `${pathOf(a.pageId)} / ${pageName}`,
      ),
    );
  }
  return items;
}

function item(
  id: ElementId,
  kind: ElementKind,
  name: string,
  label: string,
  path: string,
): PaletteItem {
  const haystack = normalize(label);
  return {
    id,
    kind,
    name,
    label,
    path,
    haystack,
    words: haystack.split(/[\s›/_.,'’-]+/u).filter((w) => w !== ""),
  };
}

/**
 * Score of an item for a query (higher is better), null when it does not match. The rule, simple on purpose:
 * prefix of the name (or of the file name) > prefix of a word > substring > letters in order from the start of a
 * word (« cpo » finds « Capitaine Orsen », « orse » does not find « carte_hautbois »).
 * Ties: shorter label first, then alphabetical (stable, predictable).
 */
export function score(entry: PaletteItem, query: string): number | null {
  if (query === "") return 0;
  const name = normalize(entry.name);
  if (name.startsWith(query) || entry.haystack.startsWith(query)) return 400;
  if (entry.words.some((w) => w.startsWith(query))) return 300;
  if (entry.haystack.includes(query)) return 200;
  const first = query[0] ?? "";
  if (!entry.words.some((w) => w.startsWith(first))) return null;
  let at =
    entry.words
      .map((w) => (w.startsWith(first) ? entry.haystack.indexOf(w) : -1))
      .find((i) => i >= 0) ?? 0;
  for (const c of query) {
    at = entry.haystack.indexOf(c, at);
    if (at < 0) return null;
    at++;
  }
  return 100;
}

export function inTab(entry: PaletteItem, tab: PaletteTab): boolean {
  return (
    tab === "all" ||
    entry.kind === tab ||
    (tab === "propertyDefinition" && entry.kind === "linkType")
  );
}

/** The best `limit` matches of `query` in `tab`. */
export function searchPalette(
  items: readonly PaletteItem[],
  query: string,
  tab: PaletteTab,
  limit = 30,
): PaletteItem[] {
  const q = normalize(query.trim());
  const scored: { entry: PaletteItem; score: number }[] = [];
  for (const entry of items) {
    if (!inTab(entry, tab)) continue;
    const s = score(entry, q);
    if (s !== null) scored.push({ entry, score: s });
  }
  scored.sort(
    (a, b) =>
      b.score - a.score ||
      a.entry.label.length - b.entry.label.length ||
      a.entry.label.localeCompare(b.entry.label, "fr"),
  );
  return scored.slice(0, limit).map((s) => s.entry);
}
