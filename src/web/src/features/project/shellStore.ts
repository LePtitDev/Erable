import { create } from "zustand";

export type ShellMode = "read" | "edit";

interface ShellState {
  /** Shown in the status bar (« Lecture » / « Édition »). */
  readonly mode: ShellMode;
  /** Cursor position of the editor, shown in the status bar while editing. */
  readonly cursor: { readonly line: number; readonly column: number } | null;
  setMode: (mode: ShellMode) => void;
  setCursor: (cursor: { line: number; column: number } | null) => void;
}

/** Transient UI state of the project shell (not persisted). */
export const useShellStore = create<ShellState>()((set) => ({
  mode: "read",
  cursor: null,
  setMode: (mode) => {
    set({ mode, ...(mode === "read" ? { cursor: null } : {}) });
  },
  setCursor: (cursor) => {
    set({ cursor });
  },
}));
