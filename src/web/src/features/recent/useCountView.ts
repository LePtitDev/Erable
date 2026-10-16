import { useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { queryKeys } from "../../shared/api/keys";
import { recordView, viewDelayMs } from "../../shared/api/recent";
import type { ElementId } from "../../shared/ids/elementId";

/**
 * Counts the display of an element as a view once it has stayed on screen for two seconds (F11.R1). Leaving before
 * that cancels it, so that scrolling through the tree or the arrows of the history do not fill the list. Best effort:
 * a failure only means one view is not listed.
 */
export function useCountView(
  projectId: ElementId | null,
  elementId: ElementId | null,
  enabled: boolean,
) {
  const queryClient = useQueryClient();
  useEffect(() => {
    if (!enabled || projectId === null || elementId === null) return;
    const timer = setTimeout(() => {
      recordView(projectId, elementId)
        .then(() => queryClient.invalidateQueries({ queryKey: queryKeys.recent(projectId) }))
        .catch(() => undefined);
    }, viewDelayMs);
    return () => {
      clearTimeout(timer);
    };
  }, [enabled, projectId, elementId, queryClient]);
}
