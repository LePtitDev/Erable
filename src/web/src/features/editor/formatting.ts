import { EditorSelection, type EditorState, type TransactionSpec } from "@codemirror/state";

/**
 * Wraps each selection in `marker` (`**` bold, `*` italic), or removes it when the selection is already wrapped;
 * an empty selection gets an empty pair with the cursor inside (E2E-05-13).
 */
export function toggleInline(state: EditorState, marker: string): TransactionSpec {
  return state.changeByRange((range) => {
    const before = state.sliceDoc(range.from - marker.length, range.from);
    const after = state.sliceDoc(range.to, range.to + marker.length);
    if (before === marker && after === marker) {
      return {
        changes: [
          { from: range.from - marker.length, to: range.from },
          { from: range.to, to: range.to + marker.length },
        ],
        range: EditorSelection.range(range.from - marker.length, range.to - marker.length),
      };
    }
    return {
      changes: [
        { from: range.from, insert: marker },
        { from: range.to, insert: marker },
      ],
      range: EditorSelection.range(range.from + marker.length, range.to + marker.length),
    };
  });
}

/** Makes the lines of the selection headings of `level` (0 removes the heading). */
export function setHeading(state: EditorState, level: number): TransactionSpec {
  const changes: { from: number; to: number; insert: string }[] = [];
  const seen = new Set<number>();
  for (const range of state.selection.ranges) {
    for (let pos = range.from; pos <= range.to;) {
      const line = state.doc.lineAt(pos);
      if (!seen.has(line.number)) {
        seen.add(line.number);
        const current = /^#{1,6}\s+/.exec(line.text)?.[0] ?? "";
        changes.push({
          from: line.from,
          to: line.from + current.length,
          insert: level > 0 ? `${"#".repeat(level)} ` : "",
        });
      }
      pos = line.to + 1;
    }
  }
  return { changes };
}

/** Inserts text at each cursor (replacing the selection), the cursor after it. */
export function insertText(state: EditorState, text: string): TransactionSpec {
  return state.changeByRange((range) => ({
    changes: { from: range.from, to: range.to, insert: text },
    range: EditorSelection.cursor(range.from + text.length),
  }));
}
