import { useTranslation } from "react-i18next";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { ElementHeader } from "./ElementHeader";
import styles from "./FolderView.module.css";

/**
 * Generic place for the kinds whose screen arrives later (category M2-04, property M2-05, link type M2-07) and for
 * attachments (completed by M1-14): header and kind only.
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
