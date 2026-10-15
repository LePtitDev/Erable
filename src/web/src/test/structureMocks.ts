import type {
  CategoryListItem,
  CategoryPageItem,
  CategoryView,
  PropertyDefinitionItem,
} from "../shared/api/structure";
import { requireElementId, type ElementId } from "../shared/ids/elementId";
import { roots } from "../shared/ids/roots";
import { details, ids } from "./projectMocks";
import { http, server } from "./msw";
import type { TreeNodeItem } from "../shared/api/tree";

/** IDs of the structure of the small « Chroniques d'Aube » of the component tests. */
export const structureIds = {
  entite: requireElementId("E1A0000000000001"),
  personnage: requireElementId("E1A0000000000002"),
  pnj: ids.categoryPnj,
  marchand: requireElementId("8000000000000021"),
  garde: requireElementId("8000000000000022"),
  region: requireElementId("8000000000000023"),
  niveau: requireElementId("44B107E9C2A63F58"),
  hostile: requireElementId("8000000000000031"),
  role: requireElementId("8000000000000032"),
  propRegion: requireElementId("8000000000000033"),
};

const s = structureIds;

export const definitions: Record<"niveau" | "hostile" | "role" | "region", PropertyDefinitionItem> =
  {
    niveau: {
      id: s.niveau,
      name: "Niveau",
      key: "niveau",
      description: "",
      type: "int",
      default: null,
      rules: [
        { op: "greater_or_equal", value: 1 },
        { op: "less_or_equal", value: 60 },
      ],
      options: [],
    },
    hostile: {
      id: s.hostile,
      name: "Hostile",
      key: "hostile",
      description: "",
      type: "bool",
      default: null,
      rules: [],
      options: [],
    },
    role: {
      id: s.role,
      name: "Rôle",
      key: "role",
      description: "",
      type: "enum",
      default: null,
      rules: [],
      options: [
        { key: "marchande", label: "Marchande" },
        { key: "garde", label: "Garde" },
      ],
    },
    region: {
      id: s.propRegion,
      name: "Région",
      key: "region",
      description: "",
      type: "ref",
      default: null,
      rules: [{ op: "in_category", value: s.region }],
      options: [],
    },
  };

/** Tree nodes of the structure, to add to `chroniquesNodes()`. */
export function structureNodes(): TreeNodeItem[] {
  const node = (
    id: ElementId,
    kind: TreeNodeItem["kind"],
    name: string,
    order: number,
  ): TreeNodeItem => ({
    id,
    kind,
    parentId: kind === "category" ? roots.categories : roots.properties,
    name,
    sortOrder: order,
    duplicateName: false,
  });
  return [
    node(s.entite, "category", "Entité", 10),
    node(s.personnage, "category", "Personnage", 11),
    node(s.marchand, "category", "Marchand", 12),
    node(s.garde, "category", "Garde", 13),
    node(s.region, "category", "Région", 14),
    node(s.niveau, "propertyDefinition", "Niveau", 1),
    node(s.hostile, "propertyDefinition", "Hostile", 2),
    node(s.role, "propertyDefinition", "Rôle", 3),
    node(s.propRegion, "propertyDefinition", "Région", 4),
  ];
}

export const categoryList: CategoryListItem[] = [
  { id: s.entite, name: "Entité", parentCategoryId: null },
  { id: s.personnage, name: "Personnage", parentCategoryId: s.entite },
  { id: s.pnj, name: "PNJ", parentCategoryId: s.personnage },
  { id: s.marchand, name: "Marchand", parentCategoryId: s.pnj },
  { id: s.garde, name: "Garde", parentCategoryId: s.pnj },
  { id: s.region, name: "Région", parentCategoryId: null },
];

/** The category screen of PNJ, as the backend answers it. */
export function pnjView(nodes: readonly TreeNodeItem[]): CategoryView {
  const node = nodes.find((n) => n.id === s.pnj);
  if (node === undefined) throw new Error("PNJ is not in the nodes.");
  return {
    element: details(node, nodes),
    description: "Personnage non joueur.",
    parent: { id: s.personnage, name: "Personnage" },
    lineage: [
      { id: s.entite, name: "Entité" },
      { id: s.personnage, name: "Personnage" },
    ],
    subCategories: [
      { id: s.garde, name: "Garde" },
      { id: s.marchand, name: "Marchand" },
    ],
    properties: [
      {
        property: definitions.hostile,
        value: false,
        origin: "own",
        source: null,
        overrides: false,
      },
      { property: definitions.niveau, value: 10, origin: "own", source: null, overrides: true },
      {
        property: definitions.role,
        value: "garde",
        origin: "inherited",
        source: { id: s.personnage, name: "Personnage" },
        overrides: false,
      },
    ],
    pageCount: 0,
    pageCountWithSubCategories: 2,
  };
}

export const pnjPages: CategoryPageItem[] = [
  {
    id: ids.orsen,
    name: "Capitaine Orsen",
    path: [],
    via: { id: s.garde, name: "Garde" },
  },
  {
    id: ids.ilsa,
    name: "Ilsa Varenne",
    path: [],
    via: { id: s.marchand, name: "Marchand" },
  },
];

/** Mocks the category and property endpoints; returns the recorded writes. */
export function mockStructure(options: { readonly view?: CategoryView } = {}) {
  const writes = {
    categories: [] as { id: string; body: unknown }[],
    values: [] as { id: string; propertyId: string; body: unknown }[],
  };
  server.use(
    http.get("/api/v1/projects/{projectId}/categories", ({ response }) =>
      response(200).json(categoryList),
    ),
    http.get("/api/v1/projects/{projectId}/categories/{id}", ({ response }) => {
      if (options.view === undefined) throw new Error("No category view mocked.");
      return response(200).json(options.view);
    }),
    http.put(
      "/api/v1/projects/{projectId}/categories/{id}",
      async ({ params, request, response }) => {
        writes.categories.push({ id: params.id, body: await request.json() });
        if (options.view === undefined) throw new Error("No category view mocked.");
        return response(200).json(options.view);
      },
    ),
    http.put(
      "/api/v1/projects/{projectId}/categories/{id}/values/{propertyId}",
      async ({ params, request, response }) => {
        writes.values.push({
          id: params.id,
          propertyId: params.propertyId,
          body: await request.json(),
        });
        if (options.view === undefined) throw new Error("No category view mocked.");
        return response(200).json(options.view);
      },
    ),
    http.get("/api/v1/projects/{projectId}/categories/{id}/pages", ({ query, response }) =>
      response(200).json(query.get("includeSubCategories") === "true" ? pnjPages : []),
    ),
    http.get("/api/v1/projects/{projectId}/properties", ({ response }) =>
      response(200).json(Object.values(definitions)),
    ),
  );
  return writes;
}
