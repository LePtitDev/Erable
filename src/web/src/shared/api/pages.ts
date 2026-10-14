import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

export type PageView = Rebrand<Schemas["PageView"]>;
export type ResolvedItem = Rebrand<Schemas["ResolvedItem"]>;
export type AttachmentItem = Rebrand<Schemas["AttachmentItem"]>;

export function pageQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.page(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/pages/{id}", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

/**
 * Current name, kind, path and state of every ID a page references, in **one** call whatever their number
 * (F04.R1–R2). Keyed under the project so that a rename or a move refreshes every page that shows the name.
 */
export function resolveQuery(projectId: ElementId, ids: readonly ElementId[]) {
  const sorted = [...new Set(ids)].sort();
  return queryOptions({
    queryKey: [...queryKeys.projectContent(projectId), "resolve", sorted.join(",")],
    queryFn: async ({ signal }) => {
      if (sorted.length === 0) return new Map<ElementId, ResolvedItem>();
      const result = await unwrap(
        api.GET("/api/v1/projects/{projectId}/resolve", {
          params: { path: { projectId }, query: { ids: sorted.join(",") } },
          signal,
        }),
      );
      return new Map(result.items.map((item) => [item.id, item]));
    },
    staleTime: 30_000,
  });
}

/** URL of the content of an attachment (Range, ETag, safe Content-Type: docs/architecture.md §9). */
export function attachmentUrl(projectId: ElementId, id: ElementId): string {
  return `/api/v1/projects/${projectId}/attachments/${id}`;
}

/** Markdown and revision of a page, for the editor (the revision goes back in `If-Match`). */
export function pageContentQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.pageContent(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/pages/{id}/content", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
    staleTime: 0,
  });
}

/** Saves the Markdown of a page if it is still at `revision` (optimistic concurrency; 409 otherwise). */
export function savePageContent(
  projectId: ElementId,
  id: ElementId,
  markdown: string,
  revision: number,
) {
  return unwrap(
    api.PUT("/api/v1/projects/{projectId}/pages/{id}/content", {
      params: { path: { projectId, id }, header: { "If-Match": `"${String(revision)}"` } },
      body: { markdown },
    }),
  );
}
