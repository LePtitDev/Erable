import { markdown, markdownLanguage } from "@codemirror/lang-markdown";
import { syntaxTree } from "@codemirror/language";
import {
  RangeSetBuilder,
  StateEffect,
  StateField,
  type EditorState,
  type Extension,
} from "@codemirror/state";
import {
  Decoration,
  EditorView,
  ViewPlugin,
  WidgetType,
  type DecorationSet,
  type ViewUpdate,
} from "@codemirror/view";
import type { ElementKind } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { matchReference, type ReferenceMatch } from "../../shared/markdown/referenceSyntax";
import { erableReferences } from "./lezerErable";

/** What a pill shows for an ID: its current name and kind, or a broken state. */
export interface PillTarget {
  readonly name: string | null;
  readonly kind: ElementKind | null;
  readonly state: "existing" | "deleted" | "unknown";
}

export interface FoundPill {
  readonly from: number;
  readonly to: number;
  readonly match: ReferenceMatch;
}

/** Markdown for CodeMirror with the Erable references as syntax nodes (lezerErable.ts). */
export function markdownWithReferences(): Extension {
  return markdown({ base: markdownLanguage, extensions: [erableReferences] });
}

/**
 * The `[[ID…]]` and `![[ID…]]` between `from` and `to`, read from the syntax tree (so never in code nor after an
 * escape, R6–R7). A reference that starts in the range is found whole even if it ends after it.
 */
export function findPills(state: EditorState, from = 0, to = state.doc.length): FoundPill[] {
  const result: FoundPill[] = [];
  syntaxTree(state).iterate({
    from,
    to,
    enter: (node) => {
      if (node.name !== "ErableReference" && node.name !== "ErableEmbed") return;
      const match = matchReference(state.sliceDoc(node.from, node.to), 0);
      if (match !== null) result.push({ from: node.from, to: node.from + match.length, match });
      return false;
    },
  });
  return result;
}

/** Every distinct ID referenced in the document (for one `resolve` call). */
export function referencedIds(state: EditorState): ElementId[] {
  return [...new Set(findPills(state).map((p) => p.match.id))];
}

// ─── State: resolved names and the « raw IDs » switch ─────────────────────

export const setTargets = StateEffect.define<ReadonlyMap<ElementId, PillTarget>>();
export const setRawIds = StateEffect.define<boolean>();

export const targetsField = StateField.define<ReadonlyMap<ElementId, PillTarget>>({
  create: () => new Map(),
  update: (value, tr) => tr.effects.reduce((v, e) => (e.is(setTargets) ? e.value : v), value),
});

/** F05.R2: shows the source text instead of the pills, without touching the document. */
export const rawIdsField = StateField.define<boolean>({
  create: () => false,
  update: (value, tr) => tr.effects.reduce((v, e) => (e.is(setRawIds) ? e.value : v), value),
});

// ─── Decorations ──────────────────────────────────────────────────────────

class PillWidget extends WidgetType {
  constructor(
    readonly text: string,
    readonly kind: ElementKind | null,
    readonly broken: boolean,
    readonly embed: boolean,
    readonly id: ElementId,
  ) {
    super();
  }

  override eq(other: PillWidget) {
    return (
      other.text === this.text &&
      other.kind === this.kind &&
      other.broken === this.broken &&
      other.embed === this.embed &&
      other.id === this.id
    );
  }

  toDOM() {
    const pill = document.createElement("span");
    pill.className = `cm-pill${this.broken ? " cm-pill-broken" : ""}${this.embed ? " cm-pill-embed" : ""}`;
    pill.dataset["kind"] = this.kind ?? "unknown";
    pill.dataset["id"] = this.id;
    pill.title = this.id;
    pill.textContent = this.text;
    return pill;
  }

  override ignoreEvent() {
    return false;
  }
}

/** Label of a pill: the reference label, else the current name, else the ID. */
export function pillText(match: ReferenceMatch, target: PillTarget | undefined): string {
  return match.label ?? target?.name ?? match.id;
}

/**
 * Replacement decorations of the pills in `ranges` (the visible part of the editor: the budget of 5 000 lines,
 * docs/testing.md §11). Pure: tested on an EditorState, without DOM.
 */
export function pillDecorations(
  state: EditorState,
  ranges: readonly { from: number; to: number }[],
): DecorationSet {
  if (state.field(rawIdsField, false) === true) return Decoration.none;
  const targets = state.field(targetsField, false) ?? new Map<ElementId, PillTarget>();
  const builder = new RangeSetBuilder<Decoration>();
  let last = -1;
  for (const range of ranges) {
    for (const pill of findPills(state, range.from, range.to)) {
      if (pill.from < last) continue;
      const target = targets.get(pill.match.id);
      builder.add(
        pill.from,
        pill.to,
        Decoration.replace({
          widget: new PillWidget(
            pillText(pill.match, target),
            target?.kind ?? null,
            target !== undefined && target.state !== "existing",
            pill.match.embed,
            pill.match.id,
          ),
        }),
      );
      last = pill.to;
    }
  }
  return builder.finish();
}

const pillPlugin = ViewPlugin.fromClass(
  class {
    decorations: DecorationSet;

    constructor(view: EditorView) {
      this.decorations = pillDecorations(view.state, view.visibleRanges);
    }

    update(update: ViewUpdate) {
      const switched = update.transactions.some((tr) =>
        tr.effects.some((e) => e.is(setTargets) || e.is(setRawIds)),
      );
      if (
        update.docChanged ||
        update.viewportChanged ||
        switched ||
        syntaxTree(update.startState) !== syntaxTree(update.state)
      ) {
        this.decorations = pillDecorations(update.state, update.view.visibleRanges);
      }
    }
  },
  {
    decorations: (plugin) => plugin.decorations,
    // F05.R1: the cursor jumps over a pill and Backspace deletes it whole.
    provide: (plugin) =>
      EditorView.atomicRanges.of((view) => view.plugin(plugin)?.decorations ?? Decoration.none),
  },
);

/** The pill extension: state fields and the view plugin. */
export function pills(): Extension {
  return [targetsField, rawIdsField, pillPlugin];
}
