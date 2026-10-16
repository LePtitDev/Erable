import { screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import { roots } from "../../shared/ids/roots";
import { http, server } from "../../test/msw";
import { renderApp } from "../../test/render";

const projectId = requireElementId("CA0B0E5EC0A5D1E1");
const lieux = requireElementId("8000000000000010");
const hautbois = requireElementId("5B19C0E2A4D7F311");

function element(
  id: typeof projectId,
  kind: "root" | "folder" | "page",
  name: string,
  parentId: typeof projectId | null,
) {
  return {
    id,
    kind,
    parentId,
    name,
    revision: 1,
    createdAt: "2026-03-12T10:30:00.000Z",
    createdBy: "camille",
    createdByName: "Camille R.",
    updatedAt: "2026-10-13T06:00:00.000Z",
    updatedBy: "camille",
    updatedByName: "Camille R.",
    deleted: false,
    path: parentId === null ? [] : [{ id: roots.pages, kind: "root" as const, name: "Pages" }],
  };
}

function mockProject(layout?: object) {
  const putLayouts: unknown[] = [];
  server.use(
    http.get("/api/v1/me", ({ response }) =>
      response(200).json({
        id: "local",
        name: "Arthur",
        defaultProjectsFolder: "/w",
        isServerAdmin: true,
      }),
    ),
    http.get("/api/v1/me/recent-projects", ({ response }) => response(200).json([])),
    http.put("/api/v1/me/recent-projects/{projectId}/last-element", ({ response }) =>
      response(204).empty(),
    ),
    http.get("/api/v1/me/ui-state/{key}", ({ response }) =>
      layout === undefined
        ? response.untyped(HttpResponse.json({}, { status: 404 }))
        : response.untyped(HttpResponse.json(layout)),
    ),
    http.put("/api/v1/me/ui-state/{key}", async ({ request, response }) => {
      putLayouts.push(await request.json());
      return response(204).empty();
    }),
    http.get("/api/v1/projects/{projectId}", ({ response }) =>
      response(200).json({
        id: projectId,
        name: "Chroniques d'Aube",
        folder: "/w/chroniques",
        contentLanguage: "fr",
        formatVersion: 1,
        createdAt: "2026-03-10T09:00:00.000Z",
        myRole: "admin",
      }),
    ),
    http.get("/api/v1/projects/{projectId}/tree", ({ response }) =>
      response(200).json({
        nodes: [
          {
            id: roots.pages,
            kind: "root",
            parentId: null,
            name: "Pages",
            sortOrder: 1,
            duplicateName: false,
          },
          {
            id: lieux,
            kind: "folder",
            parentId: roots.pages,
            name: "Lieux",
            sortOrder: 1,
            duplicateName: false,
          },
          {
            id: hautbois,
            kind: "page",
            parentId: lieux,
            name: "Hautbois",
            sortOrder: 1,
            duplicateName: false,
          },
        ],
        counts: { pages: 1, categories: 0, properties: 2, links: 0 },
      }),
    ),
    http.get("/api/v1/projects/{projectId}/elements/{id}", ({ params, response }) =>
      params.id === lieux
        ? response(200).json(element(lieux, "folder", "Lieux", roots.pages))
        : response.untyped(
            HttpResponse.json({ status: 404, code: "element.notFound" }, { status: 404 }),
          ),
    ),
  );
  return putLayouts;
}

describe("ProjectShell", () => {
  it("shows the project name, the counts and the reading mode in the status bar", async () => {
    mockProject();
    await renderApp(`/p/${projectId}/e/${lieux}`);

    expect(await screen.findByRole("link", { name: "Chroniques d'Aube" })).toBeInTheDocument();
    expect(
      await screen.findByText("1 page · 0 catégorie · 2 propriétés · 0 type de lien"),
    ).toBeInTheDocument();
    expect(screen.getByText("Lecture")).toBeInTheDocument();
  });

  it("shows a folder with its breadcrumb and contents", async () => {
    mockProject();
    await renderApp(`/p/${projectId}/e/${lieux}`);

    expect(await screen.findByRole("heading", { level: 1, name: "Lieux" })).toBeInTheDocument();
    expect(screen.getByRole("navigation", { name: "Fil d’Ariane" })).toHaveTextContent(
      "Pages/Lieux",
    );
    expect(await screen.findByRole("link", { name: /Hautbois/ })).toHaveAttribute(
      "href",
      `/p/${projectId}/e/${hautbois}`,
    );
  });

  it("shows « Élément introuvable » for an unknown ID, without technical detail (E2E-03-03)", async () => {
    mockProject();
    await renderApp(`/p/${projectId}/e/0123456789ABCDEF`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Élément introuvable" }),
    ).toBeInTheDocument();
    expect(screen.queryByText(/404|element\.notFound/)).not.toBeInTheDocument();
  });

  it("shows « Élément introuvable » for a malformed ID", async () => {
    mockProject();
    await renderApp(`/p/${projectId}/e/not-an-id`);

    expect(
      await screen.findByRole("heading", { level: 1, name: "Élément introuvable" }),
    ).toBeInTheDocument();
  });

  it("collapses the explorer with its button and restores the stored width", async () => {
    mockProject({ explorerOpen: true, explorerWidth: 320, sidePanelOpen: true });
    const user = userEvent.setup();
    await renderApp(`/p/${projectId}/e/${lieux}`);

    const separator = await screen.findByRole("separator", { name: "Largeur de l’explorateur" });
    await waitFor(() => {
      expect(separator).toHaveAttribute("aria-valuenow", "320");
    });
    await user.click(screen.getByRole("button", { name: "Masquer l’explorateur" }));

    expect(
      screen.queryByRole("navigation", { name: "Explorateur du projet" }),
    ).not.toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Afficher l’explorateur" })).toHaveAttribute(
      "aria-expanded",
      "false",
    );
  });

  it("resizes the explorer from the keyboard and saves the layout", async () => {
    const saved = mockProject();
    const user = userEvent.setup();
    await renderApp(`/p/${projectId}/e/${lieux}`);

    const separator = await screen.findByRole("separator", { name: "Largeur de l’explorateur" });
    separator.focus();
    await user.keyboard("{ArrowRight}{ArrowRight}");

    expect(separator).toHaveAttribute("aria-valuenow", "312");
    await waitFor(() => {
      expect(saved).toContainEqual(expect.objectContaining({ explorerWidth: 312 }));
    });
  });
});
