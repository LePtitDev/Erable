import { api, unwrap } from "./client";

const fragmentPattern = /^#session=([0-9A-Fa-f]+)$/;

/**
 * Desktop mode: the shell opens the SPA with `#session=<token>`. A fragment is never sent to the server,
 * so the token stays out of every log. We remove it from the address bar and history, then exchange it once
 * for an HttpOnly cookie that the WebView attaches to every later request (API, WebSocket, images).
 * Does nothing in server mode or after a reload (the cookie is already set).
 */
export async function establishDesktopSession(location: Location = window.location): Promise<void> {
  const token = fragmentPattern.exec(location.hash)?.[1];
  if (token === undefined) {
    return;
  }
  history.replaceState(history.state, "", location.pathname + location.search);
  await unwrap(api.POST("/api/session", { params: { header: { "X-Erable-Session": token } } }));
}
