import type { ElementId } from "../ids/elementId";

/**
 * Query keys of every server resource, in one place: the invalidation rules (and, with M4-03, the SignalR events)
 * target resources through these functions, never through ad-hoc arrays.
 */
export const queryKeys = {
  me: ["me"] as const,
  preferences: ["me", "preferences"] as const,
  recentProjects: ["me", "recent-projects"] as const,
  project: (projectId: ElementId) => ["projects", projectId] as const,
  tree: (projectId: ElementId) => ["projects", projectId, "tree"] as const,
  element: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "elements", id] as const,
  page: (projectId: ElementId, id: ElementId) => ["projects", projectId, "pages", id] as const,
  pageContent: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "pages", id, "content"] as const,
  resolve: (projectId: ElementId, id: ElementId) => ["projects", projectId, "resolve", id] as const,
  backlinks: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "backlinks", id] as const,
  /** Everything that shows names or structure of a project: after a rename or a move. */
  projectContent: (projectId: ElementId) => ["projects", projectId] as const,
};
