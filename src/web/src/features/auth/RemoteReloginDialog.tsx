import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { finishRemoteRelogin, useAuthStore } from "../../shared/api/auth";
import { errorMessage } from "../../shared/api/errors";
import { useConnectServer, useServers } from "../../shared/api/servers";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";

/**
 * The desktop application worked on a project of a server and its token ended: the password of the same
 * account is asked over the screen, and the request that failed is replayed — nothing the user wrote is lost, as for
 * a session that expires on a server (E2E-14-09).
 */
export function RemoteReloginDialog() {
  const { t } = useTranslation();
  const serverId = useAuthStore((s) => s.remoteRelogin);
  const server = useServers()?.servers.find((s) => s.id === serverId);
  const connect = useConnectServer();
  const passwordId = useId();
  const [password, setPassword] = useState("");

  const submit = (event: SyntheticEvent) => {
    event.preventDefault();
    if (server?.userId === undefined || server.userId === null) return;
    connect.mutate(
      { url: server.url, login: server.userId, password },
      {
        onSuccess: () => {
          setPassword("");
          finishRemoteRelogin(true);
        },
      },
    );
  };

  return (
    <Dialog
      open={serverId !== null && server !== undefined}
      onOpenChange={(open) => {
        if (!open) finishRemoteRelogin(false);
      }}
      title={t("servers.expired.title", { name: server?.name ?? "" })}
      description={t("servers.expired.description", {
        user: server?.userName ?? server?.userId ?? "",
      })}
      size="sm"
    >
      <form
        className={field["form"]}
        onSubmit={submit}
        aria-label={t("servers.expired.title", { name: server?.name ?? "" })}
      >
        <div className={field["field"]}>
          <label htmlFor={passwordId} className={field["label"]}>
            {t("auth.password")}
          </label>
          <input
            id={passwordId}
            className={field["input"]}
            type="password"
            value={password}
            autoComplete="current-password"
            required
            autoFocus
            onChange={(e) => {
              setPassword(e.target.value);
            }}
          />
        </div>
        {connect.isError && (
          <p role="alert" className={field["error"]}>
            {errorMessage(t, connect.error)}
          </p>
        )}
        <div className={field["actions"]}>
          <Button
            onClick={() => {
              finishRemoteRelogin(false);
            }}
          >
            {t("common.cancel")}
          </Button>
          <Button type="submit" variant="primary" disabled={connect.isPending}>
            {t("auth.submit")}
          </Button>
        </div>
      </form>
    </Dialog>
  );
}
