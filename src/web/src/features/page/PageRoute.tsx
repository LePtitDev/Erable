import type { ElementDetails } from "../../shared/api/tree";
import type { ElementId } from "../../shared/ids/elementId";
import { PageView } from "./PageView";

/** Page screen: reading (M1-11); editing arrives with M1-12. */
export function PageRoute({
  projectId,
  element,
}: {
  readonly projectId: ElementId;
  readonly element: ElementDetails;
}) {
  return <PageView projectId={projectId} element={element} />;
}
