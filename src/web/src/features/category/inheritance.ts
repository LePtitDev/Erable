import type { CategoryListItem } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";

/**
 * The loop a new parent would create (F07.R2), as the backend reports it: the category, the chain down to the new
 * parent, the category again (« Personnage › PNJ › Marchand › Personnage », E2E-07-03); null when there is none.
 * Instant feedback only: the backend refuses the same choice.
 */
export function loopOf(
  categories: readonly CategoryListItem[],
  id: ElementId,
  parentId: ElementId | null,
): ElementId[] | null {
  if (parentId === null) return null;
  if (parentId === id) return [id, id];
  const parents = new Map(categories.map((c) => [c.id, c.parentCategoryId] as const));
  const chain: ElementId[] = [];
  const seen = new Set<ElementId>();
  for (
    let current: ElementId | null = parentId;
    current !== null;
    current = parents.get(current) ?? null
  ) {
    if (current === id) return [id, ...chain.reverse(), id];
    if (seen.has(current)) return null;
    seen.add(current);
    chain.push(current);
  }
  return null;
}
