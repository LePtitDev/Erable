const heading = /^ {0,3}#{1,6}[ \t]+(.*?)(?:[ \t]+#+)?[ \t]*$/;
const fence = /^ {0,3}(`{3,}|~{3,})/;
const explicitAnchor = /[ \t]*\{#[^}]*\}[ \t]*$/;

/**
 * The section the cursor is in: the text of the last heading at or before `line` (1-based), or null above the first
 * one. Headings inside code fences do not count. Told to the other editors of the page (« Camille R. modifie aussi
 * cette page — section « Histoire » », F14.R3), so it is the title as written, without its explicit anchor.
 */
export function sectionAt(markdown: string, line: number): string | null {
  const lines = markdown.split("\n");
  let found: string | null = null;
  let openFence: string | null = null;
  for (const text of lines.slice(0, Math.max(0, line))) {
    const fenceMatch = fence.exec(text);
    if (fenceMatch?.[1] !== undefined) {
      const marker = fenceMatch[1];
      if (openFence === null) openFence = marker;
      else if (marker[0] === openFence[0] && marker.length >= openFence.length) openFence = null;
      continue;
    }
    if (openFence !== null) continue;
    const match = heading.exec(text);
    if (match?.[1] !== undefined) {
      const title = match[1].replace(explicitAnchor, "").trim();
      found = title === "" ? null : title;
    }
  }
  return found;
}
