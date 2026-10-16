import { useQueryClient } from "@tanstack/react-query";
import { useContext, useEffect } from "react";
import type { ElementId } from "../../shared/ids/elementId";
import {
  invalidateAll,
  invalidateElementChanged,
  invalidateTreeChanged,
} from "../../shared/realtime/invalidation";
import { RealtimeContext } from "../../shared/realtime/RealtimeContext";
import { useShellStore } from "./shellStore";

/**
 * Keeps the project in step with what the others do (M4-03, F14.R4): the server announces each write and the query
 * cache refetches what is on screen — no polling. After a lost link, everything reloads.
 */
export function useProjectEvents(projectId: ElementId): void {
  const client = useQueryClient();
  const connectToProject = useContext(RealtimeContext);

  useEffect(() => {
    let cancelled = false;
    let stop: (() => Promise<void>) | null = null;
    const editing = () => useShellStore.getState().editingId;

    connectToProject(projectId, {
      elementChanged: (event) => {
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
      .then((stopper) => {
        if (cancelled) void stopper();
        else stop = stopper;
      })
      .catch((error: unknown) => {
        console.error("The real-time connection could not be set up", error);
      });

    return () => {
      cancelled = true;
      void stop?.();
    };
  }, [client, connectToProject, projectId]);
}
