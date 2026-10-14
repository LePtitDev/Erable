import type { MarkdownConfig } from "@lezer/markdown";
import { matchReference } from "../../shared/markdown/referenceSyntax";

const openBracket = 91; // [
const exclamation = 33; // !

/**
 * `[[ID…]]` and `![[ID…]]` for the editor's parser (@lezer/markdown), by the shared grammar (referenceSyntax.ts).
 * Recognized before links and images: otherwise each `[[` opens two link delimiters, and resolving them is
 * quadratic in a long paragraph (≈ 190 ms per keystroke on 5 000 lines). Code, escapes and autolinks are handled by
 * the built-in parsers that run first, as in docs/markdown-syntax.md (R6, R7).
 */
export const erableReferences: MarkdownConfig = {
  defineNodes: [{ name: "ErableReference" }, { name: "ErableEmbed" }],
  parseInline: [
    {
      name: "ErableReference",
      before: "Link",
      parse(cx, next, pos) {
        if (next !== openBracket && next !== exclamation) return -1;
        if (next === exclamation && cx.char(pos + 1) !== openBracket) return -1;
        const match = matchReference(cx.slice(pos, cx.end), 0);
        if (match === null) return -1;
        return cx.addElement(
          cx.elt(match.embed ? "ErableEmbed" : "ErableReference", pos, pos + match.length),
        );
      },
    },
  ],
};
