import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useState } from "react";
import {
  pagePreviewQuery,
  type PageStructure,
  type PageView as PageData,
} from "../../shared/api/pages";
import type { CategorySummary } from "../../shared/api/structure";
import type { ElementId } from "../../shared/ids/elementId";
import {
  draftOfStructure,
  editedProperty,
  sameStructure,
  savedValues,
  toPageProperty,
  type PageProperty,
  type StructureDraft,
} from "../properties/pageProperties";

/** The body the backend wants for a draft: categories and every own value. */
export function structureOf(draft: StructureDraft): PageStructure {
  return {
    categories: draft.categories,
    properties: savedValues(draft).map(([propertyId, value]) => ({ propertyId, value })),
  };
}

function useDebounced<T>(value: T, delayMs: number): T {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => {
      setDebounced(value);
    }, delayMs);
    return () => {
      clearTimeout(timer);
    };
  }, [value, delayMs]);
  return debounced;
}

/**
 * Categories and own values of a page while it is edited (F05.R6, F07.R3): the draft, what saving it would resolve to
 * (asked to the backend, which owns inheritance), and the number of validation errors for the status bar. Nothing is
 * asked while the draft equals the saved page.
 */
export function usePageStructure(projectId: ElementId, pageId: ElementId, page: PageData) {
  const saved = useMemo(() => page.properties.map(toPageProperty), [page.properties]);
  const [baseline, setBaseline] = useState(() => draftOfStructure(page.categories, saved));
  const [draft, setDraft] = useState(baseline);
  const dirty = !sameStructure(draft, baseline);
  const settled = useDebounced(draft, 150);
  const preview = useQuery({
    ...pagePreviewQuery(projectId, pageId, structureOf(settled)),
    enabled: !sameStructure(settled, baseline),
    placeholderData: keepPreviousData,
  });

  const live = dirty && preview.data !== undefined;
  const properties: readonly PageProperty[] = useMemo(
    () => (live ? preview.data.properties.map(toPageProperty) : saved),
    [live, preview.data, saved],
  );
  const categories: readonly CategorySummary[] = live ? preview.data.categories : page.categories;
  const errors = useMemo(
    () => properties.reduce((sum, p) => sum + editedProperty(p, draft).shownViolations.length, 0),
    [properties, draft],
  );

  return {
    draft,
    setDraft,
    dirty,
    properties,
    categories,
    errors,
    /** Call once the draft is saved: it becomes the reference the next edits are compared with. */
    markSaved: () => {
      setBaseline(draft);
    },
  };
}
