import { useCurrentUserId } from "../project/useCurrentUserId";
import { usePresenceStore, type PresenceUser } from "../../shared/realtime/presenceStore";

/** The other users present in the project (not this screen's user), in name order. */
export function useOtherUsers(): readonly PresenceUser[] {
  const me = useCurrentUserId();
  const users = usePresenceStore((s) => s.users);
  return users.filter((u) => u.userId !== me);
}
