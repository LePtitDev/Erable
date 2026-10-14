import {
  Prec,
  StateEffect,
  StateField,
  type EditorState,
  type Extension,
  type Transaction,
  type TransactionSpec,
} from "@codemirror/state";
import { EditorView, keymap } from "@codemirror/view";
import type { ElementId } from "../../shared/ids/elementId";
import { platformKey, shortcuts } from "../../shared/keyboard/shortcuts";

/**
 * The insert palette inside the editor (F05.R3, docs/architecture.md §6.2). Opened by typing `[[` or `![[` — the text
 * typed after it is the query, and stays in the editor if the palette is closed with Escape (E2E-05-06) — or by the
 * shortcut, which inserts nothing (the palette then has its own search field).
 */
export interface PaletteState {
  readonly open: boolean;
  readonly trigger: "typed" | "shortcut";
  /** Start of `[[` / `![[` (typed), or the cursor (shortcut): what an insertion replaces starts here. */
  readonly from: number;
  readonly embed: boolean;
  /** DOM ID of the highlighted option, for aria-activedescendant. */
  readonly activeOption: string | null;
}

const closed: PaletteState = {
  open: false,
  trigger: "typed",
  from: 0,
  embed: false,
  activeOption: null,
};

export const openPalette = StateEffect.define<{
  trigger: "typed" | "shortcut";
  from: number;
  embed: boolean;
}>();
export const closePalette = StateEffect.define();
export const setActiveOption = StateEffect.define<string | null>();

/** Length of the opening marker: `[[` or `![[`. */
function markerLength(state: PaletteState): number {
  return state.trigger === "typed" ? (state.embed ? 3 : 2) : 0;
}

/** `[[` just typed at the cursor (not escaped): where it starts and whether it is an embed. */
export function typedOpening(state: EditorState): { from: number; embed: boolean } | null {
  const head = state.selection.main.head;
  if (!state.selection.main.empty || state.sliceDoc(head - 2, head) !== "[[") return null;
  const before = state.sliceDoc(head - 3, head - 2);
  if (before === "\\") return null;
  return before === "!" ? { from: head - 3, embed: true } : { from: head - 2, embed: false };
}

function next(value: PaletteState, tr: Transaction): PaletteState {
  for (const effect of tr.effects) {
    if (effect.is(openPalette)) return { ...closed, open: true, ...effect.value };
    if (effect.is(closePalette)) return closed;
    if (effect.is(setActiveOption)) value = { ...value, activeOption: effect.value };
  }
  if (!value.open) {
    const opening = tr.docChanged && tr.isUserEvent("input.type") ? typedOpening(tr.state) : null;
    return opening === null ? value : { ...closed, open: true, trigger: "typed", ...opening };
  }
  const from = tr.changes.mapPos(value.from);
  const moved = { ...value, from };
  if (value.trigger === "shortcut")
    return tr.docChanged || tr.selection !== undefined ? closed : moved;
  // Typed: the palette follows the text after the marker, and closes when the cursor leaves it.
  const head = tr.state.selection.main.head;
  const start = from + markerLength(moved);
  if (tr.state.sliceDoc(from, start) !== (value.embed ? "![[" : "[[") || head < start)
    return closed;
  const query = tr.state.sliceDoc(start, head);
  return /[\n\]]/.test(query) || query.length > 80 ? closed : moved;
}

export const paletteField = StateField.define<PaletteState>({
  create: () => closed,
  update: next,
  provide: (field) =>
    EditorView.contentAttributes.compute([field], (state) => {
      const p = state.field(field);
      return p.open && p.activeOption !== null
        ? { "aria-controls": paletteListId, "aria-activedescendant": p.activeOption }
        : {};
    }),
});

export const paletteListId = "insert-palette-options";

/** The text searched: what was typed after `[[` (shortcut mode: the palette's own field). */
export function paletteQuery(state: EditorState): string {
  const p = state.field(paletteField);
  if (!p.open || p.trigger !== "typed") return "";
  return state.sliceDoc(p.from + markerLength(p), state.selection.main.head);
}

/** Replaces `[[query` (or inserts at the cursor) by `[[ID]]` / `![[ID]]`, cursor after it, palette closed. */
export function insertReference(
  state: EditorState,
  id: ElementId,
  embed: boolean,
): TransactionSpec {
  const p = state.field(paletteField);
  const from = p.open ? p.from : state.selection.main.from;
  const to = p.open && p.trigger === "typed" ? state.selection.main.head : state.selection.main.to;
  const text = `${embed ? "!" : ""}[[${id}]]`;
  return {
    changes: { from, to, insert: text },
    selection: { anchor: from + text.length },
    effects: closePalette.of(null),
    userEvent: "input.complete",
  };
}

/** What the palette component does with the keys while it is open. */
export interface PaletteKeys {
  move: (delta: number) => void;
  /** Inserts the highlighted option; false when there is none. */
  pick: (embed: boolean) => boolean;
  tab: (delta: number) => void;
}

/** State field, keys (above the editor's own) and the opening shortcut (configurable in M5-01). */
export function insertPalette(keys: PaletteKeys): Extension {
  const whenOpen = (run: (view: EditorView) => void) => (view: EditorView) => {
    if (!view.state.field(paletteField).open) return false;
    run(view);
    return true;
  };
  return [
    paletteField,
    Prec.highest(
      keymap.of([
        {
          key: "ArrowDown",
          run: whenOpen(() => {
            keys.move(1);
          }),
        },
        {
          key: "ArrowUp",
          run: whenOpen(() => {
            keys.move(-1);
          }),
        },
        { key: "Enter", run: (view) => view.state.field(paletteField).open && keys.pick(false) },
        {
          key: "Shift-Enter",
          run: (view) => view.state.field(paletteField).open && keys.pick(true),
        },
        {
          key: "Tab",
          run: whenOpen(() => {
            keys.tab(1);
          }),
        },
        {
          key: "Shift-Tab",
          run: whenOpen(() => {
            keys.tab(-1);
          }),
        },
        {
          key: "Escape",
          run: whenOpen((view) => {
            view.dispatch({ effects: closePalette.of(null) });
          }),
        },
        {
          key: platformKey(shortcuts.openPalette),
          preventDefault: true,
          run: (view) => {
            const head = view.state.selection.main.head;
            view.dispatch({
              effects: openPalette.of({ trigger: "shortcut", from: head, embed: false }),
            });
            return true;
          },
        },
      ]),
    ),
  ];
}
