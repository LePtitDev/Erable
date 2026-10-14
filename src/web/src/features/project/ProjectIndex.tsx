import { useQuery } from "@tanstack/react-query";
import { Navigate, useParams } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { recentProjectsQuery } from "../../shared/api/projects";
import { roots } from "../../shared/ids/roots";

/** `/p/:projectId`: reopens the last element viewed in this project (E2E-01-02), else the Pages root. */
export function ProjectIndex() {
  const { t } = useTranslation();
  const { projectId } = useParams({ from: "/p/$projectId/" });
  const recent = useQuery(recentProjectsQuery);
  if (projectId === null) return null;
  if (recent.isPending) return <p aria-busy="true">{t("common.loading")}</p>;
  const last = recent.data?.find((r) => r.projectId === projectId)?.lastElementId ?? null;
  return (
    <Navigate
      to="/p/$projectId/e/$elementId"
      params={{ projectId, elementId: last ?? roots.pages }}
      replace
    />
  );
}
