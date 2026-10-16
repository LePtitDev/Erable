import { useQueryClient } from "@tanstack/react-query";
import { useContext, useEffect, useState } from "react";
import { meQuery } from "../../shared/api/projects";
import type { ElementId } from "../../shared/ids/elementId";
import {
  invalidateAll,
  invalidateElementChanged,
  invalidateTreeChanged,
} from "../../shared/realtime/invalidation";
import type { ProjectLink } from "../../shared/realtime/projectConnection";
import { RealtimeContext } from "../../shared/realtime/RealtimeContext";
import { useShellStore } from "./shellStore";

/**
 * Keeps the project in step with what the others do (M4-03, F14.R4): the server announces each write and the query
 * cache refetches what is on screen — no polling. After a lost link, everything reloads. The same link tells the
 * others what this user is doing: the element shown, reading or editing, and the section (M4-04, F14.R3).
 */
export function useProjectEvents(projectId: ElementId): void {
  const client = useQueryClient();
  const connectToProject = useContext(RealtimeContext);
  const [link, setLink] = useState<ProjectLink | null>(null);

  useEffect(() => {
    let cancelled = false;
    let current: ProjectLink | null = null;
    const editing = () => useShellStore.getState().editingId;

    connectToProject(projectId, {
      elementChanged: (event) => {
        const me = client.getQueryData(meQuery.queryKey);
        if (event.elementId === editing() && event.by !== me?.id) {
          // Saved by somebody else while this user edits it: told in the editor; the merge happens at the save.
          useShellStore.getState().setRemoteChange(event.by);
        }
        void invalidateElementChanged(client, projectId, event.elementId, editing());
      },
      elementMoved: (event) => {
        void invalidateElementChanged(client, projectId, event.elementId, editing());
      },
      elementDeleted: (event) => {
        void invalidateElementChanged(client, projectId, event.elementId, editing());
      },
      treeChanged: () => {
        void invalidateTreeChanged(client, projectId, editing());
      },
      resynchronize: () => {
        void invalidateAll(client, projectId, editing());
      },
    })
      .then((started) => {
        if (cancelled) {
          void started.stop();
        } else {
          current = started;
          setLink(started);
        }
      })
      .catch((error: unknown) => {
        console.error("The real-time connection could not be set up", error);
      });

    return () => {
      cancelled = true;
      setLink(null);
      void current?.stop();
    };
  }, [client, connectToProject, projectId]);

  const viewingId = useShellStore((s) => s.viewingId);
  const mode = useShellStore((s) => s.mode);
  const section = useShellStore((s) => s.section);
  useEffect(() => {
    link?.setPresence({ elementId: viewingId, mode, section: mode === "edit" ? section : null });
  }, [link, viewingId, mode, section]);
}
