import type { Node } from "mdast";
import type { CompileContext, Extension, Token } from "mdast-util-from-markdown";
import type { ElementId } from "../ids/elementId";
import { matchReference, type EmbedAttributes } from "./referenceSyntax";

/** `[[ID]]`, `[[ID#anchor]]`, `[[ID|label]]` in the syntax tree. */
export interface ErableReference extends Node {
  type: "erableReference";
  id: ElementId;
  anchor: string | null;
  label: string | null;
}

/** `![[ID]]`, `![[ID|caption]]{width=… align=…}` in the syntax tree. */
export interface ErableEmbed extends Node {
  type: "erableEmbed";
  id: ElementId;
  label: string | null;
  attrs: EmbedAttributes | null;
}

declare module "mdast" {
  interface PhrasingContentMap {
    erableReference: ErableReference;
    erableEmbed: ErableEmbed;
  }
  interface RootContentMap {
    erableReference: ErableReference;
    erableEmbed: ErableEmbed;
  }
}

function exitReference(this: CompileContext, token: Token) {
  const node = this.stack[this.stack.length - 1] as ErableReference | ErableEmbed;
  // The tokenizer only accepts text that matches the grammar: re-reading it cannot fail.
  const match = matchReference(this.sliceSerialize(token));
  if (match !== null) {
    node.id = match.id;
    node.label = match.label;
    if (node.type === "erableReference") {
      node.anchor = match.anchor;
    } else {
      node.attrs = match.attrs;
    }
  }
  this.exit(token);
}

/** Turns the micromark tokens of {@link erableSyntax} into mdast nodes. */
export const erableFromMarkdown: Extension = {
  enter: {
    erableReference(token) {
      this.enter(
        { type: "erableReference", id: "" as ElementId, anchor: null, label: null },
        token,
      );
    },
    erableEmbed(token) {
      this.enter({ type: "erableEmbed", id: "" as ElementId, label: null, attrs: null }, token);
    },
  },
  exit: {
    erableReference: exitReference,
    erableEmbed: exitReference,
  },
};
