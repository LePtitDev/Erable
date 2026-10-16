import type {
  CategoryListItem,
  CategoryPageItem,
  CategoryView,
  PagePropertyItem,
  PropertyDefinitionItem,
  PropertyView,
} from "../shared/api/structure";
import { HttpResponse } from "msw";
import type { BacklinkItem, LinkTypeView, PageLinksView } from "../shared/api/links";
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
    parentId:
      kind === "category" ? roots.categories : kind === "linkType" ? roots.links : roots.properties,
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
    node(linkIds.mentor, "linkType", "Mentor de", 1),
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

/** The property screen of Niveau, as the backend answers it (E2E-08-01). */
export function niveauView(nodes: readonly TreeNodeItem[]): PropertyView {
  const node = nodes.find((n) => n.id === s.niveau);
  if (node === undefined) throw new Error("Niveau is not in the nodes.");
  return {
    element: details(node, nodes),
    definition: definitions.niveau,
    usedBy: [
      { id: s.pnj, name: "PNJ" },
      { id: s.marchand, name: "Marchand" },
    ],
    filledCount: 4,
    violations: [
      {
        pageId: ids.ilsa,
        pageName: "Ilsa Varenne",
        value: 72,
        violations: [{ rule: { op: "less_or_equal", value: 60 }, index: null }],
      },
      {
        pageId: ids.orsen,
        pageName: "Capitaine Orsen",
        value: 99,
        violations: [{ rule: { op: "less_or_equal", value: 60 }, index: null }],
      },
    ],
  };
}

/** Mocks the property endpoints; returns the recorded writes. The first answer to a PUT can be a refusal. */
export function mockProperty(
  view: PropertyView,
  options: { readonly refuse?: { code: string; pages: unknown[] } } = {},
) {
  const writes = { puts: [] as { id: string; body: unknown }[], renames: [] as unknown[] };
  let refused = false;
  server.use(
    http.get("/api/v1/projects/{projectId}/properties/{id}", ({ response }) =>
      response(200).json(view),
    ),
    http.put(
      "/api/v1/projects/{projectId}/properties/{id}",
      async ({ params, request, response }) => {
        const body = await request.json();
        writes.puts.push({ id: params.id, body });
        if (options.refuse !== undefined && !refused) {
          refused = true;
          return response.untyped(
            HttpResponse.json(
              { status: 422, code: options.refuse.code, pages: options.refuse.pages },
              { status: 422 },
            ),
          );
        }
        return response(200).json(view);
      },
    ),
    http.patch("/api/v1/projects/{projectId}/elements/{id}", async ({ request, response }) => {
      writes.renames.push(await request.json());
      return response(200).json(view.element);
    }),
  );
  return writes;
}

/** The properties of Ilsa Varenne as the backend resolves them (E2E-05-12, E2E-07-05, E2E-08-08). */
export function ilsaStructure(): {
  categories: { id: ElementId; name: string }[];
  properties: PagePropertyItem[];
} {
  return {
    categories: [{ id: s.pnj, name: "PNJ" }],
    properties: [
      {
        property: definitions.hostile,
        ownValue: null,
        value: false,
        origin: "inherited",
        sources: [{ id: s.pnj, name: "PNJ" }],
        candidates: [],
        violations: [],
        fallback: null,
      },
      {
        property: definitions.niveau,
        ownValue: 72,
        value: 72,
        origin: "own",
        sources: [],
        candidates: [],
        violations: [{ rule: { op: "less_or_equal", value: 60 }, index: null }],
        fallback: {
          value: 10,
          origin: "inherited",
          sources: [{ id: s.pnj, name: "PNJ" }],
          candidates: [],
        },
      },
    ],
  };
}

export const linkIds = {
  mentor: requireElementId("8000000000000041"),
  ally: requireElementId("8000000000000042"),
};

/** Mocks the link endpoints for a page; returns the recorded writes. */
export function mockLinks(
  options: {
    readonly outgoing?: PageLinksView["outgoing"];
    readonly incoming?: PageLinksView["incoming"];
    readonly mentions?: BacklinkItem[];
  } = {},
) {
  const writes = { created: [] as unknown[], deleted: [] as string[], updated: [] as unknown[] };
  const type = (id: ElementId, name: string, inverseName: string | null) => ({
    id,
    name,
    description: "",
    inverseName,
    linkCount: 0,
  });
  server.use(
    http.get("/api/v1/projects/{projectId}/link-types", ({ response }) =>
      response(200).json([
        type(linkIds.ally, "Allié de", "Allié de"),
        type(linkIds.mentor, "Mentor de", "Élève de"),
      ]),
    ),
    http.get("/api/v1/projects/{projectId}/pages/{id}/links", ({ response }) =>
      response(200).json({ outgoing: options.outgoing ?? [], incoming: options.incoming ?? [] }),
    ),
    http.get("/api/v1/projects/{projectId}/elements/{id}/backlinks", ({ response }) =>
      response(200).json(options.mentions ?? []),
    ),
    http.post("/api/v1/projects/{projectId}/links", async ({ request, response }) => {
      const body = await request.json();
      writes.created.push(body);
      return response(201).json({ id: ids.project, ...body });
    }),
    http.delete("/api/v1/projects/{projectId}/links/{id}", ({ params, response }) => {
      writes.deleted.push(params.id);
      return response(204).empty();
    }),
  );
  return writes;
}

/** The screen of the « Mentor de » link type. */
export function mentorView(nodes: readonly TreeNodeItem[]): LinkTypeView {
  const node = nodes.find((n) => n.id === linkIds.mentor);
  if (node === undefined) throw new Error("Mentor de is not in the nodes.");
  return {
    element: details(node, nodes),
    description: "Un personnage qui en forme un autre.",
    inverseName: "Élève de",
    linkCount: 0,
  };
}
