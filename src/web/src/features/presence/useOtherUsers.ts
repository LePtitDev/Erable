import { useQuery } from "@tanstack/react-query";
import { meQuery } from "../../shared/api/projects";
import { usePresenceStore, type PresenceUser } from "../../shared/realtime/presenceStore";

/** The other users present in the project (not this screen's user), in name order. */
export function useOtherUsers(): readonly PresenceUser[] {
  const me = useQuery(meQuery);
  const users = usePresenceStore((s) => s.users);
  return users.filter((u) => u.userId !== me.data?.id);
}
