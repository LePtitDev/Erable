import { useQuery } from "@tanstack/react-query";
import { Search } from "lucide-react";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { errorMessage } from "../../shared/api/errors";
import {
  initialsOf,
  recentProjectsQuery,
  useRemoveRecentProject,
  type ProjectSummary,
  type RecentProject,
} from "../../shared/api/projects";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { Button } from "../../shared/ui/Button";
import styles from "./RecentProjects.module.css";
import { RowMenu } from "./RowMenu";
import { useMigratingOpen } from "./useMigratingOpen";

interface RecentProjectsProps {
  readonly onOpened: (project: ProjectSummary, lastElementId?: ElementId | null) => void;
}

/**
 * Recent projects of the user profile (F01.R2): a missing folder stays listed, greyed, with « Retirer ». Every row
 * also has a « ⋯ » menu to take it off the list; the folder itself is never touched.
 */
export function RecentProjects({ onOpened }: RecentProjectsProps) {
  const { t } = useTranslation();
  const format = useFormatters();
  const recent = useQuery(recentProjectsQuery);
  const open = useMigratingOpen();
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
    open.open(project.folder, (summary) => {
      onOpened(summary, project.lastElementId);
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

      {open.error !== null && (
        <p role="alert" className={styles["error"]}>
          {errorMessage(t, open.error)}
        </p>
      )}
      {open.dialog}

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
                      disabled={open.pending}
                    >
                      {project.name}
                    </button>
                  ) : (
                    <span className={styles["name"]}>{project.name}</span>
                  )}
                  <span className={styles["path"]}>{project.folder}</span>
                </span>
                {!available && (
                  <Button
                    size="sm"
                    className={styles["above"]}
                    onClick={() => {
                      remove.mutate(project.projectId);
                    }}
                    aria-label={t("home.recent.removeNamed", { name: project.name })}
                  >
                    {t("home.recent.remove")}
                  </Button>
                )}
                {available ? (
                  <span className={styles["mode"]}>
                    <span className={styles["dot"]} aria-hidden />
                    <span className={styles["label"]}>{t("home.recent.local")}</span>
                  </span>
                ) : (
                  <span className={styles["status"]}>
                    {t(`home.recent.status.${project.status}`)}
                  </span>
                )}
                <time
                  className={styles["when"]}
                  dateTime={project.lastOpenedAt}
                  title={format.dateTime(new Date(project.lastOpenedAt))}
                >
                  {format.relative(new Date(project.lastOpenedAt))}
                </time>
                <RowMenu
                  label={t("home.recent.actions", { name: project.name })}
                  items={[
                    {
                      label: t("home.recent.removeFromList"),
                      onSelect: () => {
                        remove.mutate(project.projectId);
                      },
                    },
                  ]}
                />
              </li>
            );
          })}
        </ul>
      )}
    </section>
  );
}
