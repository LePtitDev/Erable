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
  categories: (projectId: ElementId) => ["projects", projectId, "categories"] as const,
  category: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "categories", id] as const,
  categoryPages: (projectId: ElementId, id: ElementId, includeSubCategories: boolean) =>
    ["projects", projectId, "categories", id, "pages", includeSubCategories] as const,
  properties: (projectId: ElementId) => ["projects", projectId, "properties"] as const,
  property: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "properties", id] as const,
  trash: (projectId: ElementId) => ["projects", projectId, "trash"] as const,
  deleteImpact: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "impact", id] as const,
  linkTypes: (projectId: ElementId) => ["projects", projectId, "link-types"] as const,
  linkType: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "link-types", id] as const,
  pageLinks: (projectId: ElementId, id: ElementId) =>
    ["projects", projectId, "pages", id, "links"] as const,
  /** Recently viewed (own) and edited (shared) elements; below `projectContent`, so any content change refreshes it. */
  recent: (projectId: ElementId) => ["projects", projectId, "me", "recent"] as const,
  search: (projectId: ElementId, query: unknown) =>
    ["projects", projectId, "search", query] as const,
  /** Everything that shows names or structure of a project: after a rename or a move. */
  projectContent: (projectId: ElementId) => ["projects", projectId] as const,
};
