import type { DiffRow } from "../../shared/api/history";

/** Unchanged lines kept around a change; the rest of a long unchanged run is folded. */
const context = 2;

/** Rows to show: the changed ones with `context` lines around them, a gap standing for what is folded. */
export function foldRows(rows: readonly DiffRow[]): (DiffRow | null)[] {
  const keep = rows.map((_, i) =>
    rows.slice(Math.max(0, i - context), i + context + 1).some((r) => r.kind !== "unchanged"),
  );
  const shown: (DiffRow | null)[] = [];
  rows.forEach((row, i) => {
    if (keep[i] === true) shown.push(row);
    else if (shown.at(-1) !== null) shown.push(null);
  });
  return shown;
}
