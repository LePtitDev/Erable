import { useQuery } from "@tanstack/react-query";
import { useParams } from "@tanstack/react-router";
import { meQuery } from "../../shared/api/projects";
import { useProjectServer } from "../../shared/api/servers";
import { parseElementId } from "../../shared/ids/elementId";

/**
 * Who « moi » is on this screen: the user of the machine, or — in a project that lives on a server — the account
 * the application signed in with there (M4-07). Authors, presence and the « modified by someone else » notice compare
 * with it.
 */
export function useCurrentUserId(): string | undefined {
  const me = useQuery(meQuery);
  const params = useParams({ strict: false });
  const raw = typeof params.projectId === "string" ? params.projectId : null;
  const server = useProjectServer(raw === null ? null : parseElementId(raw));
  return server?.userId ?? me.data?.id;
}
