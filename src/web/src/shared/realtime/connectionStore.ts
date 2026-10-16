import { create } from "zustand";

/** State of the link with the server: shown in the status bar (« Connecté à … · synchronisé »). */
export type ConnectionState = "connecting" | "connected" | "reconnecting" | "disconnected";

interface ConnectionStore {
  readonly state: ConnectionState;
  /** Set once the first connection is up and after each resynchronisation. */
  readonly syncedAt: number | null;
}

export const useConnectionStore = create<ConnectionStore>(() => ({
  state: "disconnected",
  syncedAt: null,
}));

export function setConnectionState(state: ConnectionState, synced = false): void {
  useConnectionStore.setState((current) => ({
    state,
    syncedAt: synced ? Date.now() : state === "disconnected" ? null : current.syncedAt,
  }));
}
