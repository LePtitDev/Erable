import { queryOptions, useMutation, useQueryClient } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, ApiError, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

export type ProjectSummary = Rebrand<Schemas["ProjectSummary"]>;
export type RecentProject = Rebrand<Schemas["RecentProjectItem"]>;
export type FolderInspection = Rebrand<Schemas["FolderInspection"]>;
export type CurrentUser = Rebrand<Schemas["CurrentUserInfo"]>;

export const meQuery = queryOptions({
  queryKey: queryKeys.me,
  queryFn: ({ signal }) => unwrap(api.GET("/api/v1/me", { signal })),
  staleTime: Infinity,
});

/**
 * Changes the name the user is shown under (greeting, authors, presence). Everything else cached may show it as an
 * author, so it is refetched too.
 */
export function useRenameMe() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (name: string) => unwrap(api.PUT("/api/v1/me/name", { body: { name } })),
    onSuccess: (me) => {
      client.setQueryData(queryKeys.me, me);
      return client.invalidateQueries({
        predicate: (query) => query.queryHash !== JSON.stringify(queryKeys.me),
      });
    },
  });
}

export const projectListQuery = queryOptions({
  queryKey: queryKeys.projectList,
  queryFn: ({ signal }) => unwrap(api.GET("/api/v1/projects", { signal })),
});

export const recentProjectsQuery = queryOptions({
  queryKey: queryKeys.recentProjects,
  queryFn: ({ signal }) => unwrap(api.GET("/api/v1/me/recent-projects", { signal })),
});

export function projectQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.project(projectId),
    queryFn: ({ signal }) =>
      unwrap(api.GET("/api/v1/projects/{projectId}", { params: { path: { projectId } }, signal })),
    staleTime: Infinity,
  });
}

/** What a folder is, without writing anything (F01.R1, F01.R3). */
export function inspectFolder(folder: string): Promise<FolderInspection> {
  return unwrap(api.POST("/api/v1/projects/inspect", { body: { folder } }));
}

/** What an older project needs before it opens: the formats involved (F01.R3, M2-09). */
export interface MigrationNeed {
  readonly fromVersion: number;
  readonly toVersion: number;
}

/** The migration the backend asks to confirm, or null when the failure is something else. */
export function migrationRequired(error: unknown): MigrationNeed | null {
  if (!(error instanceof ApiError) || error.problem?.["code"] !== "project.migration.required") {
    return null;
  }
  const { fromVersion, toVersion } = error.problem;
  return typeof fromVersion === "number" && typeof toVersion === "number"
    ? { fromVersion, toVersion }
    : null;
}

export function useOpenProject() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: ({
      folder,
      confirmMigration = false,
    }: {
      folder: string;
      confirmMigration?: boolean;
    }) => unwrap(api.POST("/api/v1/projects/open", { body: { folder, confirmMigration } })),
    onSuccess: (project) => {
      client.setQueryData(queryKeys.project(project.id), project);
      void client.invalidateQueries({ queryKey: queryKeys.recentProjects });
    },
  });
}

export function useCreateProject() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: { folder: string | null; name: string; contentLanguage: string }) =>
      unwrap(api.POST("/api/v1/projects", { body })),
    onSuccess: (project) => {
      client.setQueryData(queryKeys.project(project.id), project);
      void client.invalidateQueries({ queryKey: queryKeys.recentProjects });
      void client.invalidateQueries({ queryKey: queryKeys.projectList });
    },
  });
}

export function useRemoveRecentProject() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (projectId: ElementId) =>
      unwrap(
        api.DELETE("/api/v1/me/recent-projects/{projectId}", { params: { path: { projectId } } }),
      ),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.recentProjects }),
  });
}

/** Remembers the element shown last in a project, to reopen it there (E2E-01-02). */
export function recordLastElement(projectId: ElementId, elementId: ElementId): Promise<unknown> {
  return unwrap(
    api.PUT("/api/v1/me/recent-projects/{projectId}/last-element", {
      params: { path: { projectId } },
      body: { elementId },
    }),
  );
}

/** Initials of a project name for its avatar: « Chroniques d'Aube » → « CA ». */
export function initialsOf(name: string): string {
  const words = name.split(/[\s'’-]+/u).filter((w) => /\p{L}|\p{N}/u.test(w));
  const letters = words.length > 1 ? [words[0], words[words.length - 1]] : [name];
  return letters
    .map((w) => /[\p{L}\p{N}]/u.exec(w ?? "")?.[0] ?? "")
    .join("")
    .toUpperCase()
    .slice(0, 2);
}
