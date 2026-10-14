import { useSearch } from "@tanstack/react-router";
import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { PageEditor } from "./PageEditor";
import { PageView } from "./PageView";

/** Page screen: reading (F04), or editing with `?edit=true` (F05). */
export function PageRoute({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  const search = useSearch({ from: "/p/$projectId/e/$elementId" });
  return search.edit === true ? (
    <PageEditor projectId={projectId} element={element} focusTitle={search.focus === "title"} />
  ) : (
    <PageView projectId={projectId} element={element} />
  );
}
