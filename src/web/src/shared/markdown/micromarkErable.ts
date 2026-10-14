import { asciiHexDigit, markdownLineEnding } from "micromark-util-character";
import type {
  Code,
  Construct,
  Effects,
  Extension,
  State,
  TokenizeContext,
} from "micromark-util-types";
import {
  isAnchorChar,
  matchReference,
  maxAttributesLength,
  maxLabelLength,
  readAttributes,
} from "./referenceSyntax";

declare module "micromark-util-types" {
  interface TokenTypeMap {
    erableReference: "erableReference";
    erableEmbed: "erableEmbed";
    erableEmbedAttributes: "erableEmbedAttributes";
  }
}

/**
 * micromark syntax extension for `[[ID]]` and `![[ID]]` (docs/markdown-syntax.md), plus raw HTML disabled
 * (F04.R5): HTML is then plain text, so references inside it are found exactly as Markdig finds them.
 * The state machine follows the grammar step by step; the consumed text is finally checked by
 * {@link matchReference}, the single TypeScript reading of the grammar.
 */
export function erableSyntax(): Extension {
  return {
    text: { 91: reference, 33: embed },
    disable: { null: ["htmlFlow", "htmlText"] },
  };
}

const tab = -2;
const virtualSpace = -1;
const codes = {
  exclamation: 33,
  numberSign: 35,
  leftBracket: 91,
  backslash: 92,
  rightBracket: 93,
  leftBrace: 123,
  verticalBar: 124,
  rightBrace: 125,
} as const;

// Runs before the CommonMark link and image openers on the same characters.
const reference: Construct = {
  name: "erableReference",
  tokenize: tokenizeReference,
  add: "before",
};
const embed: Construct = { name: "erableEmbed", tokenize: tokenizeEmbed, add: "before" };
const attributes: Construct = { name: "erableEmbedAttributes", tokenize: tokenizeAttributes };

function charOf(code: number): string {
  return code === tab ? "\t" : String.fromCharCode(code);
}

function tokenizeEmbed(this: TokenizeContext, effects: Effects, ok: State, nok: State): State {
  return createReferenceTokenizer(effects, ok, nok, true);
}

function tokenizeReference(this: TokenizeContext, effects: Effects, ok: State, nok: State): State {
  return createReferenceTokenizer(effects, ok, nok, false);
}

function createReferenceTokenizer(
  effects: Effects,
  ok: State,
  nok: State,
  isEmbed: boolean,
): State {
  const type = isEmbed ? "erableEmbed" : "erableReference";
  let text = "";
  let count = 0;

  const take = (code: Code) => {
    if (code !== null && code !== virtualSpace) {
      text += charOf(code);
    }
    effects.consume(code);
  };

  const start: State = (code) => {
    effects.enter(type);
    if (isEmbed) {
      if (code !== codes.exclamation) return nok(code);
      take(code);
      return open1;
    }
    return open1(code);
  };
  const open1: State = (code) => {
    if (code !== codes.leftBracket) return nok(code);
    take(code);
    return open2;
  };
  const open2: State = (code) => {
    if (code !== codes.leftBracket) return nok(code);
    take(code);
    return id;
  };
  const id: State = (code) => {
    if (count < 16 && asciiHexDigit(code)) {
      take(code);
      count++;
      return id;
    }
    return count === 16 ? afterId(code) : nok(code);
  };
  const afterId: State = (code) => {
    if (code === codes.numberSign && !isEmbed) {
      take(code);
      return anchorFirst;
    }
    return afterAnchor(code);
  };
  const anchorFirst: State = (code) => {
    if (!isAnchorCode(code)) return nok(code);
    take(code);
    return anchorRest;
  };
  const anchorRest: State = (code) => {
    if (isAnchorCode(code)) {
      take(code);
      return anchorRest;
    }
    return afterAnchor(code);
  };
  const afterAnchor: State = (code) => {
    if (code === codes.verticalBar) {
      take(code);
      count = 0;
      return label;
    }
    if (code === codes.backslash) {
      take(code);
      return escapedBar;
    }
    if (code === codes.rightBracket) {
      take(code);
      return close;
    }
    return nok(code);
  };
  // `\|`: the escaped separator, mandatory in table rows (R12).
  const escapedBar: State = (code) => {
    if (code !== codes.verticalBar) return nok(code);
    take(code);
    count = 0;
    return label;
  };
  const label: State = (code) => {
    if (code === null || markdownLineEnding(code)) return nok(code);
    if (code === codes.rightBracket) {
      take(code);
      return labelBracket;
    }
    if (code === virtualSpace) {
      take(code);
      return label;
    }
    if (count >= maxLabelLength) return nok(code);
    take(code);
    count++;
    return label;
  };
  const labelBracket: State = (code) => {
    if (code === codes.rightBracket) {
      take(code);
      return done;
    }
    // The previous "]" was part of the label.
    if (count >= maxLabelLength) return nok(code);
    count++;
    return label(code);
  };
  const close: State = (code) => {
    if (code !== codes.rightBracket) return nok(code);
    take(code);
    return done;
  };
  const done: State = (code) => {
    const match = matchReference(text);
    if (match?.length !== text.length) return nok(code);
    if (isEmbed) {
      return effects.attempt(attributes, finish, finish)(code);
    }
    return finish(code);
  };
  const finish: State = (code) => {
    effects.exit(type);
    return ok(code);
  };
  return start;
}

function tokenizeAttributes(this: TokenizeContext, effects: Effects, ok: State, nok: State): State {
  let text = "";

  const start: State = (code) => {
    if (code !== codes.leftBrace) return nok(code);
    effects.enter("erableEmbedAttributes");
    text += "{";
    effects.consume(code);
    return inside;
  };
  const inside: State = (code) => {
    if (code === null || markdownLineEnding(code) || code === tab || code === virtualSpace) {
      return nok(code);
    }
    if (code === codes.rightBrace) {
      text += "}";
      effects.consume(code);
      return readAttributes(text, 0)?.length === text.length ? end : nok;
    }
    // `text` holds the opening brace: the body is one character shorter.
    if (text.length - 1 >= maxAttributesLength) return nok(code);
    text += String.fromCharCode(code);
    effects.consume(code);
    return inside;
  };
  const end: State = (code) => {
    effects.exit("erableEmbedAttributes");
    return ok(code);
  };
  return start;
}

function isAnchorCode(code: Code): boolean {
  return code !== null && code >= 0 && isAnchorChar(String.fromCharCode(code));
}
