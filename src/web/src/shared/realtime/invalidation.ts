import type { QueryClient, QueryKey } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { queryKeys } from "../api/keys";

/**
 * What an event of the server makes stale in the query cache (M4-03). Queries are keyed under
 * `["projects", projectId, …]` (see {@link queryKeys}); only the ones on screen refetch, so over-invalidating costs
 * a few requests, never a lost edit.
 */

/** The query belongs to the element with this ID (its page, content, revisions, links, backlinks…). */
function concernsElement(key: QueryKey, id: ElementId): boolean {
  return (key[2] === "pages" || key[2] === "elements") && key[3] === id;
}

function isTree(key: QueryKey): boolean {
  return key[2] === "tree";
}

/**
 * An element changed: everything that may show it refreshes — except the tree, whose names and shape did not move,
 * and except the element the user is editing: the editor keeps its draft, and the conflict (if any) is settled when
 * it saves (M4-05).
 */
export function invalidateElementChanged(
  client: QueryClient,
  projectId: ElementId,
  elementId: ElementId,
  editingId: ElementId | null,
): Promise<void> {
  return client.invalidateQueries({
    queryKey: queryKeys.projectContent(projectId),
    predicate: (query) =>
      !isTree(query.queryKey) &&
      !(editingId === elementId && concernsElement(query.queryKey, elementId)),
  });
}

/** The tree changed (creation, rename, move, deletion, restoration…): names and paths everywhere are suspect. */
export function invalidateTreeChanged(
  client: QueryClient,
  projectId: ElementId,
  editingId: ElementId | null,
): Promise<void> {
  return client.invalidateQueries({
    queryKey: queryKeys.projectContent(projectId),
    predicate: (query) => editingId === null || !concernsElement(query.queryKey, editingId),
  });
}

/** After a reconnection: events may have been missed, everything of the project reloads. */
export function invalidateAll(
  client: QueryClient,
  projectId: ElementId,
  editingId: ElementId | null,
): Promise<void> {
  return invalidateTreeChanged(client, projectId, editingId);
}
