import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Deleting elements and the trash (docs/architecture.md §7.4, F12, M2-08).

export type DeleteImpactView = Rebrand<Schemas["DeleteImpactView"]>;
export type TrashEntryView = Rebrand<Schemas["TrashEntryView"]>;
export type RestoreView = Rebrand<Schemas["RestoreView"]>;

/** What deleting an element takes with it: shown before the user confirms (F12.R2). */
export function deleteImpactQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.deleteImpact(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/elements/{id}/impact", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
    // The answer is only valid at the moment of the question.
    staleTime: 0,
    gcTime: 0,
  });
}

/** The deletions in the trash, newest first. */
export function trashQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.trash(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/trash", {
          params: { path: { projectId } },
          signal,
        }),
      ),
  });
}

export function deleteElement(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.DELETE("/api/v1/projects/{projectId}/elements/{id}", {
      params: { path: { projectId, id } },
    }),
  );
}

export function restoreEntry(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/trash/{id}/restore", {
      params: { path: { projectId, id } },
    }),
  );
}

export function purgeEntry(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.DELETE("/api/v1/projects/{projectId}/trash/{id}", { params: { path: { projectId, id } } }),
  );
}

export function emptyTrash(projectId: ElementId) {
  return unwrap(
    api.DELETE("/api/v1/projects/{projectId}/trash", { params: { path: { projectId } } }),
  );
}
