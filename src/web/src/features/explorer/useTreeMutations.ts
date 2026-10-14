import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, unwrap } from "../../shared/api/client";
import { queryKeys } from "../../shared/api/keys";
import type { ProjectTree } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { applyMove } from "./treeModel";

interface MoveChange {
  readonly id: ElementId;
  readonly name?: string;
  readonly parentId?: ElementId;
  readonly index?: number | null;
}

/**
 * Rename, move and create from the explorer. Renames and moves are optimistic: the tree changes at once and goes
 * back if the API refuses (the error is then shown). Everything that displays names or paths is refreshed after.
 */
export function useTreeMutations(projectId: ElementId) {
  const client = useQueryClient();
  const treeKey = queryKeys.tree(projectId);

  const refresh = () => client.invalidateQueries({ queryKey: queryKeys.projectContent(projectId) });

  const update = useMutation({
    mutationFn: ({ id, name, parentId, index }: MoveChange) =>
      unwrap(
        api.PATCH("/api/v1/projects/{projectId}/elements/{id}", {
          params: { path: { projectId, id } },
          body: {
            ...(name === undefined ? {} : { name }),
            ...(parentId === undefined ? {} : { parentId }),
            ...(index === undefined || index === null ? {} : { index }),
          },
        }),
      ),
    onMutate: async (change) => {
      await client.cancelQueries({ queryKey: treeKey });
      const previous = client.getQueryData<ProjectTree>(treeKey);
      if (previous !== undefined) {
        client.setQueryData<ProjectTree>(treeKey, {
          ...previous,
          nodes: applyMove(previous.nodes, change.id, {
            ...(change.name === undefined ? {} : { name: change.name }),
            ...(change.parentId === undefined ? {} : { parentId: change.parentId }),
            ...(change.index === undefined ? {} : { index: change.index }),
          }),
        });
      }
      return { previous };
    },
    onError: (_error, _change, context) => {
      if (context?.previous !== undefined) client.setQueryData(treeKey, context.previous);
    },
    onSettled: refresh,
  });

  const create = useMutation({
    mutationFn: ({
      kind,
      parentId,
      name,
    }: {
      kind: "page" | "folder";
      parentId: ElementId;
      name: string;
    }) =>
      kind === "page"
        ? unwrap(
            api.POST("/api/v1/projects/{projectId}/pages", {
              params: { path: { projectId } },
              body: { parentId, name },
            }),
          )
        : unwrap(
            api.POST("/api/v1/projects/{projectId}/folders", {
              params: { path: { projectId } },
              body: { parentId, name },
            }),
          ),
    onSuccess: refresh,
  });

  return { update, create };
}
