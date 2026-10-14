import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  initialsOf,
  recentProjectsQuery,
  useOpenProject,
  useRemoveRecentProject,
  type ProjectSummary,
  type RecentProject,
} from "../../shared/api/projects";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import styles from "./RecentProjects.module.css";

interface RecentProjectsProps {
  readonly onOpened: (project: ProjectSummary, lastElementId?: ElementId | null) => void;
}

/** Recent projects of the user profile (F01.R2): a missing folder stays listed, greyed, with « Retirer ». */
export function RecentProjects({ onOpened }: RecentProjectsProps) {
  const { t } = useTranslation();
  const format = useFormatters();
  const recent = useQuery(recentProjectsQuery);
  const open = useOpenProject();
  const remove = useRemoveRecentProject();
  const [filter, setFilter] = useState("");

  const needle = filter.trim().toLocaleLowerCase();
  const items = (recent.data ?? []).filter(
    (p) =>
      needle === "" ||
      p.name.toLocaleLowerCase().includes(needle) ||
      p.folder.toLocaleLowerCase().includes(needle),
  );

  const openRecent = (project: RecentProject) => {
    open.mutate(project.folder, {
      onSuccess: (summary) => {
        onOpened(summary, project.lastElementId);
      },
    });
  };

  return (
    <section aria-labelledby="recent-title" className={styles["section"]}>
      <div className={styles["heading"]}>
        <h2 id="recent-title" className={styles["title"]}>
          {t("home.recent.title")}
        </h2>
        <label className={styles["filter"]}>
          <Search aria-hidden size={16} />
          <input
            type="search"
            placeholder={t("home.recent.filter")}
            aria-label={t("home.recent.filter")}
            value={filter}
            onChange={(e) => {
              setFilter(e.target.value);
            }}
          />
        </label>
      </div>

      {open.isError && (
        <p role="alert" className={styles["error"]}>
          {errorMessage(t, open.error)}
        </p>
      )}

      {recent.isSuccess && recent.data.length === 0 && (
        <p className={styles["empty"]}>{t("home.recent.empty")}</p>
      )}

      {items.length > 0 && (
        <ul className={styles["list"]}>
          {items.map((project) => {
            const available = project.status === "project";
            return (
              <li
                key={project.projectId}
                className={`${styles["item"] ?? ""} ${available ? "" : (styles["unavailable"] ?? "")}`}
              >
                <span className={styles["initials"]} aria-hidden>
                  {initialsOf(project.name)}
                </span>
                <span className={styles["text"]}>
                  {available ? (
                    <button
                      type="button"
                      className={styles["name"]}
                      onClick={() => {
                        openRecent(project);
                      }}
                      disabled={open.isPending}
                    >
                      {project.name}
                    </button>
                  ) : (
                    <span className={styles["name"]}>{project.name}</span>
                  )}
                  <span className={styles["path"]}>{project.folder}</span>
                </span>
                {available ? (
                  <span className={styles["mode"]}>
                    <span className={styles["dot"]} aria-hidden />
                    {t("home.recent.local")}
                  </span>
                ) : (
                  <>
                    <span className={styles["status"]}>
                      {t(`home.recent.status.${project.status}`)}
                    </span>
                    <Button
                      size="sm"
                      onClick={() => {
                        remove.mutate(project.projectId);
                      }}
                      aria-label={t("home.recent.removeNamed", { name: project.name })}
                    >
                      {t("home.recent.remove")}
                    </Button>
                  </>
                )}
                <span className={styles["when"]}>
                  {format.relative(new Date(project.lastOpenedAt))}
                </span>
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
