import { parseElementId, type ElementId } from "../ids/elementId";
import { setConnectionState } from "./connectionStore";
import {
  clearPresence,
  setPresenceUsers,
  type PresenceMode,
  type PresenceUser,
} from "./presenceStore";

/** The events of a project (docs/architecture.md §8.2), as the server sends them. */
export interface ElementChangedEvent {
  readonly projectId: ElementId;
  readonly elementId: ElementId;
  readonly revision: number | null;
  readonly by: string;
}

export interface ElementMovedEvent {
  readonly projectId: ElementId;
  readonly elementId: ElementId;
  readonly by: string;
}

export interface ElementDeletedEvent {
  readonly projectId: ElementId;
  readonly elementId: ElementId;
  readonly by: string;
}

export interface TreeChangedEvent {
  readonly projectId: ElementId;
  readonly by: string;
}

export interface ProjectEventHandlers {
  readonly elementChanged: (event: ElementChangedEvent) => void;
  readonly elementMoved: (event: ElementMovedEvent) => void;
  readonly elementDeleted: (event: ElementDeletedEvent) => void;
  readonly treeChanged: (event: TreeChangedEvent) => void;
  /** The link came back after a loss: events may have been missed, everything shown must be reloaded. */
  readonly resynchronize: () => void;
}

/** What this client is doing, told to the others (F14.R3). */
export interface PresenceReport {
  /** The element shown; null on a screen that is not an element (search, trash…). */
  readonly elementId: ElementId | null;
  readonly mode: PresenceMode;
  /** The heading around the cursor while editing. */
  readonly section: string | null;
}

/** The running connection of a project. */
export interface ProjectLink {
  /** Tells the others what this client is doing; sent at once when the link is up, and again after a reconnection. */
  readonly setPresence: (report: PresenceReport) => void;
  readonly stop: () => Promise<void>;
}

/** Retry after 0, 1, 2, 4, 8 s, then every 10 s, forever: a laptop that sleeps comes back by itself. */
export function retryDelay(previousRetryCount: number): number {
  return previousRetryCount === 0 ? 0 : Math.min(1000 * 2 ** (previousRetryCount - 1), 10_000);
}

/** An event as it travels: IDs are plain strings. */
interface Wire {
  readonly projectId: string;
  readonly elementId?: string;
  readonly revision?: number | null;
  readonly by: string;
}

interface WirePresenceUser {
  readonly userId: string;
  readonly name: string;
  readonly elementId: string | null;
  readonly mode: PresenceMode;
  readonly section: string | null;
}

interface WireJoinResult {
  readonly joined: boolean;
  readonly presence: readonly WirePresenceUser[];
}

function toPresence(users: readonly WirePresenceUser[]): PresenceUser[] {
  return users.map((user) => ({
    userId: user.userId,
    name: user.name,
    elementId: user.elementId === null ? null : parseElementId(user.elementId),
    mode: user.mode,
    section: user.section,
  }));
}

/**
 * Connects to `/hubs/project` and joins the project (M4-03). The SignalR client is loaded on demand: it is not part
 * of the initial bundle.
 */
export async function connectToProject(
  projectId: ElementId,
  handlers: ProjectEventHandlers,
): Promise<ProjectLink> {
  setConnectionState("connecting");
  const { HubConnectionBuilder, HubConnectionState, LogLevel } = await import("@microsoft/signalr");
  const connection = new HubConnectionBuilder()
    .withUrl("/hubs/project")
    .withAutomaticReconnect({
      nextRetryDelayInMilliseconds: (context) => retryDelay(context.previousRetryCount),
    })
    .withKeepAliveInterval(5000)
    .configureLogging(LogLevel.Warning)
    .build();

  // What comes over the wire is untyped: IDs go through the one parser before anything uses them.
  const ids = (event: Wire) => {
    const project = parseElementId(event.projectId);
    const element = event.elementId === undefined ? null : parseElementId(event.elementId);
    return project === null ? null : { project, element };
  };
  connection.on("ElementChanged", (event: Wire) => {
    const parsed = ids(event);
    if (parsed?.element) {
      handlers.elementChanged({
        projectId: parsed.project,
        elementId: parsed.element,
        revision: event.revision ?? null,
        by: event.by,
      });
    }
  });
  connection.on("ElementMoved", (event: Wire) => {
    const parsed = ids(event);
    if (parsed?.element) {
      handlers.elementMoved({ projectId: parsed.project, elementId: parsed.element, by: event.by });
    }
  });
  connection.on("ElementDeleted", (event: Wire) => {
    const parsed = ids(event);
    if (parsed?.element) {
      handlers.elementDeleted({
        projectId: parsed.project,
        elementId: parsed.element,
        by: event.by,
      });
    }
  });
  connection.on("TreeChanged", (event: Wire) => {
    const parsed = ids(event);
    if (parsed !== null) handlers.treeChanged({ projectId: parsed.project, by: event.by });
  });
  connection.on(
    "PresenceChanged",
    (event: { projectId: string; users: readonly WirePresenceUser[] }) => {
      if (parseElementId(event.projectId) === projectId) setPresenceUsers(toPresence(event.users));
    },
  );

  let stopped = false;
  let ready = false;
  let wanted: PresenceReport | null = null;

  const sendPresence = () => {
    if (!ready || wanted === null) return;
    connection
      .invoke("SetPresence", projectId, wanted.elementId, wanted.mode, wanted.section)
      .catch(() => undefined);
  };

  /** Joins the project (a new connection belongs to no group) and tells where this client is. */
  const join = async (): Promise<boolean> => {
    const result = await connection.invoke<WireJoinResult>("JoinProject", projectId);
    if (result.joined) {
      ready = true;
      setPresenceUsers(toPresence(result.presence));
      sendPresence();
    }
    return result.joined;
  };

  connection.onreconnecting(() => {
    ready = false;
    setConnectionState("reconnecting");
  });
  connection.onreconnected(() => {
    join()
      .then((joined) => {
        setConnectionState(joined ? "connected" : "disconnected", joined);
        if (joined) handlers.resynchronize();
      })
      .catch(() => {
        setConnectionState("reconnecting");
      });
  });
  connection.onclose(() => {
    ready = false;
    clearPresence();
    setConnectionState("disconnected");
  });

  try {
    await connection.start();
    setConnectionState((await join()) ? "connected" : "disconnected", true);
  } catch {
    // The server is not reachable yet: keep trying in the background, like after a loss.
    setConnectionState("reconnecting");
    void retryStart(
      connection,
      () => connection.state === HubConnectionState.Disconnected,
      join,
      handlers,
      () => stopped,
    );
  }

  return {
    setPresence: (report) => {
      wanted = report;
      sendPresence();
    },
    stop: async () => {
      stopped = true;
      ready = false;
      clearPresence();
      setConnectionState("disconnected");
      await connection.stop();
    },
  };
}

interface Startable {
  start: () => Promise<void>;
}

/** The first connection failed: the automatic reconnection only covers a link that was up. */
async function retryStart(
  connection: Startable,
  down: () => boolean,
  join: () => Promise<boolean>,
  handlers: ProjectEventHandlers,
  stopped: () => boolean,
): Promise<void> {
  for (let attempt = 1; !stopped() && down(); attempt++) {
    await new Promise((resolve) => setTimeout(resolve, retryDelay(attempt)));
    if (stopped() || !down()) return;
    try {
      await connection.start();
      const joined = await join();
      setConnectionState(joined ? "connected" : "disconnected", joined);
      if (joined) handlers.resynchronize();
      return;
    } catch {
      setConnectionState("reconnecting");
    }
  }
}
