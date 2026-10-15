import { useQuery } from "@tanstack/react-query";
import { useNavigate } from "@tanstack/react-router";
import {
  FolderOpen,
  FolderPlus,
  CircleQuestionMark,
  LayoutTemplate,
  Server,
  Settings,
  SquareLibrary,
} from "lucide-react";
import { useEffect, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { apiInfoQuery } from "../../shared/api/queries";
import { meQuery, type ProjectSummary } from "../../shared/api/projects";
import type { ElementId } from "../../shared/ids/elementId";
import { LanguageSwitcher } from "../../shared/i18n/LanguageSwitcher";
import { ThemeSwitcher } from "../../shared/theme/ThemeSwitcher";
import { Logo } from "../../shared/ui/Logo";
import { HelpDialog } from "./HelpDialog";
import styles from "./HomeScreen.module.css";
import { NewProjectDialog } from "./NewProjectDialog";
import { OpenFolderDialog } from "./OpenFolderDialog";
import { PreferencesDialog } from "./PreferencesDialog";
import { useWindowStore } from "../../shared/native/native";
import { RecentProjects } from "./RecentProjects";

type OpenDialog = "new" | "open" | "preferences" | "help" | null;

/** Home screen (F01, mockup « Accueil — ouvrir un projet »). */
export function HomeScreen() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const info = useQuery(apiInfoQuery);
  const me = useQuery(meQuery);
  const [dialog, setDialog] = useState<OpenDialog>(null);
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

  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (
        (event.ctrlKey || event.metaKey) &&
        !event.shiftKey &&
        !event.altKey &&
        event.key.toLowerCase() === "o"
      ) {
        event.preventDefault();
        setDialog("open");
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => {
      window.removeEventListener("keydown", onKeyDown);
    };
  }, []);

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
          <button
            type="button"
            className={styles["navItem"]}
            disabled
            aria-describedby="templates-soon"
          >
            <LayoutTemplate aria-hidden size={18} />
            {t("home.nav.templates")}
            <span id="templates-soon" className={styles["soon"]}>
              {t("common.soon")}
            </span>
          </button>
          <button
            type="button"
            className={styles["navItem"]}
            onClick={() => {
              setDialog("preferences");
            }}
          >
            <Settings aria-hidden size={18} />
            {t("home.nav.preferences")}
          </button>
          <button
            type="button"
            className={styles["navItem"]}
            onClick={() => {
              setDialog("help");
            }}
          >
            <CircleQuestionMark aria-hidden size={18} />
            {t("home.nav.help")}
          </button>
        </nav>
        <div className={styles["settings"]}>
          <LanguageSwitcher />
          <ThemeSwitcher />
        </div>
      </aside>

      <main className={styles["main"]}>
        <header className={styles["header"]}>
          <h1 className={styles["greeting"]}>
            {me.isSuccess ? t("home.greeting", { name: me.data.name }) : t("app.name")}
          </h1>
          <p className={styles["subtitle"]}>{t("home.subtitle")}</p>
        </header>

        <div className={styles["actions"]}>
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
          <button
            type="button"
            className={styles["action"]}
            disabled
            aria-describedby="server-soon"
          >
            <Server aria-hidden size={22} />
            <span className={styles["actionText"]}>
              <span className={styles["actionTitle"]}>{t("home.connect.title")}</span>
              <span className={styles["actionHint"]} id="server-soon">
                {t("home.connect.hint")} · {t("common.soon")}
              </span>
            </span>
          </button>
        </div>

        <RecentProjects onOpened={opened} />

        <div className={styles["dropZone"]}>
          <FolderOpen aria-hidden size={20} />
          {t("home.dropZone")}
        </div>
      </main>

      <NewProjectDialog open={dialog === "new"} onOpenChange={close} onCreated={opened} />
      <OpenFolderDialog
        key={droppedFolder ?? "picker"}
        open={dialog === "open"}
        onOpenChange={(open) => {
          if (!open) setDroppedFolder(null);
          close(open);
        }}
        onOpened={opened}
        initialFolder={droppedFolder}
      />
      <PreferencesDialog open={dialog === "preferences"} onOpenChange={close} />
      <HelpDialog open={dialog === "help"} onOpenChange={close} />
    </div>
  );
}
