import type { Root } from "mdast";
import remarkGfm from "remark-gfm";
import remarkParse from "remark-parse";
import { unified, type Processor } from "unified";
import { erableFromMarkdown } from "./mdastErable";
import { erableSyntax } from "./micromarkErable";

/** remark plugin: Erable references and embeds, raw HTML as text (docs/markdown-syntax.md). */
export function remarkErable(this: Processor) {
  const data = this.data();
  (data.micromarkExtensions ??= []).push(erableSyntax());
  (data.fromMarkdownExtensions ??= []).push(erableFromMarkdown);
}

/**
 * The parser shared by rendering, the table of contents and the tests: CommonMark + GFM (single tildes are not
 * strikethrough, as in Markdig) + Erable.
 */
export function createMarkdownParser() {
  return unified().use(remarkParse).use(remarkGfm, { singleTilde: false }).use(remarkErable);
}

export function parseMarkdown(markdown: string): Root {
  return createMarkdownParser().parse(markdown);
}
