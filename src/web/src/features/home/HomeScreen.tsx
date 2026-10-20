import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  FolderOpen,
  FolderPlus,
  CircleQuestionMark,
  LogOut,
  Server,
  Settings,
  SquareLibrary,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { apiInfoQuery } from "../../shared/api/queries";
import { meQuery, type ProjectSummary } from "../../shared/api/projects";
import type { ElementId } from "../../shared/ids/elementId";
import { Logo } from "../../shared/ui/Logo";
import { useShortcut } from "../../shared/keyboard/useShortcuts";
import { HelpDialog } from "./HelpDialog";
import styles from "./HomeScreen.module.css";
import { NewProjectDialog } from "./NewProjectDialog";
import { OpenFolderDialog } from "./OpenFolderDialog";
import { PreferencesDialog } from "./PreferencesDialog";
import { useWindowStore } from "../../shared/native/native";
import { RecentProjects } from "./RecentProjects";
import { ConnectServerDialog } from "./ConnectServerDialog";
import { ServersSection } from "./ServersSection";
import { ServerProjects } from "./ServerProjects";
import { signOutAndReload } from "../../shared/api/auth";

type OpenDialog = "new" | "open" | "connect" | "preferences" | "help" | null;

/** Home screen (F01, mockup « Accueil — ouvrir un projet »). */
export function HomeScreen() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const info = useQuery(apiInfoQuery);
  const me = useQuery(meQuery);
  // On a server: the projects are the ones the user is a member of; only a server administrator creates
  // one, and a path of the machine means nothing unless the backend was told to open folders.
  const server = info.data?.mode === "server";
  const folders = info.data?.folderProjects !== false;
  const canCreate = !server || me.data?.isServerAdmin === true;
  const [dialog, setDialog] = useState<OpenDialog>(null);
  // The server just connected to, shown open in the list of servers (E2E-01-06).
  const [connectedServer, setConnectedServer] = useState<string | null>(null);
  const [droppedFolder, setDroppedFolder] = useState<string | null>(null);

  // F16 (E2E-16-07): a folder dropped on the window opens like « Ouvrir un dossier… », drops made elsewhere are not
  // replayed when the home screen shows up later.
  const dropped = useWindowStore((s) => s.droppedFolder);
  const seenDrop = useRef(dropped?.sequence ?? 0);
  useEffect(() => {
    if (dropped === null || dropped.sequence === seenDrop.current) return;
    seenDrop.current = dropped.sequence;
    setDroppedFolder(dropped.path);
    setDialog("open");
  }, [dropped]);

  useShortcut("openFolder", () => {
    setDialog("open");
  });

  const opened = (project: ProjectSummary, elementId?: ElementId | null) => {
    setDialog(null);
    if (elementId) {
      void navigate({
        to: "/p/$projectId/e/$elementId",
        params: { projectId: project.id, elementId },
      });
    } else {
      void navigate({ to: "/p/$projectId", params: { projectId: project.id } });
    }
  };

  const close = (open: boolean) => {
    if (!open) setDialog(null);
  };

  return (
    <div className={styles["home"]}>
      <aside className={styles["aside"]}>
        <div className={styles["brand"]}>
          <span className={styles["logo"]}>
            <Logo />
          </span>
          <span className={styles["brandText"]}>
            <span className={styles["brandName"]}>{t("app.name")}</span>
            {info.isSuccess && (
              <span className={styles["version"]}>
                {t("home.version", { version: info.data.appVersion })} ·{" "}
                {t(`app.mode.${info.data.mode}`)}
              </span>
            )}
          </span>
        </div>
        <nav aria-label={t("home.nav.label")} className={styles["nav"]}>
          <a
            href="/"
            aria-current="page"
            className={`${styles["navItem"] ?? ""} ${styles["navActive"] ?? ""}`}
          >
            <SquareLibrary aria-hidden size={18} />
            {t("home.nav.projects")}
          </a>
        </nav>
        <div className={styles["footer"]}>
          <button
            type="button"
            className={styles["navItem"]}
            onClick={() => {
              setDialog("preferences");
            }}
          >
            <Settings aria-hidden size={18} />
            <span className={styles["navLabel"]}>{t("home.nav.preferences")}</span>
          </button>
          <button
            type="button"
            className={styles["navItem"]}
            onClick={() => {
              setDialog("help");
            }}
          >
            <CircleQuestionMark aria-hidden size={18} />
            <span className={styles["navLabel"]}>{t("home.nav.help")}</span>
          </button>
          {server && (
            <button type="button" className={styles["navItem"]} onClick={signOutAndReload}>
              <LogOut aria-hidden size={18} />
              <span className={styles["navLabel"]}>{t("auth.logout")}</span>
            </button>
          )}
        </div>
      </aside>

      <main className={styles["main"]}>
        <header className={styles["header"]}>
          <h1 className={styles["greeting"]}>
            {me.isSuccess ? t("home.greeting", { name: me.data.name }) : t("app.name")}
          </h1>
          <p className={styles["subtitle"]}>
            {t(server ? "home.subtitleServer" : "home.subtitle")}
          </p>
        </header>

        <div className={styles["actions"]}>
          {canCreate && (
            <button
              type="button"
              className={`${styles["action"] ?? ""} ${styles["actionPrimary"] ?? ""}`}
              onClick={() => {
                setDialog("new");
              }}
            >
              <FolderPlus aria-hidden size={22} />
              <span className={styles["actionText"]}>
                <span className={styles["actionTitle"]}>{t("home.new.title")}</span>
                <span className={styles["actionHint"]}>{t("home.new.hint")}</span>
              </span>
            </button>
          )}
          {folders && (
            <button
              type="button"
              className={styles["action"]}
              onClick={() => {
                setDialog("open");
              }}
              aria-keyshortcuts="Control+O"
            >
              <FolderOpen aria-hidden size={22} />
              <span className={styles["actionText"]}>
                <span className={styles["actionTitle"]}>{t("home.open.title")}</span>
                <span className={styles["actionHint"]}>{t("home.open.hint")}</span>
              </span>
            </button>
          )}
          {!server && (
            <button
              type="button"
              className={styles["action"]}
              onClick={() => {
                setDialog("connect");
              }}
            >
              <Server aria-hidden size={22} />
              <span className={styles["actionText"]}>
                <span className={styles["actionTitle"]}>{t("home.connect.title")}</span>
                <span className={styles["actionHint"]}>{t("home.connect.hint")}</span>
              </span>
            </button>
          )}
        </div>

        {server && <ServerProjects />}
        {!server && (
          <ServersSection
            expanded={connectedServer}
            onConnect={() => {
              setDialog("connect");
            }}
          />
        )}
        {folders && <RecentProjects onOpened={opened} />}

        {folders && (
          <div className={styles["dropZone"]}>
            <FolderOpen aria-hidden size={20} />
            {t("home.dropZone")}
          </div>
        )}
      </main>

      <NewProjectDialog open={dialog === "new"} onOpenChange={close} onCreated={opened} />
      <OpenFolderDialog
        key={droppedFolder ?? "picker"}
        open={folders && dialog === "open"}
        onOpenChange={(open) => {
          if (!open) setDroppedFolder(null);
          close(open);
        }}
        onOpened={opened}
        initialFolder={droppedFolder}
      />
      <ConnectServerDialog
        open={dialog === "connect"}
        onOpenChange={close}
        onConnected={(connected) => {
          setConnectedServer(connected.id);
          setDialog(null);
        }}
      />
      <PreferencesDialog open={dialog === "preferences"} onOpenChange={close} />
      <HelpDialog open={dialog === "help"} onOpenChange={close} />
    </div>
  );
}
