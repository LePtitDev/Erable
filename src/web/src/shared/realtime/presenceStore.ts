import { create } from "zustand";
import type { ElementId } from "../ids/elementId";

export type PresenceMode = "read" | "edit";

/** A user present in the project (F14.R3). */
export interface PresenceUser {
  readonly userId: string;
  readonly name: string;
  /** The element they show; null on a screen that is not an element. */
  readonly elementId: ElementId | null;
  readonly mode: PresenceMode;
  /** The heading around their cursor while they edit. */
  readonly section: string | null;
}

interface PresenceStore {
  /** Everybody in the project, this user included, ordered by name. */
  readonly users: readonly PresenceUser[];
}

export const usePresenceStore = create<PresenceStore>(() => ({ users: [] }));

export function setPresenceUsers(users: readonly PresenceUser[]): void {
  usePresenceStore.setState({ users });
}

export function clearPresence(): void {
  usePresenceStore.setState({ users: [] });
}

/** The others (not the user of this screen) who are editing the given element. */
export function editorsOf(
  users: readonly PresenceUser[],
  elementId: ElementId,
  me: string | undefined,
): PresenceUser[] {
  return users.filter((u) => u.userId !== me && u.mode === "edit" && u.elementId === elementId);
}
