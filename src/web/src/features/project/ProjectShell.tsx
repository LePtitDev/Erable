import { useQuery } from "@tanstack/react-query";
import { Outlet, useParams } from "@tanstack/react-router";
import { useTranslation } from "react-i18next";
import { projectQuery } from "../../shared/api/projects";
import { NotFoundScreen } from "./ErrorScreens";

/** Frame of the project screens; the full shell (explorer, panels, status bar) arrives with M1-09. */
export function ProjectShell() {
  const { t } = useTranslation();
  const { projectId } = useParams({ from: "/p/$projectId" });
  const project = useQuery({
    ...projectQuery(projectId ?? ("" as never)),
    enabled: projectId !== null,
  });
  if (projectId === null || project.isError) {
    return <NotFoundScreen />;
  }
  return (
    <div>
      <h1>{project.data?.name ?? t("common.loading")}</h1>
      <Outlet />
    </div>
  );
}
