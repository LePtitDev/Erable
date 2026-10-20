import { useTranslation } from "react-i18next";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { ElementHeader } from "./ElementHeader";
import styles from "./FolderView.module.css";

/**
 * Generic place for the kinds without a screen of their own, such as attachments: header and kind only.
 */
export function GenericElementView({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  const { t } = useTranslation();
  return (
    <div className={styles["view"]}>
      <ElementHeader projectId={projectId} element={element} />
      <p className={styles["empty"]}>
        {t("element.kindScreenSoon", { kind: t(`kinds.${element.kind}`) })}
      </p>
    </div>
  );
}
