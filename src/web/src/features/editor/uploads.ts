import {
  Facet,
  StateEffect,
  StateField,
  type EditorState,
  type Extension,
  type TransactionSpec,
} from "@codemirror/state";
import { Decoration, EditorView, WidgetType, type DecorationSet } from "@codemirror/view";
import type { ElementId } from "../../shared/ids/elementId";

/**
 * Files being uploaded from the editor (F06.R1). Each one is a **widget at a position**, never text: the document
 * cannot be saved with a placeholder in it, and a failed upload leaves nothing behind. The position follows the
 * edits made meanwhile; once the attachment exists, `![[ID]]` replaces the widget at that position.
 */
export interface PendingUpload {
  readonly key: string;
  readonly pos: number;
  readonly name: string;
  readonly fraction: number;
}

export const addUpload = StateEffect.define<{ key: string; pos: number; name: string }>({
  map: (value, mapping) => ({ ...value, pos: mapping.mapPos(value.pos) }),
});
export const progressUpload = StateEffect.define<{ key: string; fraction: number }>();
export const removeUpload = StateEffect.define<string>();

export const uploadsField = StateField.define<readonly PendingUpload[]>({
  create: () => [],
  update(uploads, tr) {
    let next = tr.docChanged
      ? uploads.map((u) => ({ ...u, pos: tr.changes.mapPos(u.pos) }))
      : uploads;
    for (const effect of tr.effects) {
      if (effect.is(addUpload)) next = [...next, { ...effect.value, fraction: 0 }];
      else if (effect.is(progressUpload))
        next = next.map((u) =>
          u.key === effect.value.key ? { ...u, fraction: effect.value.fraction } : u,
        );
      else if (effect.is(removeUpload)) next = next.filter((u) => u.key !== effect.value);
    }
    return next;
  },
});

/** Accessible label of a placeholder (translated by the page editor). */
const uploadLabel = Facet.define<(name: string) => string, (name: string) => string>({
  combine: (values) => values[0] ?? ((name) => name),
});

function uploadDecorations(
  uploads: readonly PendingUpload[],
  label: (name: string) => string,
): DecorationSet {
  return Decoration.set(
    [...uploads]
      .sort((a, b) => a.pos - b.pos)
      .map((u) =>
        Decoration.widget({ widget: new UploadWidget(u, label(u.name)), side: 1 }).range(u.pos),
      ),
  );
}

class UploadWidget extends WidgetType {
  constructor(
    readonly upload: PendingUpload,
    readonly label: string,
  ) {
    super();
  }

  override eq(other: UploadWidget) {
    return (
      other.upload.key === this.upload.key &&
      other.upload.fraction === this.upload.fraction &&
      other.label === this.label
    );
  }

  toDOM() {
    const pill = document.createElement("span");
    pill.className = "cm-pill cm-pill-pending cm-upload";
    pill.setAttribute("role", "progressbar");
    pill.setAttribute("aria-label", this.label);
    pill.setAttribute("aria-valuemin", "0");
    pill.setAttribute("aria-valuemax", "100");
    const percent = Math.round(this.upload.fraction * 100);
    pill.setAttribute("aria-valuenow", String(percent));
    pill.textContent = `${this.upload.name} · ${String(percent)} %`;
    return pill;
  }
}

/** Configures the editor for uploads: the label of the placeholders. */
export function uploads(label: (name: string) => string): Extension {
  return [
    uploadsField,
    uploadLabel.of(label),
    EditorView.decorations.compute([uploadsField, uploadLabel], (state) =>
      uploadDecorations(state.field(uploadsField), state.facet(uploadLabel)),
    ),
  ];
}

/** Where the placeholder `key` currently is, or null once removed. */
export function uploadPos(state: EditorState, key: string): number | null {
  return state.field(uploadsField, false)?.find((u) => u.key === key)?.pos ?? null;
}

/** Replaces the placeholder `key` by `![[ID]]` (and a line break between files dropped together); null once gone. */
export function completeUpload(
  state: EditorState,
  key: string,
  id: ElementId,
  separate: boolean,
): TransactionSpec | null {
  const pos = uploadPos(state, key);
  if (pos === null) return null;
  return {
    changes: { from: pos, insert: `![[${id}]]${separate ? "\n" : ""}` },
    effects: removeUpload.of(key),
    userEvent: "input.attachment",
  };
}
