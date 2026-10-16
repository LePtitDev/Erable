import { useQuery } from "@tanstack/react-query";
import { useEffect, type ReactNode } from "react";
import { requestRelogin } from "../../shared/api/auth";
import { ApiError, setUnauthorizedHandler } from "../../shared/api/client";
import { meQuery } from "../../shared/api/projects";
import { apiInfoQuery } from "../../shared/api/queries";
import { LoginScreen } from "./LoginScreen";
import { ReloginDialog } from "./ReloginDialog";

/**
 * On a server, nothing shows before the user is signed in (M4-01). The desktop application has no accounts and
 * renders at once. Once signed in, an expired session asks for the password again over the current screen.
 */
export function AuthGate({ children }: { readonly children: ReactNode }) {
  const info = useQuery(apiInfoQuery);
  const accounts = info.isSuccess && info.data.mode === "server";
  const me = useQuery({ ...meQuery, enabled: accounts, retry: false });
  const signedIn = me.isSuccess;

  useEffect(() => {
    if (!accounts || !signedIn) return;
    setUnauthorizedHandler(() => requestRelogin());
    return () => {
      setUnauthorizedHandler(null);
    };
  }, [accounts, signedIn]);

  if (info.isPending) return null;
  // Without the handshake nothing can work: let the screens show their own error.
  if (!accounts) return children;
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
