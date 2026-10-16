import { create } from "zustand";
import { api, unwrap, type Rebrand, type Schemas } from "./client";

export type SessionResult = Rebrand<Schemas["SessionResult"]>;

/** Opens a session: the backend answers with an HttpOnly cookie, never readable by scripts (M4-01). */
export function login(loginName: string, password: string): Promise<SessionResult> {
  return unwrap(
    api.POST("/api/v1/auth/login", { body: { login: loginName, password, issueToken: false } }),
  );
}

/** Ends the session on the server: the cookie is dead even if someone kept a copy (E2E-14-01). */
export async function logout(): Promise<void> {
  await unwrap(api.POST("/api/v1/auth/logout"));
}

interface AuthState {
  /** The session expired while the user was working: the sign-in dialog is shown over the screen. */
  readonly reloginOpen: boolean;
}

export const useAuthStore = create<AuthState>(() => ({ reloginOpen: false }));

let waiting: { promise: Promise<boolean>; settle: (signedIn: boolean) => void } | null = null;

/** Shows the sign-in dialog and waits for it; several failed calls share one dialog. */
export function requestRelogin(): Promise<boolean> {
  if (waiting === null) {
    let settle: (signedIn: boolean) => void = () => undefined;
    const promise = new Promise<boolean>((resolve) => {
      settle = resolve;
    });
    waiting = { promise, settle };
    useAuthStore.setState({ reloginOpen: true });
  }
  return waiting.promise;
}

/** Closes the sign-in dialog; the calls that waited are replayed when `signedIn`. */
export function finishRelogin(signedIn: boolean): void {
  useAuthStore.setState({ reloginOpen: false });
  waiting?.settle(signedIn);
  waiting = null;
}
