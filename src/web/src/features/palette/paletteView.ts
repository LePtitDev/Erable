import type { ElementId } from "../../shared/ids/elementId";
import type { PaletteState } from "./palette";
import type { PaletteItem, PaletteTab } from "./paletteSearch";

/** The palette state of the editor plus what the popup needs to show it. */
export interface Snapshot extends PaletteState {
  readonly query: string;
  readonly at: { readonly left: number; readonly top: number } | null;
}

export interface PalettePopupProps {
  readonly snapshot: Snapshot;
  readonly tab: PaletteTab;
  readonly results: readonly PaletteItem[];
  readonly active: PaletteItem | null;
  readonly ownQuery: string;
  readonly onOwnQuery: (query: string) => void;
  readonly onTab: (tab: PaletteTab) => void;
  readonly onSelect: (entry: PaletteItem) => void;
  readonly onPick: (embed: boolean, entry?: PaletteItem | null) => boolean;
  readonly onClose: () => void;
  readonly onMove: (delta: number) => void;
}

export const optionId = (id: ElementId) => `palette-option-${id}`;

/** Groups by kind in the order of first appearance (the best result's group first). */
export function groupByKind(
  results: readonly PaletteItem[],
): { kind: PaletteItem["kind"]; items: PaletteItem[] }[] {
  const groups = new Map<PaletteItem["kind"], PaletteItem[]>();
  for (const entry of results) groups.set(entry.kind, [...(groups.get(entry.kind) ?? []), entry]);
  return [...groups].map(([kind, items]) => ({ kind, items }));
}

export function nextTab(
  tabs: readonly PaletteTab[],
  current: PaletteTab,
  delta: number,
): PaletteTab {
  return tabs[(tabs.indexOf(current) + delta + tabs.length) % tabs.length] ?? "all";
}
