import { useQuery } from "@tanstack/react-query";
import { Link, Outlet, useParams, useRouter } from "@tanstack/react-router";
import { ArrowLeft, ArrowRight, Moon, PanelLeft, Search, Sun } from "lucide-react";
import { useEffect, useState } from "react";
import { useTranslation } from "react-i18next";
import { projectQuery } from "../../shared/api/projects";
import { treeQuery } from "../../shared/api/tree";
import { useUiState } from "../../shared/api/uiState";
import type { ElementId } from "../../shared/ids/elementId";
import { useThemeStore } from "../../shared/theme/theme";
import { useWindowStore } from "../../shared/native/native";
import { useSetTheme } from "../../shared/theme/useSetTheme";
import { useSystemDark } from "../../shared/theme/useSystemDark";
import { Logo } from "../../shared/ui/Logo";
import { useFormatCombo } from "../../shared/keyboard/useFormatCombo";
import { useShortcut, useShortcuts } from "../../shared/keyboard/useShortcuts";
import { QuickSearch } from "../search/QuickSearch";
import { PreferencesDialog } from "../home/PreferencesDialog";
import { Explorer } from "../explorer/Explorer";
import { UserMenu } from "../auth/UserMenu";
import { Notices } from "./Notices";
import { PresenceAvatars } from "../presence/Presence";
import { ApiError } from "../../shared/api/client";
import { apiInfoQuery } from "../../shared/api/queries";
import { useProjectServer } from "../../shared/api/servers";
import { AccessDeniedScreen, NotFoundScreen } from "./ErrorScreens";
import { accessOf, loadingAccess, ProjectAccessContext, useCanEdit } from "./access";
import { useConnectionStore } from "../../shared/realtime/connectionStore";
import { usePresenceStore } from "../../shared/realtime/presenceStore";
import { useProjectEvents } from "./useProjectEvents";
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
  const info = useQuery(apiInfoQuery);
  if (projectId === null || project.isError) {
    // On a server a 404 also means « not yours »: one screen for both, nothing of the project shown (F14.R2).
    const denied =
      info.data?.mode === "server" &&
      project.error instanceof ApiError &&
      project.error.status === 404;
    return denied ? <AccessDeniedScreen /> : <NotFoundScreen project />;
  }
  return (
    <ProjectAccessContext
      value={project.data === undefined ? loadingAccess : accessOf(project.data.myRole)}
    >
      <ShellFrame projectId={projectId} projectName={project.data?.name ?? t("common.loading")} />
    </ProjectAccessContext>
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
  // What the others do shows up without reloading (M4-03).
  useProjectEvents(projectId);
  const [preferences, setPreferences] = useState(false);
  const [quickSearch, setQuickSearch] = useState(false);

  // Ctrl+K (⌘K) by default opens the quick search from anywhere, the editor included (F10.R1, F15.R4).
  useShortcut("quickSearch", () => {
    setQuickSearch(true);
  });

  // Document and window title (DesktopWindow): the project while it is shown.
  const setWindowProject = useWindowStore((s) => s.setProjectName);
  useEffect(() => {
    setWindowProject(projectName);
    return () => {
      setWindowProject(null);
    };
  }, [projectName, setWindowProject]);

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
        onSearch={() => {
          setQuickSearch(true);
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
              <Explorer projectId={projectId} />
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
        <main className={styles["main"]} id="main" data-scroll-restoration-id="main">
          <Outlet />
        </main>
      </div>
      <StatusBar projectId={projectId} />
      <Notices />
      <PreferencesDialog open={preferences} onOpenChange={setPreferences} />
      <QuickSearch projectId={projectId} open={quickSearch} onOpenChange={setQuickSearch} />
    </div>
  );
}

function TopBar({
  projectId,
  projectName,
  explorerOpen,
  onToggleExplorer,
  onPreferences,
  onSearch,
}: {
  readonly projectId: ElementId;
  readonly projectName: string;
  readonly explorerOpen: boolean;
  readonly onToggleExplorer: () => void;
  readonly onPreferences: () => void;
  readonly onSearch: () => void;
}) {
  const { t, i18n } = useTranslation();
  const router = useRouter();
  const preference = useThemeStore((s) => s.preference);
  const setPreference = useSetTheme();
  const systemDark = useSystemDark();
  const dark = preference === "dark" || (preference === "system" && systemDark);
  const quickSearchKey = useShortcuts().quickSearch;
  const formatCombo = useFormatCombo();

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
      <button type="button" className={styles["search"]} onClick={onSearch}>
        <Search aria-hidden size={16} />
        <span className={styles["searchText"]}>{t("shell.search")}</span>
        {quickSearchKey !== "" && (
          <kbd className={styles["kbd"]}>{formatCombo(quickSearchKey)}</kbd>
        )}
      </button>
      <div className={styles["right"]}>
        <PresenceAvatars />
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
        <UserMenu />
      </div>
    </header>
  );
}

/** « Connecté à … · synchronisé » on a server (M4-03); « Local » on the desktop, which has nobody to sync with. */
function ConnectionStatus() {
  const { t } = useTranslation();
  const info = useQuery(apiInfoQuery);
  const { projectId } = useParams({ from: "/p/$projectId" });
  const remote = useProjectServer(projectId);
  const state = useConnectionStore((s) => s.state);
  const editors = usePresenceStore((s) => s.users.filter((u) => u.mode === "edit").length);
  if (info.data?.mode !== "server" && remote === undefined) {
    return (
      <span className={styles["connection"]}>
        <span className={styles["dot"]} aria-hidden />
        {t("shell.status.local")}
      </span>
    );
  }
  const lost = state === "reconnecting" || state === "disconnected";
  return (
    <span
      className={`${styles["connection"] ?? ""} ${lost ? (styles["lost"] ?? "") : ""}`}
      role="status"
      data-connection={state}
    >
      <span className={styles["dot"]} aria-hidden />
      {t(`shell.status.connection.${state}`, { server: remote?.name ?? globalThis.location.host })}
      {state === "connected" &&
        editors > 0 &&
        ` · ${t("shell.status.editors", { count: editors })}`}
    </span>
  );
}

function StatusBar({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const tree = useQuery(treeQuery(projectId));
  const mode = useShellStore((s) => s.mode);
  const cursor = useShellStore((s) => s.cursor);
  const validationErrors = useShellStore((s) => s.validationErrors);
  const canEdit = useCanEdit();
  const counts = tree.data?.counts;
  return (
    <footer className={styles["statusBar"]}>
      <ConnectionStatus />
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
      {mode === "edit" && validationErrors > 0 && (
        <span className={styles["errors"]} role="status">
          {t("shell.status.errors", { count: validationErrors })}
        </span>
      )}
      {!canEdit && <span role="status">{t("shell.status.readOnly")}</span>}
      <span className={styles["mode"]}>{t(`shell.mode.${mode}`)}</span>
    </footer>
  );
}
