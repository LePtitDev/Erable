import { useQuery } from "@tanstack/react-query";
import { useEffect, type ReactNode } from "react";
import { requestRelogin, requestRemoteRelogin } from "../../shared/api/auth";
import { ApiError, setUnauthorizedHandler } from "../../shared/api/client";
import { meQuery } from "../../shared/api/projects";
import { apiInfoQuery } from "../../shared/api/queries";
import { LoginScreen } from "./LoginScreen";
import { ReloginDialog } from "./ReloginDialog";
import { RemoteReloginDialog } from "./RemoteReloginDialog";

/**
 * On a server, nothing shows before the user is signed in. The desktop application has no accounts and
 * renders at once. Once signed in, an expired session asks for the password again over the current screen.
 */
/** Added by the desktop backend to what it forwards to a server: which server an answer comes from. */
const remoteServerHeader = "x-erable-remote-server";

export function AuthGate({ children }: { readonly children: ReactNode }) {
  const info = useQuery(apiInfoQuery);
  const accounts = info.isSuccess && info.data.mode === "server";
  const me = useQuery({ ...meQuery, enabled: accounts, retry: false });
  const signedIn = me.isSuccess;
  const desktop = info.isSuccess && info.data.mode === "desktop";

  // The desktop application has no accounts of its own, but a project of a server answers 401 when its token ended.
  useEffect(() => {
    if (!desktop) return;
    setUnauthorizedHandler((_request, response) => {
      const serverId = response.headers.get(remoteServerHeader);
      return serverId === null ? Promise.resolve(false) : requestRemoteRelogin(serverId);
    });
    return () => {
      setUnauthorizedHandler(null);
    };
  }, [desktop]);

  useEffect(() => {
    if (!accounts || !signedIn) return;
    setUnauthorizedHandler(() => requestRelogin());
    return () => {
      setUnauthorizedHandler(null);
    };
  }, [accounts, signedIn]);

  if (info.isPending) return null;
  // Without the handshake nothing can work: let the screens show their own error.
  if (!accounts) {
    return (
      <>
        {children}
        {desktop && <RemoteReloginDialog />}
      </>
    );
  }
  if (me.isPending) return null;
  if (me.isError && me.error instanceof ApiError && me.error.status === 401) {
    return <LoginScreen />;
  }
  return (
    <>
      {children}
      <ReloginDialog />
    </>
  );
}
