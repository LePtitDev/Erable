import { createContext, useContext } from "react";
import type { Schemas } from "../../shared/api/client";

/** What the signed-in user may do in the open project (F14.R1); the backend enforces it, the interface follows. */
export type ProjectRole = Schemas["ProjectSummary"]["myRole"];

export interface ProjectAccess {
  readonly role: ProjectRole;
  /** Editor or administrator: the edit buttons, the drag and drop of the tree. */
  readonly canEdit: boolean;
  /** Administrator: the members of the project. */
  readonly isAdmin: boolean;
  /** False while the project (hence the role) is loading: the user reads meanwhile. */
  readonly roleKnown: boolean;
}

export function accessOf(role: ProjectRole): ProjectAccess {
  return { role, canEdit: role !== "reader", isAdmin: role === "admin", roleKnown: true };
}

/** Until the role is known the user reads: the edit buttons never flash up for a reader. */
export const loadingAccess: ProjectAccess = { ...accessOf("reader"), roleKnown: false };

/**
 * Provided by the project shell. Outside it (a component rendered alone) the user is an administrator, which is
 * what the desktop application always is.
 */
export const ProjectAccessContext = createContext<ProjectAccess>(accessOf("admin"));

export function useProjectAccess(): ProjectAccess {
  return useContext(ProjectAccessContext);
}

/** False for a reader: nothing offers to change what the server would refuse. */
export function useCanEdit(): boolean {
  return useContext(ProjectAccessContext).canEdit;
}
