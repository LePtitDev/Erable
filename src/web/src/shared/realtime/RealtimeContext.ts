import { createContext } from "react";
import { connectToProject } from "./projectConnection";

/** How a project connects to the server's events; tests provide a connection that does nothing. */
export type ConnectToProject = typeof connectToProject;

export const RealtimeContext = createContext<ConnectToProject>(connectToProject);
