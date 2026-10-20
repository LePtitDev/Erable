import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Link types and typed links between pages (docs/architecture.md §5.5, F09).

export type LinkTypeItem = Rebrand<Schemas["LinkTypeItem"]>;
export type LinkTypeView = Rebrand<Schemas["LinkTypeView"]>;
export type PageLinkItem = Rebrand<Schemas["PageLinkItem"]>;
export type PageLinksView = Rebrand<Schemas["PageLinksView"]>;
export type BacklinkItem = Rebrand<Schemas["BacklinkItem"]>;

/** Every live link type, with the number of links using it. */
export function linkTypesQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.linkTypes(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/link-types", {
          params: { path: { projectId } },
          signal,
        }),
      ),
    staleTime: 30_000,
  });
}

export function linkTypeQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.linkType(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/link-types/{id}", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

/** Typed links of a page, outgoing and incoming (E2E-09-02, E2E-09-03). */
export function pageLinksQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.pageLinks(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/pages/{id}/links", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

/** Pages whose text mentions the page: the implicit references, listed apart from the typed links (E2E-09-03). */
export function backlinksQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.backlinks(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/elements/{id}/backlinks", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

export function createLinkType(projectId: ElementId, parentId: ElementId, name: string) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/link-types", {
      params: { path: { projectId } },
      body: { parentId, name },
    }),
  );
}

export function updateLinkType(
  projectId: ElementId,
  id: ElementId,
  description: string,
  inverseName: string | null,
) {
  return unwrap(
    api.PUT("/api/v1/projects/{projectId}/link-types/{id}", {
      params: { path: { projectId, id } },
      body: { description, inverseName },
    }),
  );
}

export function createLink(
  projectId: ElementId,
  sourceId: ElementId,
  linkTypeId: ElementId,
  targetId: ElementId,
) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/links", {
      params: { path: { projectId } },
      body: { sourceId, linkTypeId, targetId },
    }),
  );
}

export function deleteLink(projectId: ElementId, id: ElementId) {
  return unwrap(
    api.DELETE("/api/v1/projects/{projectId}/links/{id}", { params: { path: { projectId, id } } }),
  );
}
