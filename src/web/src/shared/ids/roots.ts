import { requireElementId } from "./elementId";

/** Fixed IDs of the four roots, identical in every project (docs/architecture.md §5.1). */
export const roots = {
  pages: requireElementId("0000000000000001"),
  categories: requireElementId("0000000000000002"),
  properties: requireElementId("0000000000000003"),
  links: requireElementId("0000000000000004"),
} as const;
