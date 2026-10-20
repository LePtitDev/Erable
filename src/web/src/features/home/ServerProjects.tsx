import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { initialsOf, projectListQuery, recentProjectsQuery } from "../../shared/api/projects";
import { apiInfoQuery } from "../../shared/api/queries";
import styles from "./RecentProjects.module.css";

/**
 * The projects of the server the user is a member of, with their role (F14.R2). Someone who is not a member
 * of a project never sees it listed. Where the local « recent projects » are shown too (a backend that also opens
 * folders), a project already listed there is not repeated.
 */
export function ServerProjects() {
  const { t } = useTranslation();
  const info = useQuery(apiInfoQuery);
  const projects = useQuery(projectListQuery);
  const recent = useQuery({ ...recentProjectsQuery, enabled: info.data?.folderProjects === true });

  const known = new Set((recent.data ?? []).map((p) => p.projectId));
  const items = (projects.data ?? []).filter((p) => !known.has(p.id));

  return (
    <section aria-labelledby="server-projects-title" className={styles["section"]}>
      <div className={styles["heading"]}>
        <h2 id="server-projects-title" className={styles["title"]}>
          {t("home.server.title")}
        </h2>
      </div>
      {projects.isSuccess && projects.data.length === 0 && (
        <p className={styles["empty"]}>{t("home.server.empty")}</p>
      )}
      {items.length > 0 && (
        <ul className={styles["list"]}>
          {items.map((project) => (
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
    </section>
  );
}
