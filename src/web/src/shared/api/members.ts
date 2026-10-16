import { queryOptions, useMutation, useQueryClient } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

export type MemberItem = Rebrand<Schemas["MemberItem"]>;
export type AccountItem = Rebrand<Schemas["AccountItem"]>;
export type ProjectRole = Schemas["MemberItem"]["role"];

/** The members of a project; administrators of the project only (M4-08, F14.R1). */
export function membersQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.members(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/members", {
          params: { path: { projectId } },
          signal,
        }),
      ),
    retry: false,
  });
}

/** Adds an existing account to the project, or changes its role. */
export function useSetMember(projectId: ElementId) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (member: { userId: string; role: ProjectRole }) =>
      unwrap(
        api.PUT("/api/v1/projects/{projectId}/members/{userId}", {
          params: { path: { projectId, userId: member.userId } },
          body: { role: member.role },
        }),
      ),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.members(projectId) }),
  });
}

export function useRemoveMember(projectId: ElementId) {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (userId: string) =>
      unwrap(
        api.DELETE("/api/v1/projects/{projectId}/members/{userId}", {
          params: { path: { projectId, userId } },
        }),
      ),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.members(projectId) }),
  });
}

/** Creates an account (server administrators; no free sign-up in v1). */
export function useCreateAccount() {
  return useMutation({
    mutationFn: (account: { login: string; name: string; password: string }) =>
      unwrap(api.POST("/api/v1/accounts", { body: { ...account, isServerAdmin: false } })),
  });
}
