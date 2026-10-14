import { useQuery } from "@tanstack/react-query";
import { Link, Outlet, useParams, useRouter } from "@tanstack/react-router";
import { ArrowLeft, ArrowRight, Moon, PanelLeft, Search, Sun } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { initialsOf, meQuery, projectQuery } from "../../shared/api/projects";
import { treeQuery } from "../../shared/api/tree";
import { useUiState } from "../../shared/api/uiState";
import type { ElementId } from "../../shared/ids/elementId";
import { useThemeStore } from "../../shared/theme/theme";
import { useSystemDark } from "../../shared/theme/useSystemDark";
import { Logo } from "../../shared/ui/Logo";
import { PreferencesDialog } from "../home/PreferencesDialog";
import { NotFoundScreen } from "./ErrorScreens";
import styles from "./ProjectShell.module.css";
import { Splitter } from "./Splitter";
import { useShellStore } from "./shellStore";
import { defaultLayout, explorerBounds, explorerPanelId, type Layout } from "./layout";

/**
 * Frame shared by every project screen (mockup « Page — lecture »): top bar, explorer, work area, status bar.
 * Panel sizes and visibility are interface state of the user, stored through the API (decision Q7).
 */
export function ProjectShell() {
  const { t } = useTranslation();
  const { projectId } = useParams({ from: "/p/$projectId" });
  const project = useQuery({
    ...projectQuery(projectId ?? ("" as ElementId)),
    enabled: projectId !== null,
  });
  if (projectId === null || project.isError) {
    return <NotFoundScreen project />;
  }
  return (
    <ShellFrame projectId={projectId} projectName={project.data?.name ?? t("common.loading")} />
  );
}

function ShellFrame({
  projectId,
  projectName,
}: {
  readonly projectId: ElementId;
  readonly projectName: string;
}) {
  const { t } = useTranslation();
  const layout = useUiState<Layout>("layout", defaultLayout);
  const [preferences, setPreferences] = useState(false);

  useEffect(() => {
    document.title = `${projectName} — ${t("app.name")}`;
  }, [projectName, t]);

  const { explorerOpen, explorerWidth } = layout.value;
  return (
    <div
      className={styles["shell"]}
      style={{ ["--explorer-width" as string]: `${String(explorerWidth)}px` }}
    >
      <TopBar
        projectId={projectId}
        projectName={projectName}
        explorerOpen={explorerOpen}
        onToggleExplorer={() => {
          layout.update((l) => ({ explorerOpen: !l.explorerOpen }));
        }}
        onPreferences={() => {
          setPreferences(true);
        }}
      />
      <div className={styles["body"]}>
        {explorerOpen && (
          <>
            <nav
              id={explorerPanelId}
              aria-label={t("explorer.label")}
              className={styles["explorer"]}
            >
              <ExplorerSlot />
            </nav>
            <Splitter
              label={t("shell.resizeExplorer")}
              controls={explorerPanelId}
              value={explorerWidth}
              min={explorerBounds.min}
              max={explorerBounds.max}
              onChange={(width) => {
                layout.update({ explorerWidth: width });
              }}
            />
          </>
        )}
        <main className={styles["main"]} id="main">
          <Outlet />
        </main>
      </div>
      <StatusBar projectId={projectId} />
      <PreferencesDialog open={preferences} onOpenChange={setPreferences} />
    </div>
  );
}

/** Replaced by the explorer tree (M1-10). */
function ExplorerSlot() {
  const { t } = useTranslation();
  return <h2 className={styles["explorerTitle"]}>{t("explorer.title")}</h2>;
}

function TopBar({
  projectId,
  projectName,
  explorerOpen,
  onToggleExplorer,
  onPreferences,
}: {
  readonly projectId: ElementId;
  readonly projectName: string;
  readonly explorerOpen: boolean;
  readonly onToggleExplorer: () => void;
  readonly onPreferences: () => void;
}) {
  const { t, i18n } = useTranslation();
  const router = useRouter();
  const me = useQuery(meQuery);
  const preference = useThemeStore((s) => s.preference);
  const setPreference = useThemeStore((s) => s.setPreference);
  const systemDark = useSystemDark();
  const dark = preference === "dark" || (preference === "system" && systemDark);

  return (
    <header className={styles["topBar"]}>
      <Link to="/" className={styles["home"]} aria-label={t("shell.home")}>
        <Logo size={18} />
      </Link>
      <button
        type="button"
        className={styles["iconButton"]}
        aria-label={explorerOpen ? t("shell.hideExplorer") : t("shell.showExplorer")}
        aria-expanded={explorerOpen}
        aria-controls={explorerPanelId}
        onClick={onToggleExplorer}
      >
        <PanelLeft aria-hidden size={18} />
      </button>
      <Link to="/p/$projectId" params={{ projectId }} className={styles["projectName"]}>
        {projectName}
      </Link>
      <div className={styles["history"]}>
        <button
          type="button"
          className={styles["iconButton"]}
          aria-label={t("shell.back")}
          onClick={() => {
            router.history.back();
          }}
        >
          <ArrowLeft aria-hidden size={18} />
        </button>
        <button
          type="button"
          className={styles["iconButton"]}
          aria-label={t("shell.forward")}
          onClick={() => {
            router.history.forward();
          }}
        >
          <ArrowRight aria-hidden size={18} />
        </button>
      </div>
      {/* Quick search arrives with M3-02; the field is shown, inactive. */}
      <div className={styles["search"]} aria-disabled="true" title={t("common.soon")}>
        <Search aria-hidden size={16} />
        <span className={styles["searchText"]}>{t("shell.search")}</span>
        <kbd className={styles["kbd"]}>{t("shell.searchShortcut")}</kbd>
      </div>
      <div className={styles["right"]}>
        {/* Presence avatars arrive with M4-04. */}
        <button
          type="button"
          className={styles["textButton"]}
          onClick={onPreferences}
          aria-label={t("shell.languageNamed", { language: i18n.language.toUpperCase() })}
        >
          {i18n.language.toUpperCase()}
        </button>
        <button
          type="button"
          className={styles["iconButton"]}
          aria-label={t("shell.toggleTheme")}
          onClick={() => {
            setPreference(dark ? "light" : "dark");
          }}
        >
          {dark ? <Sun aria-hidden size={18} /> : <Moon aria-hidden size={18} />}
        </button>
        {me.isSuccess && (
          <span className={styles["avatar"]} title={me.data.name}>
            {initialsOf(me.data.name)}
          </span>
        )}
      </div>
    </header>
  );
}

function StatusBar({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const tree = useQuery(treeQuery(projectId));
  const mode = useShellStore((s) => s.mode);
  const cursor = useShellStore((s) => s.cursor);
  const counts = tree.data?.counts;
  return (
    <footer className={styles["statusBar"]}>
      <span className={styles["connection"]}>
        <span className={styles["dot"]} aria-hidden />
        {t("shell.status.local")}
      </span>
      {counts !== undefined && (
        <span>
          {[
            t("shell.status.pages", { count: counts.pages }),
            t("shell.status.categories", { count: counts.categories }),
            t("shell.status.properties", { count: counts.properties }),
            t("shell.status.links", { count: counts.links }),
          ].join(" · ")}
        </span>
      )}
      {cursor !== null && (
        <span className={styles["cursor"]}>{t("shell.status.cursor", cursor)}</span>
      )}
      <span className={styles["mode"]}>{t(`shell.mode.${mode}`)}</span>
    </footer>
  );
}
