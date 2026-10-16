import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { ChevronDown, ChevronRight } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import { initialsOf } from "../../shared/api/projects";
import {
  serverProjectsQuery,
  useForgetServer,
  useServers,
  type ServerItem,
} from "../../shared/api/servers";
import { Button } from "../../shared/ui/Button";
import styles from "./RecentProjects.module.css";

/**
 * The servers the application connected to, in the recent list with the mode « Serveur » (F01.R2, E2E-01-06). A
 * server opens on the projects its account belongs to; the token that opens them stays on this side of the
 * application (M4-07).
 */
export function ServersSection({
  expanded,
  onConnect,
}: {
  /** A server to show opened (the one just connected to). */
  readonly expanded: string | null;
  readonly onConnect: (server: ServerItem) => void;
}) {
  const { t } = useTranslation();
  const servers = useServers();
  if (servers === undefined || servers.servers.length === 0) return null;
  return (
    <section aria-labelledby="servers-title" className={styles["section"]}>
      <div className={styles["heading"]}>
        <h2 id="servers-title" className={styles["title"]}>
          {t("servers.title")}
        </h2>
      </div>
      {!servers.tokensSurviveRestart && <p className={styles["empty"]}>{t("servers.noVault")}</p>}
      <ul className={styles["list"]}>
        {servers.servers.map((server) => (
          <ServerRow
            key={server.id}
            server={server}
            initiallyOpen={server.id === expanded}
            onReconnect={onConnect}
          />
        ))}
      </ul>
    </section>
  );
}

function ServerRow({
  server,
  initiallyOpen,
  onReconnect,
}: {
  readonly server: ServerItem;
  readonly initiallyOpen: boolean;
  readonly onReconnect: (server: ServerItem) => void;
}) {
  const { t } = useTranslation();
  const forget = useForgetServer();
  // Open by the user's choice, else open for the server just connected to.
  const [toggled, setToggled] = useState<boolean | null>(null);
  const open = toggled ?? initiallyOpen;
  const projects = useQuery({
    ...serverProjectsQuery(server.id),
    enabled: open && server.connected,
    retry: false,
  });

  return (
    <li className={styles["item"]} style={{ flexDirection: "column", alignItems: "stretch" }}>
      <div
        style={{ display: "flex", alignItems: "center", gap: "var(--space-4)", flexWrap: "wrap" }}
      >
        <span className={styles["initials"]} aria-hidden>
          {initialsOf(server.name)}
        </span>
        <span className={styles["text"]}>
          <button
            type="button"
            className={styles["name"]}
            aria-expanded={open}
            onClick={() => {
              setToggled(!open);
            }}
          >
            {open ? <ChevronDown aria-hidden size={16} /> : <ChevronRight aria-hidden size={16} />}
            {server.name}
          </button>
          <span className={styles["path"]}>
            {server.url}
            {server.userName !== null && ` · ${server.userName}`}
          </span>
        </span>
        <span className={styles["mode"]}>
          <span className={styles["dot"]} aria-hidden />
          {t("servers.mode")}
        </span>
        {!server.connected && (
          <Button
            size="sm"
            onClick={() => {
              onReconnect(server);
            }}
          >
            {t("servers.reconnect")}
          </Button>
        )}
        <Button
          size="sm"
          aria-label={t("servers.forgetNamed", { name: server.name })}
          disabled={forget.isPending}
          onClick={() => {
            forget.mutate(server.id);
          }}
        >
          {t("servers.forget")}
        </Button>
      </div>
      {open && !server.connected && <p className={styles["empty"]}>{t("servers.signedOut")}</p>}
      {open && projects.isError && (
        <p role="alert" className={styles["error"]}>
          {errorMessage(t, projects.error)}
        </p>
      )}
      {open && projects.isSuccess && projects.data.length === 0 && (
        <p className={styles["empty"]}>{t("home.server.empty")}</p>
      )}
      {open && projects.isSuccess && projects.data.length > 0 && (
        <ul className={styles["list"]} aria-label={t("servers.projectsOf", { name: server.name })}>
          {projects.data.map((project) => (
            <li key={project.id} className={styles["item"]}>
              <span className={styles["initials"]} aria-hidden>
                {initialsOf(project.name)}
              </span>
              <span className={styles["text"]}>
                <Link
                  to="/p/$projectId"
                  params={{ projectId: project.id }}
                  className={styles["name"]}
                >
                  {project.name}
                </Link>
                <span className={styles["path"]}>
                  {t("home.server.role", { role: t(`roles.${project.myRole}`) })}
                </span>
              </span>
            </li>
          ))}
        </ul>
      )}
    </li>
  );
}
