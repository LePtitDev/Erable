import { api, unwrap } from "./client";

export interface LaunchOptions {
  /** The desktop shell asked for the scripted smoke journey (`--self-test`). */
  readonly selfTest: boolean;
  /** Folder where the smoke journey creates its project (a temporary folder of the shell). */
  readonly selfTestFolder: string | null;
  /** Project folder given on the command line (`Erable.Desktop <folder>`), opened at startup. */
  readonly openFolder: string | null;
}

export const noLaunchOptions: LaunchOptions = {
  selfTest: false,
  selfTestFolder: null,
  openFolder: null,
};

/**
 * Desktop mode: the shell opens the SPA with `#session=<token>[&open=<folder>][&selftest=1&selftestFolder=…]`. A fragment is never sent to the
 * server, so the token stays out of every log. We remove it from the address bar and history, then exchange it
 * once for an HttpOnly cookie that the WebView attaches to every later request (API, WebSocket, images).
 * Does nothing in server mode or after a reload (the cookie is already set).
 */
export async function establishDesktopSession(
  location: Location = window.location,
): Promise<LaunchOptions> {
  const params = new URLSearchParams(location.hash.slice(1));
  const token = params.get("session");
  if (token === null || !/^[0-9A-Fa-f]+$/.test(token)) {
    return noLaunchOptions;
  }
  history.replaceState(history.state, "", location.pathname + location.search);
  await unwrap(api.POST("/api/session", { params: { header: { "X-Erable-Session": token } } }));
  return {
    selfTest: params.get("selftest") === "1",
    selfTestFolder: params.get("selftestFolder"),
    openFolder: params.get("open"),
  };
}
