import { HttpResponse } from "msw";
import type { ElementDetails, ElementKind, TreeNodeItem } from "../shared/api/tree";
import { requireElementId, type ElementId } from "../shared/ids/elementId";
import { roots } from "../shared/ids/roots";
import { http, server } from "./msw";

/** IDs of a small « Chroniques d'Aube » used by component tests. */
export const ids = {
  project: requireElementId("CA0B0E5EC0A5D1E1"),
  personnages: requireElementId("8000000000000011"),
  pnj: requireElementId("8000000000000012"),
  ilsa: requireElementId("3F2A9C14E07B5D21"),
  dialogues: requireElementId("8000000000000013"),
  orsen: requireElementId("7A1C4E90B3F2D615"),
  lieux: requireElementId("8000000000000010"),
  hautbois: requireElementId("5B19C0E2A4D7F311"),
  colGele: requireElementId("8C3E1F7A90B2D446"),
  categoryPnj: requireElementId("0D7EA95162C3B804"),
};

function n(
  id: ElementId,
  kind: ElementKind,
  parentId: ElementId | null,
  name: string,
  sortOrder: number,
): TreeNodeItem {
  return { id, kind, parentId, name, sortOrder, duplicateName: false };
}

export function chroniquesNodes(): TreeNodeItem[] {
  return [
    n(roots.pages, "root", null, "Pages", 1),
    n(roots.categories, "root", null, "Catégories", 2),
    n(roots.properties, "root", null, "Propriétés", 3),
    n(roots.links, "root", null, "Liens", 4),
    n(ids.personnages, "folder", roots.pages, "Personnages", 1),
    n(ids.pnj, "folder", ids.personnages, "PNJ", 1),
    n(ids.ilsa, "page", ids.pnj, "Ilsa Varenne", 1),
    n(ids.dialogues, "page", ids.ilsa, "Dialogues", 1),
    n(ids.orsen, "page", ids.pnj, "Capitaine Orsen", 2),
    n(ids.lieux, "folder", roots.pages, "Lieux", 2),
    n(ids.hautbois, "page", ids.lieux, "Hautbois", 1),
    n(ids.colGele, "page", ids.lieux, "Col gelé", 2),
    n(ids.categoryPnj, "category", roots.categories, "PNJ", 1),
  ];
}

export function details(node: TreeNodeItem, nodes: readonly TreeNodeItem[]): ElementDetails {
  const path: ElementDetails["path"] = [];
  let parent = node.parentId;
  while (parent !== null) {
    const p = nodes.find((x) => x.id === parent);
    if (p === undefined) break;
    path.unshift({ id: p.id, kind: p.kind, name: p.name });
    parent = p.parentId;
  }
  return {
    id: node.id,
    kind: node.kind,
    parentId: node.parentId,
    name: node.name,
    revision: 1,
    createdAt: "2026-03-12T10:30:00.000Z",
    createdBy: "camille",
    createdByName: "Camille R.",
    updatedAt: "2026-10-13T06:00:00.000Z",
    updatedBy: "camille",
    updatedByName: "Camille R.",
    deleted: false,
    path,
  };
}

/**
 * Mocks the endpoints of an open project around a mutable list of nodes; returns the recorded writes.
 * `pages` gives the Markdown of the pages the test opens.
 */
export function mockProject(
  options: {
    nodes?: TreeNodeItem[];
    pages?: Record<string, string>;
    uiState?: Record<string, unknown>;
  } = {},
) {
  const nodes = options.nodes ?? chroniquesNodes();
  const writes = {
    patches: [] as { id: string; body: unknown }[],
    created: [] as unknown[],
    uiState: [] as unknown[],
  };
  const counts = () => ({
    pages: nodes.filter((x) => x.kind === "page").length,
    categories: nodes.filter((x) => x.kind === "category").length,
    properties: 0,
    links: 0,
  });
  server.use(
    http.get("/api/v1/me", ({ response }) =>
      response(200).json({ id: "local", name: "Arthur", defaultProjectsFolder: "/w" }),
    ),
    http.get("/api/v1/me/recent-projects", ({ response }) => response(200).json([])),
    http.put("/api/v1/me/recent-projects/{projectId}/last-element", ({ response }) =>
      response(204).empty(),
    ),
    http.get("/api/v1/me/ui-state/{key}", ({ params, response }) => {
      const value = options.uiState?.[params.key];
      return value === undefined
        ? response.untyped(HttpResponse.json({}, { status: 404 }))
        : response.untyped(HttpResponse.json(value as object));
    }),
    http.put("/api/v1/me/ui-state/{key}", async ({ request, response }) => {
      writes.uiState.push(await request.json());
      return response(204).empty();
    }),
    http.get("/api/v1/projects/{projectId}", ({ response }) =>
      response(200).json({
        id: ids.project,
        name: "Chroniques d'Aube",
        folder: "/w/chroniques",
        contentLanguage: "fr",
        formatVersion: 1,
        createdAt: "2026-03-10T09:00:00.000Z",
      }),
    ),
    http.get("/api/v1/projects/{projectId}/tree", ({ response }) =>
      response(200).json({ nodes: [...nodes], counts: counts() }),
    ),
    http.get("/api/v1/projects/{projectId}/elements/{id}", ({ params, response }) => {
      const node = nodes.find((x) => x.id === params.id);
      return node === undefined
        ? response.untyped(
            HttpResponse.json({ status: 404, code: "element.notFound" }, { status: 404 }),
          )
        : response(200).json(details(node, nodes));
    }),
    http.patch(
      "/api/v1/projects/{projectId}/elements/{id}",
      async ({ params, request, response }) => {
        const body = (await request.json()) as { name?: string; parentId?: ElementId };
        writes.patches.push({ id: params.id, body });
        const i = nodes.findIndex((x) => x.id === params.id);
        const node = nodes[i];
        if (node === undefined)
          return response.untyped(HttpResponse.json({ status: 404 }, { status: 404 }));
        nodes[i] = {
          ...node,
          name: body.name ?? node.name,
          parentId: body.parentId ?? node.parentId,
        };
        return response(200).json(details(nodes[i], nodes));
      },
    ),
    http.post("/api/v1/projects/{projectId}/folders", async ({ request, response }) => {
      const body = (await request.json()) as { parentId: ElementId; name: string };
      writes.created.push({ kind: "folder", ...body });
      const node = n(
        requireElementId(`90000000000000${String(10 + writes.created.length)}`),
        "folder",
        body.parentId,
        body.name,
        99,
      );
      nodes.push(node);
      return response(201).json(details(node, nodes));
    }),
    http.post("/api/v1/projects/{projectId}/pages", async ({ request, response }) => {
      const body = (await request.json()) as { parentId: ElementId; name: string };
      writes.created.push({ kind: "page", ...body });
      const node = n(
        requireElementId(`A0000000000000${String(10 + writes.created.length)}`),
        "page",
        body.parentId,
        body.name,
        99,
      );
      nodes.push(node);
      return response(201).json(details(node, nodes));
    }),
    http.get("/api/v1/projects/{projectId}/pages/{id}", ({ params, response }) => {
      const node = nodes.find((x) => x.id === params.id);
      if (node === undefined)
        return response.untyped(HttpResponse.json({ status: 404 }, { status: 404 }));
      return response(200).json({
        element: details(node, nodes),
        markdown: options.pages?.[node.id] ?? "",
        subPages: nodes
          .filter((x) => x.parentId === node.id && x.kind === "page")
          .map((x) => ({ id: x.id, name: x.name })),
        attachments: [],
      });
    }),
  );
  return { nodes, writes };
}
