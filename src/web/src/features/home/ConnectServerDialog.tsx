import { useId, useState, type SyntheticEvent } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  incompatibleOf,
  probeServer,
  useConnectServer,
  type ServerItem,
  type ServerProbe,
} from "../../shared/api/servers";
import { Button } from "../../shared/ui/Button";
import { Dialog } from "../../shared/ui/Dialog";
import field from "../../shared/ui/Field.module.css";

interface ConnectServerDialogProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly onConnected: (server: ServerItem) => void;
}

/**
 * « Se connecter à un serveur » (E2E-01-06): the address first, checked with the handshake alone — a server of
 * another version is explained here and nothing else is sent to it (F16.R4, E2E-16-06) — then the account.
 */
export function ConnectServerDialog({ open, onOpenChange, onConnected }: ConnectServerDialogProps) {
  const { t } = useTranslation();
  const ids = { url: useId(), login: useId(), password: useId() };
  const connect = useConnectServer();
  const [url, setUrl] = useState("");
  const [login, setLogin] = useState("");
  const [password, setPassword] = useState("");
  const [probe, setProbe] = useState<ServerProbe | null>(null);
  const [checking, setChecking] = useState(false);
  const [problem, setProblem] = useState<string | null>(null);

  const reset = () => {
    setUrl("");
    setLogin("");
    setPassword("");
    setProbe(null);
    setProblem(null);
    connect.reset();
  };

  const check = (event: SyntheticEvent) => {
    event.preventDefault();
    setChecking(true);
    setProblem(null);
    probeServer(url)
      .then(setProbe)
      .catch((failure: unknown) => {
        setProblem(errorMessage(t, failure));
      })
      .finally(() => {
        setChecking(false);
      });
  };

  const signIn = (event: SyntheticEvent) => {
    event.preventDefault();
    if (probe === null) return;
    connect.mutate(
      { url: probe.url, login, password },
      {
        onSuccess: (server) => {
          reset();
          onConnected(server);
        },
        onError: (failure) => {
          const versions = incompatibleOf(failure);
          // The server changed between the check and the sign-in: back to the explanation.
          if (versions !== null) setProbe({ ...probe, compatible: false, ...versions });
        },
      },
    );
  };

  const close = (next: boolean) => {
    if (!next) reset();
    onOpenChange(next);
  };

  return (
    <Dialog
      open={open}
      onOpenChange={close}
      title={t("servers.connect.title")}
      {...(probe === null ? { description: t("servers.connect.description") } : {})}
    >
      {probe === null ? (
        <form className={field["form"]} onSubmit={check} aria-label={t("servers.connect.title")}>
          <div className={field["field"]}>
            <label htmlFor={ids.url} className={field["label"]}>
              {t("servers.connect.url")}
            </label>
            <input
              id={ids.url}
              className={field["input"]}
              value={url}
              placeholder={t("servers.connect.placeholder")}
              inputMode="url"
              autoCapitalize="none"
              spellCheck={false}
              required
              autoFocus
              onChange={(e) => {
                setUrl(e.target.value);
              }}
            />
          </div>
          {problem !== null && (
            <p role="alert" className={field["error"]}>
              {problem}
            </p>
          )}
          <div className={field["actions"]}>
            <Button
              onClick={() => {
                close(false);
              }}
            >
              {t("common.cancel")}
            </Button>
            <Button type="submit" variant="primary" disabled={checking || url.trim() === ""}>
              {t("servers.connect.next")}
            </Button>
          </div>
        </form>
      ) : !probe.compatible ? (
        <div className={field["form"]}>
          <p role="alert" className={field["error"]}>
            {t("servers.incompatible", {
              host: probe.host,
              serverApi: probe.serverApiVersion,
              serverApp: probe.serverAppVersion,
              appApi: probe.appApiVersion,
              appVersion: probe.appVersion,
            })}
          </p>
          <div className={field["actions"]}>
            <Button
              variant="primary"
              onClick={() => {
                close(false);
              }}
            >
              {t("common.close")}
            </Button>
          </div>
        </div>
      ) : (
        <form className={field["form"]} onSubmit={signIn} aria-label={t("servers.connect.title")}>
          <p className={field["hint"]}>
            {t("servers.connect.reached", { host: probe.host, version: probe.serverAppVersion })}
          </p>
          <div className={field["field"]}>
            <label htmlFor={ids.login} className={field["label"]}>
              {t("auth.login")}
            </label>
            <input
              id={ids.login}
              className={field["input"]}
              value={login}
              autoComplete="username"
              autoCapitalize="none"
              spellCheck={false}
              required
              autoFocus
              onChange={(e) => {
                setLogin(e.target.value);
              }}
            />
          </div>
          <div className={field["field"]}>
            <label htmlFor={ids.password} className={field["label"]}>
              {t("auth.password")}
            </label>
            <input
              id={ids.password}
              className={field["input"]}
              type="password"
              value={password}
              autoComplete="current-password"
              required
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
                setProbe(null);
                connect.reset();
              }}
            >
              {t("servers.connect.back")}
            </Button>
            <Button
              type="submit"
              variant="primary"
              disabled={connect.isPending || login.trim() === ""}
            >
              {t("auth.submit")}
            </Button>
          </div>
        </form>
      )}
    </Dialog>
  );
}
