import { useQuery } from "@tanstack/react-query";
import { useId, useState } from "react";
import { useTranslation } from "react-i18next";
import { recentQuery } from "../../shared/api/recent";
import type { ElementId } from "../../shared/ids/elementId";
import { useAuthorName } from "../project/authors";
import { RecentList } from "../recent/RecentList";
import styles from "./SearchView.module.css";

type Tab = "viewed" | "edited";
const tabs: readonly Tab[] = ["viewed", "edited"];

/** « Consultées » (own) and « Modifiées » (everybody's, with the author) on the right of the search screen (F11). */
export function ActivityPanel({ projectId }: { readonly projectId: ElementId }) {
  const { t } = useTranslation();
  const authorName = useAuthorName();
  const recent = useQuery(recentQuery(projectId, 8));
  const [tab, setTab] = useState<Tab>("viewed");
  const prefix = useId();
  const entries =
    tab === "viewed"
      ? (recent.data?.viewed ?? []).map((v) => ({
          id: v.id,
          kind: v.kind,
          name: v.name,
          at: v.viewedAt,
        }))
      : (recent.data?.edited ?? []).map((e) => ({
          id: e.id,
          kind: e.kind,
          name: e.name,
          at: e.editedAt,
          who: authorName(e.editedBy, e.editedByName),
        }));
  return (
    <aside className={styles["activity"]} aria-label={t("search.activity")}>
      <div role="tablist" aria-label={t("search.activity")} className={styles["tabs"]}>
        {tabs.map((name) => (
          <button
            key={name}
            type="button"
            role="tab"
            id={`${prefix}-${name}`}
            aria-selected={tab === name}
            aria-controls={`${prefix}-panel`}
            tabIndex={tab === name ? 0 : -1}
            className={styles["tab"]}
            onClick={() => {
              setTab(name);
            }}
            onKeyDown={(event) => {
              if (event.key === "ArrowRight" || event.key === "ArrowLeft") {
                setTab(name === "viewed" ? "edited" : "viewed");
                event.preventDefault();
              }
            }}
          >
            {t(`recent.${name}`)}
          </button>
        ))}
      </div>
      <div role="tabpanel" id={`${prefix}-panel`} aria-labelledby={`${prefix}-${tab}`}>
        <RecentList
          projectId={projectId}
          entries={entries}
          emptyLabel={t(tab === "viewed" ? "recent.emptyViewed" : "recent.emptyEdited")}
        />
      </div>
    </aside>
  );
}
