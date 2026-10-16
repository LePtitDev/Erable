import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { recentQuery } from "../../shared/api/recent";
import type { ElementId } from "../../shared/ids/elementId";
import { RecentList } from "./RecentList";

/** « Récemment consultées » under the explorer (mockup « Page — lecture », F11). */
export function RecentViewedPanel({
  projectId,
  headingId,
}: {
  readonly projectId: ElementId;
  readonly headingId: string;
}) {
  const { t } = useTranslation();
  const recent = useQuery(recentQuery(projectId, 6));
  return (
    <RecentList
      projectId={projectId}
      ariaLabelledBy={headingId}
      emptyLabel={t("recent.emptyViewed")}
      entries={(recent.data?.viewed ?? []).map((v) => ({
        id: v.id,
        kind: v.kind,
        name: v.name,
        at: v.viewedAt,
      }))}
    />
  );
}
