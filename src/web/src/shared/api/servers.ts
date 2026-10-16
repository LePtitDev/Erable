import { queryOptions, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, ApiError, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";
import { apiInfoQuery } from "./queries";

export type ServerItem = Rebrand<Schemas["ServerItem"]>;
export type ServerList = Rebrand<Schemas["ServerList"]>;
export type ServerProbe = Rebrand<Schemas["ServerProbe"]>;
export type RemoteProject = Rebrand<Schemas["RemoteProject"]>;

/** The servers the application is connected to (desktop only; the home screen lists them as « Serveur »). */
export const serversQuery = queryOptions({
  queryKey: queryKeys.servers,
  queryFn: ({ signal }) => unwrap(api.GET("/api/v1/servers", { signal })),
});

/** The servers of the desktop application; empty (and no request) on a server. */
export function useServers(): ServerList | undefined {
  const info = useQuery(apiInfoQuery);
  const servers = useQuery({ ...serversQuery, enabled: info.data?.mode === "desktop" });
  return servers.data;
}

/** The server a project lives on, or undefined for a local project and on a server. */
export function useProjectServer(projectId: ElementId | null): ServerItem | undefined {
  const servers = useServers();
  return projectId === null
    ? undefined
    : servers?.servers.find((server) => server.projectIds.includes(projectId));
}

/** The first request to a server, and the only one when it speaks another version (F16.R4). */
export function probeServer(url: string): Promise<ServerProbe> {
  return unwrap(api.POST("/api/v1/servers/probe", { body: { url } }));
}

export function useConnectServer() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (body: { url: string; login: string; password: string }) =>
      unwrap(api.POST("/api/v1/servers/connect", { body })),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.servers }),
  });
}

export function serverProjectsQuery(serverId: string) {
  return queryOptions({
    queryKey: queryKeys.serverProjects(serverId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/servers/{serverId}/projects", { params: { path: { serverId } }, signal }),
      ),
  });
}

export function useForgetServer() {
  const client = useQueryClient();
  return useMutation({
    mutationFn: (serverId: string) =>
      unwrap(api.DELETE("/api/v1/servers/{serverId}", { params: { path: { serverId } } })),
    onSuccess: () => client.invalidateQueries({ queryKey: queryKeys.servers }),
  });
}

/** The versions in presence when a server cannot be used (`server.incompatible`), or null for another failure. */
export interface IncompatibleServer {
  readonly serverAppVersion: string;
  readonly serverApiVersion: string;
  readonly appVersion: string;
  readonly appApiVersion: string;
}

export function incompatibleOf(error: unknown): IncompatibleServer | null {
  if (!(error instanceof ApiError) || error.problem?.["code"] !== "server.incompatible")
    return null;
  const p = error.problem;
  const text = (value: unknown) => (typeof value === "string" ? value : "?");
  return {
    serverAppVersion: text(p["serverAppVersion"]),
    serverApiVersion: text(p["serverApiVersion"]),
    appVersion: text(p["appVersion"]),
    appApiVersion: text(p["appApiVersion"]),
  };
}
