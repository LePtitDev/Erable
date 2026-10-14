import { Link, useParams } from "@tanstack/react-router";
import { FileQuestionMark, TriangleAlert } from "lucide-react";
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
