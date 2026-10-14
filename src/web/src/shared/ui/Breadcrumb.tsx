import { Link } from "@tanstack/react-router";
import { Fragment } from "react";
import { useTranslation } from "react-i18next";
import type { PathItem } from "../api/tree";
import type { ElementId } from "../ids/elementId";
import styles from "./Breadcrumb.module.css";

interface BreadcrumbProps {
  readonly projectId: ElementId;
  /** Root first, parent last. */
  readonly path: readonly PathItem[];
  readonly current: string;
}

/** « Pages / Personnages / PNJ / Ilsa Varenne » (E2E-04-01); each ancestor opens its element. */
export function Breadcrumb({ projectId, path, current }: BreadcrumbProps) {
  const { t } = useTranslation();
  return (
    <nav aria-label={t("shell.breadcrumb")} className={styles["breadcrumb"]}>
      <ol className={styles["list"]}>
        {path.map((item) => (
          <Fragment key={item.id}>
            <li>
              <Link
                to="/p/$projectId/e/$elementId"
                params={{ projectId, elementId: item.id }}
                className={styles["link"]}
              >
                {item.name}
              </Link>
            </li>
            <li aria-hidden className={styles["separator"]}>
              /
            </li>
          </Fragment>
        ))}
        <li aria-current="page" className={styles["current"]}>
          {current}
        </li>
      </ol>
    </nav>
  );
}
