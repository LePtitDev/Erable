import { useQuery } from "@tanstack/react-query";
import { useParams, useSearch } from "@tanstack/react-router";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { ApiError } from "../../shared/api/client";
import { recordLastElement } from "../../shared/api/projects";
import { elementQuery } from "../../shared/api/tree";
import { NotFoundScreen } from "./ErrorScreens";
import { FolderView } from "./FolderView";
import { GenericElementView } from "./GenericElementView";
import { PageRoute } from "../page/PageRoute";
import { useCountView } from "../recent/useCountView";
import { CategoryView } from "../category/CategoryView";
import { LinkTypeView } from "../links/LinkTypeView";
import { PropertyView } from "../properties/PropertyView";
import { useCanEdit } from "./access";

/** `/p/:projectId/e/:elementId`: the kind of the element chooses the view (docs/architecture.md §3.1, F03.R3). */
export function ElementRoute() {
  const { t } = useTranslation();
  const { projectId, elementId } = useParams({ from: "/p/$projectId/e/$elementId" });
  const { edit: editRequested } = useSearch({ from: "/p/$projectId/e/$elementId" });
  // A reader following an edit link reads instead (F14.R1).
  const canEdit = useCanEdit();
  const edit = editRequested === true && canEdit;
  const enabled = projectId !== null && elementId !== null;
  const element = useQuery({
    ...elementQuery(projectId ?? ("" as never), elementId ?? ("" as never)),
    enabled,
  });

  const found = element.isSuccess && !element.data.deleted;
  useEffect(() => {
    if (found && projectId !== null && elementId !== null) {
      // Best effort: the home screen reopens the project here (E2E-01-02).
      recordLastElement(projectId, elementId).catch(() => undefined);
    }
  }, [found, projectId, elementId]);

  // Roots and attachments are not listed among the viewed elements.
  useCountView(
    projectId,
    elementId,
    found && element.data.kind !== "root" && element.data.kind !== "attachment",
  );

  if (
    !enabled ||
    (element.isError && element.error instanceof ApiError && element.error.status === 404)
  ) {
    return <NotFoundScreen />;
  }
  if (element.isError) throw element.error;
  if (element.isPending) return <p aria-busy="true">{t("common.loading")}</p>;
  if (element.data.deleted) return <NotFoundScreen />;

  switch (element.data.kind) {
    case "page":
      return <PageRoute projectId={projectId} element={element.data} />;
    case "root":
    case "folder":
      return <FolderView projectId={projectId} element={element.data} />;
    case "category":
      return (
        <CategoryView
          key={element.data.id}
          projectId={projectId}
          element={element.data}
          edit={edit}
        />
      );
    case "propertyDefinition":
      return (
        <PropertyView
          key={element.data.id}
          projectId={projectId}
          element={element.data}
          edit={edit}
        />
      );
    case "linkType":
      return (
        <LinkTypeView
          key={element.data.id}
          projectId={projectId}
          element={element.data}
          edit={edit}
        />
      );
    case "attachment":
      return <GenericElementView projectId={projectId} element={element.data} />;
  }
}
