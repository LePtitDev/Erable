import type { ReactNode } from "react";
import { useTranslation } from "react-i18next";
import type { ElementDetails } from "../../shared/api/tree";
import { useFormatters } from "../../shared/i18n/format";
import type { ElementId } from "../../shared/ids/elementId";
import { Breadcrumb } from "../../shared/ui/Breadcrumb";
import { IdChip } from "../../shared/ui/IdChip";
import { KindBadge } from "../../shared/ui/KindBadge";
import { useAuthorName } from "./authors";
import styles from "./ElementHeader.module.css";

interface ElementHeaderProps {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
  /** Buttons next to the title (« Modifier »…). */
  readonly actions?: ReactNode;
  /** Chips after the ID (categories in M2). */
  readonly chips?: ReactNode;
  /** Replaces the title (inline rename, editor). */
  readonly title?: ReactNode;
  /** Shows the large kind badge before the title (category and property screens). */
  readonly kind?: boolean;
  /** ID of the title, for an `aria-labelledby`. */
  readonly titleId?: string;
}

/** Breadcrumb, title, ID, modification line (mockup « Page — lecture », F03, E2E-04-01). */
export function ElementHeader({
  projectId,
  element,
  actions,
  chips,
  title,
  kind = false,
  titleId,
}: ElementHeaderProps) {
  const { t } = useTranslation();
  const format = useFormatters();
  const author = useAuthorName();
  const root = element.kind === "root";
  return (
    <header className={styles["header"]}>
      {!root && <Breadcrumb projectId={projectId} path={element.path} current={element.name} />}
      <div className={styles["titleRow"]}>
        <div className={styles["titleGroup"]}>
          {kind && <KindBadge kind={element.kind} size="lg" />}
          {title ?? (
            <h1 className={styles["title"]} id={titleId}>
              {element.name}
            </h1>
          )}
        </div>
        {actions !== undefined && <div className={styles["actions"]}>{actions}</div>}
      </div>
      {!root && (
        <div className={styles["meta"]}>
          <IdChip id={element.id} />
          {chips}
          <span className={styles["stamp"]}>
            {t("element.modified", {
              when: format.relative(new Date(element.updatedAt)),
              who: author(element.updatedBy, element.updatedByName),
            })}{" "}
            · {t("element.created", { date: format.date(new Date(element.createdAt)) })}
          </span>
        </div>
      )}
    </header>
  );
}
