import { create } from "zustand";
import type { ElementId } from "../../shared/ids/elementId";

export type ShellMode = "read" | "edit";

interface ShellState {
  /** Shown in the status bar (« Lecture » / « Édition »). */
  readonly mode: ShellMode;
  /** Cursor position of the editor, shown in the status bar while editing. */
  readonly cursor: { readonly line: number; readonly column: number } | null;
  /** Validation errors of the page being edited, shown in the status bar (F05.R6). */
  readonly validationErrors: number;
  /** The page being edited, if any: the real-time events leave it alone (M4-03). */
  readonly editingId: ElementId | null;
  /** The element shown by the work area (null on the search, the trash…): told to the others as the presence. */
  readonly viewingId: ElementId | null;
  /** The heading around the cursor of the editor, told to the others while editing. */
  readonly section: string | null;
  setViewing: (id: ElementId | null) => void;
  setSection: (section: string | null) => void;
  setMode: (mode: ShellMode, editingId?: ElementId) => void;
  setValidationErrors: (count: number) => void;
  setCursor: (cursor: { line: number; column: number } | null) => void;
}

/** Transient UI state of the project shell (not persisted). */
export const useShellStore = create<ShellState>()((set) => ({
  mode: "read",
  cursor: null,
  validationErrors: 0,
  editingId: null,
  viewingId: null,
  section: null,
  setViewing: (viewingId) => {
    set({ viewingId });
  },
  setSection: (section) => {
    set({ section });
  },
  setMode: (mode, editingId) => {
    set({
      mode,
      editingId: mode === "edit" ? (editingId ?? null) : null,
      ...(mode === "read" ? { cursor: null, section: null } : {}),
    });
  },
  setValidationErrors: (validationErrors) => {
    set({ validationErrors });
  },
  setCursor: (cursor) => {
    set({ cursor });
  },
}));
