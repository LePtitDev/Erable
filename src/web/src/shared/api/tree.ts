import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

export type ProjectTree = Rebrand<Schemas["ProjectTree"]>;
export type TreeNodeItem = Rebrand<Schemas["TreeNodeItem"]>;
export type ElementDetails = Rebrand<Schemas["ElementDetails"]>;
export type ElementKind = Schemas["ElementKind"];
export type PathItem = Rebrand<Schemas["PathItem"]>;

/** The whole explorer tree, loaded once and kept in cache (docs/architecture.md §8.1). */
export function treeQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.tree(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/tree", { params: { path: { projectId } }, signal }),
      ),
    staleTime: 30_000,
  });
}

export function elementQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.element(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/elements/{id}", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}
