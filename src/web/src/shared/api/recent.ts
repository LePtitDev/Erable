import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Recently viewed and edited elements (docs/architecture.md §8.1 `me/recent`, F11, M3-04).

export type RecentActivity = Rebrand<Schemas["RecentActivityView"]>;
export type ViewedItem = RecentActivity["viewed"][number];
export type EditedItem = RecentActivity["edited"][number];

/** How long an element must stay on screen to count as viewed (F11.R1). */
export const viewDelayMs = 2000;

/** The user's latest views and the latest changes of the project. */
export function recentQuery(projectId: ElementId, limit = 10) {
  return queryOptions({
    queryKey: [...queryKeys.recent(projectId), limit],
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/me/recent", {
          params: { path: { projectId }, query: { limit } },
          signal,
        }),
      ),
  });
}

export function recordView(projectId: ElementId, elementId: ElementId) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/me/views", {
      params: { path: { projectId } },
      body: { elementId },
    }),
  );
}
