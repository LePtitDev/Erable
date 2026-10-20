import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Revision history of an element (docs/architecture.md §8.1 `elements/{id}/revisions`, F13).

export type RevisionItem = Rebrand<Schemas["RevisionItem"]>;
export type RevisionDiff = Rebrand<Schemas["RevisionDiffView"]>;
export type DiffRow = RevisionDiff["text"][number];
export type RevisionRestored = Rebrand<Schemas["RevisionRestoredView"]>;

/** The revisions of an element, newest first. */
export function revisionsQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.revisions(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/elements/{id}/revisions", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

/** Two revisions compared (any order: the older one comes first in the answer). */
export function revisionDiffQuery(projectId: ElementId, id: ElementId, from: number, to: number) {
  return queryOptions({
    queryKey: queryKeys.revisionDiff(projectId, id, from, to),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/elements/{id}/revisions/diff", {
          params: { path: { projectId, id }, query: { from, to } },
          signal,
        }),
      ),
  });
}

export function restoreRevision(projectId: ElementId, id: ElementId, revision: number) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/elements/{id}/revisions/{revision}/restore", {
      params: { path: { projectId, id, revision } },
    }),
  );
}
