import { create } from "zustand";

export type ShellMode = "read" | "edit";

interface ShellState {
  /** Shown in the status bar (« Lecture » / « Édition »). */
  readonly mode: ShellMode;
  /** Cursor position of the editor, shown in the status bar while editing. */
  readonly cursor: { readonly line: number; readonly column: number } | null;
  /** Validation errors of the page being edited, shown in the status bar (F05.R6). */
  readonly validationErrors: number;
  setMode: (mode: ShellMode) => void;
  setValidationErrors: (count: number) => void;
  setCursor: (cursor: { line: number; column: number } | null) => void;
}

/** Transient UI state of the project shell (not persisted). */
export const useShellStore = create<ShellState>()((set) => ({
  mode: "read",
  cursor: null,
  validationErrors: 0,
  setMode: (mode) => {
    set({ mode, ...(mode === "read" ? { cursor: null } : {}) });
  },
  setValidationErrors: (validationErrors) => {
    set({ validationErrors });
  },
  setCursor: (cursor) => {
    set({ cursor });
  },
}));
