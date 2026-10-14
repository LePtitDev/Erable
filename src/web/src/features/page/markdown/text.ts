import type { ElementId } from "../../../shared/ids/elementId";
import { analyzeMarkdown } from "../../../shared/markdown/analyze";

/** `7A1C4E90B3F2D615` → `7A1C…D615` (mockup preview card). */
export function shortId(id: ElementId): string {
  return `${id.slice(0, 4)}…${id.slice(-4)}`;
}

/** The beginning of a page, as plain text, cut on a word. */
export function excerptOf(markdown: string, length = 180): string {
  const text = analyzeMarkdown(markdown).plainText;
  if (text.length <= length) return text;
  const cut = text.slice(0, length);
  return `${cut.slice(0, Math.max(cut.lastIndexOf(" "), length - 20))}…`;
}
