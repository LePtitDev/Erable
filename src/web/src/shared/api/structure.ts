import { queryOptions } from "@tanstack/react-query";
import type { ElementId } from "../ids/elementId";
import { api, unwrap, type Rebrand, type Schemas } from "./client";
import { queryKeys } from "./keys";

// Categories, property definitions and resolved values (docs/architecture.md §8.1, M2-03). Values and rules are free
// JSON: read them with src/shared/properties (parseValue, parseRules) through features/properties/definitions.ts.

export type CategoryListItem = Rebrand<Schemas["CategoryListItem"]>;
export type CategorySummary = Rebrand<Schemas["CategorySummary"]>;
export type CategoryView = Rebrand<Schemas["CategoryView"]>;
export type CategoryPropertyItem = Rebrand<Schemas["CategoryPropertyItem"]>;
export type CategoryPageItem = Rebrand<Schemas["CategoryPageItem"]>;
export type PropertyDefinitionItem = Rebrand<Schemas["PropertyDefinitionItem"]>;
export type PropertyView = Rebrand<Schemas["PropertyView"]>;
export type PagePropertyItem = Rebrand<Schemas["PagePropertyItem"]>;
export type ViolationItem = Rebrand<Schemas["ViolationItem"]>;
export type ValueOrigin = Schemas["ValueOrigin"];
export type UpdatePropertyRequest = Rebrand<Schemas["UpdatePropertyRequest"]>;
export type CreatePropertyRequest = Rebrand<Schemas["CreatePropertyRequest"]>;

/** Every live category with its parent (pickers, loop detection). */
export function categoriesQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.categories(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/categories", {
          params: { path: { projectId } },
          signal,
        }),
      ),
    staleTime: 30_000,
  });
}

export function categoryQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.category(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/categories/{id}", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

/** Pages of a category, with or without its sub-categories (F07.R4). */
export function categoryPagesQuery(
  projectId: ElementId,
  id: ElementId,
  includeSubCategories: boolean,
) {
  return queryOptions({
    queryKey: queryKeys.categoryPages(projectId, id, includeSubCategories),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/categories/{id}/pages", {
          params: { path: { projectId, id }, query: { includeSubCategories } },
          signal,
        }),
      ),
  });
}

/** Every live property definition (« Ajouter une propriété », instant validation). */
export function propertiesQuery(projectId: ElementId) {
  return queryOptions({
    queryKey: queryKeys.properties(projectId),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/properties", {
          params: { path: { projectId } },
          signal,
        }),
      ),
    staleTime: 30_000,
  });
}

export function propertyQuery(projectId: ElementId, id: ElementId) {
  return queryOptions({
    queryKey: queryKeys.property(projectId, id),
    queryFn: ({ signal }) =>
      unwrap(
        api.GET("/api/v1/projects/{projectId}/properties/{id}", {
          params: { path: { projectId, id } },
          signal,
        }),
      ),
  });
}

export function updateCategory(
  projectId: ElementId,
  id: ElementId,
  description: string,
  parentCategoryId: ElementId | null,
) {
  return unwrap(
    api.PUT("/api/v1/projects/{projectId}/categories/{id}", {
      params: { path: { projectId, id } },
      body: { description, parentCategoryId },
    }),
  );
}

/** Sets (value) or removes (undefined) the default a category gives to a property. */
export function setCategoryValue(
  projectId: ElementId,
  id: ElementId,
  propertyId: ElementId,
  value: unknown,
) {
  return value === undefined
    ? unwrap(
        api.DELETE("/api/v1/projects/{projectId}/categories/{id}/values/{propertyId}", {
          params: { path: { projectId, id, propertyId } },
        }),
      )
    : unwrap(
        api.PUT("/api/v1/projects/{projectId}/categories/{id}/values/{propertyId}", {
          params: { path: { projectId, id, propertyId } },
          body: { value },
        }),
      );
}

export function createCategory(projectId: ElementId, parentId: ElementId, name: string) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/categories", {
      params: { path: { projectId } },
      body: { parentId, name },
    }),
  );
}

export function createProperty(projectId: ElementId, body: CreatePropertyRequest) {
  return unwrap(
    api.POST("/api/v1/projects/{projectId}/properties", { params: { path: { projectId } }, body }),
  );
}

export function updateProperty(projectId: ElementId, id: ElementId, body: UpdatePropertyRequest) {
  return unwrap(
    api.PUT("/api/v1/projects/{projectId}/properties/{id}", {
      params: { path: { projectId, id } },
      body,
    }),
  );
}

/** Renames an element (the name of a definition is not part of its PUT). */
export function renameElement(projectId: ElementId, id: ElementId, name: string) {
  return unwrap(
    api.PATCH("/api/v1/projects/{projectId}/elements/{id}", {
      params: { path: { projectId, id } },
      body: { name },
    }),
  );
}

/** A page (or category) listed by a refusal of the backend: its name and its current value as text (F08.R5, R6). */
export interface BlockingPage {
  readonly id: ElementId;
  readonly name: string;
  readonly kind: "page" | "category";
  readonly value: string;
}

/** The `pages` extension of a refused property update, as typed objects; empty when absent or malformed. */
export function blockingPages(problem: Record<string, unknown> | undefined): BlockingPage[] {
  const raw = problem?.["pages"];
  if (!Array.isArray(raw)) return [];
  const pages: BlockingPage[] = [];
  for (const item of raw as unknown[]) {
    if (typeof item !== "object" || item === null) continue;
    const { id, name, kind, value } = item as Record<string, unknown>;
    if (typeof id === "string" && typeof name === "string") {
      pages.push({
        id: id as ElementId,
        name,
        kind: kind === "category" ? "category" : "page",
        value: typeof value === "string" ? value : "",
      });
    }
  }
  return pages;
}
