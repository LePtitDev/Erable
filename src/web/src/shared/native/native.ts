import { useQuery } from "@tanstack/react-query";
import { create } from "zustand";
import { api, unwrap } from "../api/client";
import { apiInfoQuery } from "../api/queries";
import type { ElementId } from "../ids/elementId";

/**
 * The desktop shell's few OS features (docs/architecture.md §10, F16), always through the API: the SPA never talks
 * to the OS itself (CLAUDE.md §6.3). Server mode answers 404 `native.unavailable`; the UI hides them there.
 */
export function useIsDesktop(): boolean {
  return useQuery(apiInfoQuery).data?.mode === "desktop";
}

/** The OS folder picker; null when the user cancels. */
export async function pickFolder(title: string): Promise<string | null> {
  const picked = await unwrap(api.POST("/api/v1/native/pick-folder", { body: { title } }));
  return picked.path;
}

export function openAttachmentWithSystem(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/attachments/{id}/open", {
      params: { path: { projectId, id } },
    }),
  );
}

export function revealAttachment(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/attachments/{id}/reveal", {
      params: { path: { projectId, id } },
    }),
  );
}

/** « Chroniques d'Aube — Erable »: the document title in every mode, the window title on the desktop. */
export function windowTitle(projectName: string | null): string {
  return projectName === null ? "Erable" : `${projectName} — Erable`;
}

interface WindowStore {
  /** Name of the project shown, for the window title. */
  readonly projectName: string | null;
  /** A page has changes that closing the window would lose (F16.R3). */
  readonly unsavedChanges: boolean;
  /** Incremented each time the user closes the window while there are unsaved changes. */
  readonly closeRequests: number;
  /** Last folder dropped on the window, numbered so that the same folder dropped twice is seen twice. */
  readonly droppedFolder: { readonly path: string; readonly sequence: number } | null;
  setProjectName: (name: string | null) => void;
  setUnsavedChanges: (value: boolean) => void;
  requestClose: () => void;
  dropFolder: (path: string) => void;
}

/** What the window title and the close confirmation need; synchronized with the shell by `DesktopWindow`. */
export const useWindowStore = create<WindowStore>()((set) => ({
  projectName: null,
  unsavedChanges: false,
  closeRequests: 0,
  droppedFolder: null,
  setProjectName: (projectName) => {
    set({ projectName });
  },
  setUnsavedChanges: (unsavedChanges) => {
    set({ unsavedChanges });
  },
  requestClose: () => {
    set((s) => ({ closeRequests: s.closeRequests + 1 }));
  },
  dropFolder: (path) => {
    set((s) => ({ droppedFolder: { path, sequence: (s.droppedFolder?.sequence ?? 0) + 1 } }));
  },
}));

/** The user has saved or discarded the changes: the window closes for good. */
export function approveClose() {
  return unwrap(api.POST("/api/v1/native/window/close"));
}
