import type { Definition, Heading, Nodes, Root } from "mdast";
import type { HeadingInfo } from "../../../shared/markdown/analyze";

/** URLs a rendered link or image may point to; everything else (javascript:, data:, vbscript:…) is dropped. */
export function safeUrl(url: string | null | undefined, kind: "link" | "image"): string | null {
  if (url === null || url === undefined) return null;
  const trimmed = url.trim();
  if (
    kind === "link" &&
    (trimmed.startsWith("#") || (trimmed.startsWith("/") && !trimmed.startsWith("//")))
  ) {
    return trimmed;
  }
  try {
    const parsed = new URL(trimmed);
    const allowed = kind === "link" ? ["http:", "https:", "mailto:"] : ["http:", "https:"];
    return allowed.includes(parsed.protocol) ? parsed.href : null;
  } catch {
    return null;
  }
}

export function anchorsOf(headings: readonly HeadingInfo[]): Map<Heading, string> {
  return new Map(headings.map((h) => [h.node, h.anchor]));
}

export function definitionsOf(tree: Root): Map<string, Definition> {
  const result = new Map<string, Definition>();
  const visit = (node: Nodes) => {
    if (node.type === "definition") result.set(node.identifier.toLowerCase(), node);
    if ("children" in node) {
      for (const child of node.children) visit(child);
    }
  };
  visit(tree);
  return result;
}
