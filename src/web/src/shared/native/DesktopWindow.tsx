import { useEffect } from "react";
import { api, unwrap } from "../api/client";
import { approveClose, useIsDesktop, useWindowStore, windowTitle } from "./native";

/**
 * Keeps the desktop window in step with the SPA: its title follows the current project, the shell knows whether
 * closing would lose changes, and a close request from the shell (server-sent event) reaches the page editor, which
 * asks « Enregistrer / Abandonner / Annuler » (F16.R3, E2E-16-05); a folder dropped on the window reaches the home
 * screen (E2E-16-07). Renders nothing.
 */
export function DesktopWindow() {
  const desktop = useIsDesktop();
  const projectName = useWindowStore((s) => s.projectName);
  const unsavedChanges = useWindowStore((s) => s.unsavedChanges);
  const title = windowTitle(projectName);

  useEffect(() => {
    document.title = title;
  }, [title]);

  useEffect(() => {
    if (!desktop) return;
    void unwrap(api.PUT("/api/v1/native/window", { body: { title, unsavedChanges } })).catch(
      (error: unknown) => {
        console.error("The window state could not be sent to the shell", error);
      },
    );
  }, [desktop, title, unsavedChanges]);

  useEffect(() => {
    if (!desktop) return;
    const events = new EventSource("/api/v1/native/events");
    events.onmessage = (message: MessageEvent<string>) => {
      const event = JSON.parse(message.data) as { type?: string; path?: string | null };
      if (event.type === "folderDropped" && typeof event.path === "string") {
        useWindowStore.getState().dropFolder(event.path);
        return;
      }
      if (event.type !== "closeRequested") return;
      // The changes may have been saved in the meantime: nothing left to ask.
      if (useWindowStore.getState().unsavedChanges) useWindowStore.getState().requestClose();
      else void approveClose();
    };
    return () => {
      events.close();
    };
  }, [desktop]);

  return null;
}
