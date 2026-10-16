import { Link, useParams } from "@tanstack/react-router";
import { FileQuestionMark, Lock, TriangleAlert } from "lucide-react";
import { useTranslation } from "react-i18next";
import { parseElementId } from "../../shared/ids/elementId";
import styles from "./ErrorScreens.module.css";

/**
 * « Élément introuvable » (E2E-03-03): a calm page, never a technical error, with a way on. Inside a project the
 * shell (and its search field) stays around it.
 */
export function NotFoundScreen({ project = false }: { readonly project?: boolean }) {
  const { t } = useTranslation();
  const params = useParams({ strict: false });
  const projectId = typeof params.projectId === "string" ? params.projectId : null;
  return (
    <section className={styles["screen"]} aria-labelledby="not-found-title">
      <FileQuestionMark aria-hidden size={40} className={styles["icon"]} />
      <h1 id="not-found-title" className={styles["title"]}>
        {project ? t("notFound.project") : t("notFound.title")}
      </h1>
      <p className={styles["text"]}>
        {project ? t("notFound.projectDescription") : t("notFound.description")}
      </p>
      <div className={styles["actions"]}>
        {!project && projectId !== null && (
          <Link
            to="/p/$projectId"
            params={{ projectId: parseElementId(projectId) }}
            className={styles["primary"]}
          >
            {t("notFound.projectHome")}
          </Link>
        )}
        <Link to="/" className={styles["secondary"]}>
          {t("notFound.home")}
        </Link>
      </div>
      {!project && <p className={styles["hint"]}>{t("notFound.searchHint")}</p>}
    </section>
  );
}

/**
 * « Accès refusé » (E2E-14-04): the project exists or not, the user is not a member of it. The screen never says
 * which, and shows nothing of the project (F14.R2).
 */
export function AccessDeniedScreen() {
  const { t } = useTranslation();
  return (
    <section className={styles["screen"]} aria-labelledby="access-denied-title">
      <Lock aria-hidden size={40} className={styles["icon"]} />
      <h1 id="access-denied-title" className={styles["title"]}>
        {t("accessDenied.title")}
      </h1>
      <p className={styles["text"]}>{t("accessDenied.description")}</p>
      <div className={styles["actions"]}>
        <Link to="/" className={styles["primary"]}>
          {t("notFound.home")}
        </Link>
      </div>
    </section>
  );
}

/** Rendering failure: no stack trace on screen, only a way to recover. */
export function ErrorScreen() {
  const { t } = useTranslation();
  return (
    <section role="alert" className={styles["screen"]}>
      <TriangleAlert aria-hidden size={40} className={styles["icon"]} />
      <h1 className={styles["title"]}>{t("errorScreen.title")}</h1>
      <p className={styles["text"]}>{t("errorScreen.description")}</p>
      <div className={styles["actions"]}>
        <button
          type="button"
          className={styles["primary"]}
          onClick={() => {
            window.location.reload();
          }}
        >
          {t("errorScreen.reload")}
        </button>
        <Link to="/" className={styles["secondary"]}>
          {t("notFound.home")}
        </Link>
      </div>
    </section>
  );
}
