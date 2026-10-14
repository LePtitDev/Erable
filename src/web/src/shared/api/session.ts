import { api, unwrap } from "./client";

export interface LaunchOptions {
  /** The desktop shell asked for the scripted smoke journey (`--self-test`). */
  readonly selfTest: boolean;
}

/**
 * Desktop mode: the shell opens the SPA with `#session=<token>[&selftest=1]`. A fragment is never sent to the
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
    return { selfTest: false };
  }
  history.replaceState(history.state, "", location.pathname + location.search);
  await unwrap(api.POST("/api/session", { params: { header: { "X-Erable-Session": token } } }));
  return { selfTest: params.get("selftest") === "1" };
}
