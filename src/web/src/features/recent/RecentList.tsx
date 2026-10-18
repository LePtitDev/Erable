import { Link } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import type { ElementKind } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { useFormatters } from "../../shared/i18n/format";
import { KindBadge } from "../../shared/ui/KindBadge";
import styles from "./RecentList.module.css";

export interface RecentEntry {
  readonly id: ElementId;
  readonly kind: ElementKind;
  readonly name: string;
  readonly at: string;
  /** Author of the change, for the « modifiées » list. */
  readonly who?: string;
}

/** Elements with their type and a relative time (« il y a 2 h »): the lists of F11, under the tree and on the search screen. */
export function RecentList({
  projectId,
  entries,
  emptyLabel,
  onNavigate,
  ariaLabelledBy,
}: {
  readonly projectId: ElementId;
  readonly entries: readonly RecentEntry[];
  readonly emptyLabel: string;
  readonly onNavigate?: () => void;
  readonly ariaLabelledBy?: string;
}) {
  const { t } = useTranslation();
  const format = useFormatters();
  if (entries.length === 0) {
    return <p className={styles["empty"]}>{emptyLabel}</p>;
  }
  return (
    <ul className={styles["list"]} aria-labelledby={ariaLabelledBy}>
      {entries.map((entry) => (
        <li key={entry.id}>
          <Link
            to="/p/$projectId/e/$elementId"
            params={{ projectId, elementId: entry.id }}
            className={styles["item"]}
            onClick={onNavigate}
            title={t(`kinds.${entry.kind}`)}
          >
            <KindBadge kind={entry.kind} />
            <span className={styles["text"]}>
              <span className={styles["name"]}>{entry.name}</span>
              {entry.who !== undefined && <span className={styles["who"]}>{entry.who}</span>}
            </span>
            <time
              className={styles["when"]}
              dateTime={entry.at}
              title={format.dateTime(new Date(entry.at))}
            >
              {format.relative(new Date(entry.at))}
            </time>
          </Link>
        </li>
      ))}
    </ul>
  );
}
