import { screen, waitFor, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { HttpResponse } from "msw";
import { describe, expect, it } from "vitest";
import { requireElementId } from "../../shared/ids/elementId";
import { http, server } from "../../test/msw";
import { renderApp } from "../../test/render";

const atlasId = requireElementId("A71A5000000000A1");
const chroniquesId = requireElementId("CA0B0E5EC0A5D1E1");

function mockHome(recent: { name: string; status: "project" | "missing" | "tooNew" }[] = []) {
  server.use(
    http.get("/api/info", ({ response }) =>
      response(200).json({
        appVersion: "0.1.0",
        apiVersion: "v1",
        formatVersion: 1,
        mode: "desktop",
      }),
    ),
    http.get("/api/v1/me", ({ response }) =>
      response(200).json({
        id: "local",
        name: "Arthur",
        defaultProjectsFolder: "C:\\Users\\arthur\\Documents\\Erable",
      }),
    ),
    http.get("/api/v1/me/recent-projects", ({ response }) =>
      response(200).json(
        recent.map((r, i) => ({
          projectId: i === 0 ? chroniquesId : atlasId,
          name: r.name,
          folder: `C:\\Wikis\\${r.name}`,
          lastOpenedAt: "2026-10-13T06:00:00.000Z",
          lastElementId: null,
          status: r.status,
        })),
      ),
    ),
  );
}

describe("HomeScreen", () => {
  it("greets the user by name", async () => {
    mockHome();
    await renderApp("/");

    expect(
      await screen.findByRole("heading", { level: 1, name: "Bonjour, Arthur" }),
    ).toBeInTheDocument();
  });

  it("lists recent projects with their relative date", async () => {
    mockHome([{ name: "Chroniques d'Aube", status: "project" }]);
    await renderApp("/");

    const item = await screen.findByRole("listitem");
    expect(within(item).getByRole("button", { name: "Chroniques d'Aube" })).toBeInTheDocument();
    expect(within(item).getByText("CA")).toBeInTheDocument();
    expect(within(item).getByText("il y a 2 h")).toBeInTheDocument();
  });

  it("greys a missing project and removes it without opening it (F01.R2)", async () => {
    mockHome([{ name: "Perdu", status: "missing" }]);
    let removed: string | undefined;
    server.use(
      http.delete("/api/v1/me/recent-projects/{projectId}", ({ params, response }) => {
        removed = params.projectId;
        return response(204).empty();
      }),
    );
    const user = userEvent.setup();
    await renderApp("/");

    const item = await screen.findByRole("listitem");
    expect(within(item).getByText("Introuvable")).toBeInTheDocument();
    expect(within(item).queryByRole("button", { name: "Perdu" })).not.toBeInTheDocument();
    await user.click(
      within(item).getByRole("button", { name: "Retirer Perdu des projets récents" }),
    );

    await waitFor(() => {
      expect(removed).toBe(chroniquesId);
    });
  });

  it("creates a project in the default folder with the chosen content language", async () => {
    mockHome();
    let body: unknown;
    server.use(
      http.post("/api/v1/projects", async ({ request, response }) => {
        body = await request.json();
        return response(201).json({
          id: atlasId,
          name: "Atlas",
          folder: "C:\\Users\\arthur\\Documents\\Erable\\Atlas",
          contentLanguage: "en",
          formatVersion: 1,
          createdAt: "2026-10-13T08:00:00.000Z",
        });
      }),
      http.get("/api/v1/projects/{projectId}", ({ response }) =>
        response.untyped(HttpResponse.json({ status: 404 }, { status: 404 })),
      ),
    );
    const user = userEvent.setup();
    const { router } = await renderApp("/");

    await user.click(await screen.findByRole("button", { name: /Nouveau projet/ }));
    const dialog = screen.getByRole("dialog", { name: "Nouveau projet" });
    await user.type(within(dialog).getByLabelText("Nom du projet"), "Atlas");
    await user.selectOptions(within(dialog).getByLabelText("Langue du contenu"), "English");
    expect(
      within(dialog).getByText("Dossier du projet : C:\\Users\\arthur\\Documents\\Erable\\Atlas"),
    ).toBeInTheDocument();
    await user.click(within(dialog).getByRole("button", { name: "Créer le projet" }));

    await waitFor(() => {
      expect(router.state.location.pathname.startsWith(`/p/${atlasId}`)).toBe(true);
    });
    expect(body).toEqual({
      folder: "C:\\Users\\arthur\\Documents\\Erable\\Atlas",
      name: "Atlas",
      contentLanguage: "en",
    });
  });

  it("refuses a project of a newer format with a clear message (F01.R3)", async () => {
    mockHome();
    let opened = false;
    server.use(
      http.post("/api/v1/projects/inspect", ({ response }) =>
        response(200).json({
          status: "tooNew",
          name: "Futur",
          projectId: atlasId,
          formatVersion: 9,
        }),
      ),
      http.post("/api/v1/projects/open", ({ response }) => {
        opened = true;
        return response.untyped(HttpResponse.json({ status: 422 }, { status: 422 }));
      }),
    );
    const user = userEvent.setup();
    await renderApp("/");
    await screen.findByRole("heading", { level: 1, name: "Bonjour, Arthur" });

    await user.keyboard("{Control>}o{/Control}");
    const dialog = screen.getByRole("dialog", { name: "Ouvrir un dossier" });
    await user.type(within(dialog).getByLabelText("Dossier"), "C:\\Futur");
    await user.click(within(dialog).getByRole("button", { name: "Ouvrir" }));

    expect(await within(dialog).findByRole("alert")).toHaveTextContent("format 9, plus récent");
    expect(opened).toBe(false);
  });

  it("offers to create a project in a folder without manifest, writing nothing first (F01.R1)", async () => {
    mockHome();
    let created = false;
    server.use(
      http.post("/api/v1/projects/inspect", ({ response }) =>
        response(200).json({
          status: "notAProject",
          name: null,
          projectId: null,
          formatVersion: null,
        }),
      ),
      http.post("/api/v1/projects", ({ response }) => {
        created = true;
        return response.untyped(HttpResponse.json({ status: 409 }, { status: 409 }));
      }),
    );
    const user = userEvent.setup();
    await renderApp("/");
    await screen.findByRole("heading", { level: 1, name: "Bonjour, Arthur" });

    await user.keyboard("{Control>}o{/Control}");
    await user.type(screen.getByLabelText("Dossier"), "D:\\Notes\\Bestiaire");
    await user.click(screen.getByRole("button", { name: "Ouvrir" }));

    const confirm = await screen.findByRole("dialog", { name: "Créer un projet dans ce dossier" });
    expect(within(confirm).getByLabelText("Nom du projet")).toHaveValue("Bestiaire");
    expect(created).toBe(false);
    await user.click(within(confirm).getByRole("button", { name: "Annuler" }));
    expect(created).toBe(false);
  });
});
