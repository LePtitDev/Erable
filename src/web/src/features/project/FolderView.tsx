import { useQuery } from "@tanstack/react-query";
import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import type { ElementDetails } from "../../shared/api/tree";
import { treeQuery } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { KindBadge } from "../../shared/ui/KindBadge";
import { ElementHeader } from "./ElementHeader";
import styles from "./FolderView.module.css";

/** A root or a folder: what it contains (a folder only files elements, docs/architecture.md §5.1). */
export function FolderView({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  const { t } = useTranslation();
  const tree = useQuery(treeQuery(projectId));
  const children = (tree.data?.nodes ?? []).filter((n) => n.parentId === element.id);
  return (
    <div className={styles["view"]}>
      <ElementHeader projectId={projectId} element={element} />
      {tree.isSuccess && children.length === 0 && (
        <p className={styles["empty"]}>{t("folder.empty")}</p>
      )}
      {children.length > 0 && (
        <ul className={styles["grid"]} aria-label={t("folder.contents")}>
          {children.map((child) => (
            <li key={child.id}>
              <Link
                to="/p/$projectId/e/$elementId"
                params={{ projectId, elementId: child.id }}
                className={styles["card"]}
              >
                <KindBadge kind={child.kind} />
                <span className={styles["name"]}>{child.name}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
