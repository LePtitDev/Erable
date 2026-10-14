import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { FolderView } from "../project/FolderView";

/** Page screen: reading (M1-11) and editing (M1-12). */
export function PageRoute({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  return <FolderView projectId={projectId} element={element} />;
}
