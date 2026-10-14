import type { Heading, Nodes, Root } from "mdast";
import type { ElementId } from "../ids/elementId";
import type { EmbedAttributes } from "./referenceSyntax";
import { parseMarkdown } from "./remarkErable";
import { createUniqueAnchors, slugify } from "./slug";

export interface FoundReference {
  readonly id: ElementId;
  readonly anchor: string | null;
  readonly label: string | null;
  readonly embed: boolean;
  readonly attrs: EmbedAttributes | null;
  /** UTF-16 offsets in the source (R10). */
  readonly start: number;
  readonly end: number;
}

export interface HeadingInfo {
  readonly level: number;
  readonly text: string;
  readonly anchor: string;
  readonly node: Heading;
}

const explicitAnchor = /^(.*?)\s+\{#([A-Za-z0-9_-]+)\}$/s;

/** Collapses every run of whitespace to one space and trims (docs/markdown-syntax.md §4). */
export function normalizeWhitespace(text: string): string {
  return text.replace(/\s+/g, " ").trim();
}

/** `## Titre {#ancre}` → { text: "Titre", anchor: "ancre" }. */
export function splitExplicitAnchor(text: string): { text: string; anchor: string | null } {
  const match = explicitAnchor.exec(text);
  return match?.[1] !== undefined && match[2] !== undefined
    ? { text: match[1].trimEnd(), anchor: match[2] }
    : { text, anchor: null };
}

/** Inline text of a node: a reference counts as its label only (§2.2). */
export function inlineText(node: Nodes): string {
  switch (node.type) {
    case "text":
    case "inlineCode":
      return node.value;
    case "break":
      return "\n";
    case "image":
    case "imageReference":
      return node.alt ?? "";
    case "erableReference":
    case "erableEmbed":
      return node.label ?? "";
    default:
      return "children" in node ? node.children.map((c) => inlineText(c as Nodes)).join("") : "";
  }
}

function blockText(node: Nodes): string {
  switch (node.type) {
    case "heading":
      return splitExplicitAnchor(normalizeWhitespace(inlineText(node))).text + "\n";
    case "paragraph":
    case "tableCell":
      return inlineText(node) + "\n";
    case "code":
      return node.value + "\n";
    case "html":
    case "thematicBreak":
    case "definition":
      return "";
    default:
      return "children" in node ? node.children.map((c) => blockText(c as Nodes)).join("") : "";
  }
}

/** Plain text for search, normalized (docs/markdown-syntax.md §4). */
export function plainText(tree: Root): string {
  return normalizeWhitespace(blockText(tree));
}

function visit(node: Nodes, onNode: (node: Nodes) => void) {
  onNode(node);
  if ("children" in node) {
    for (const child of node.children) {
      visit(child, onNode);
    }
  }
}

/** Headings in document order with their unique anchors (§3). */
export function headings(tree: Root): HeadingInfo[] {
  const unique = createUniqueAnchors();
  const result: HeadingInfo[] = [];
  visit(tree, (node) => {
    if (node.type === "heading") {
      const { text, anchor } = splitExplicitAnchor(normalizeWhitespace(inlineText(node)));
      result.push({ level: node.depth, text, anchor: unique(anchor ?? slugify(text)), node });
    }
  });
  return result;
}

export function references(tree: Root): FoundReference[] {
  const result: FoundReference[] = [];
  visit(tree, (node) => {
    if (node.type === "erableReference" || node.type === "erableEmbed") {
      result.push({
        id: node.id,
        anchor: node.type === "erableReference" ? node.anchor : null,
        label: node.label,
        embed: node.type === "erableEmbed",
        attrs: node.type === "erableEmbed" ? node.attrs : null,
        start: node.position?.start.offset ?? -1,
        end: node.position?.end.offset ?? -1,
      });
    }
  });
  return result;
}

/** Same analysis as the backend's `ErableMarkdown.Analyze`. */
export function analyzeMarkdown(markdown: string) {
  const tree = parseMarkdown(markdown);
  return {
    tree,
    references: references(tree),
    headings: headings(tree),
    plainText: plainText(tree),
  };
}
